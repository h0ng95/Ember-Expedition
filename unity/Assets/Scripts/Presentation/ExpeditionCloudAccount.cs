using System;
using System.IO;
using System.Threading.Tasks;
using Expedition.Core;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        const string CloudOptInKey = "ignis.cloud.opt-in.v1";
        CloudAccountConfig cloudConfig;
        CloudSaveSynchronizer cloudSync;
        LocalCloudSave cloudLocal;
        GoogleAccountSignIn googleSignIn;
        bool cloudBusy, cloudDirty, cloudAttemptedResume, cloudConfirmLocal, cloudConfirmSignOut;
        float cloudNextAttempt;
        int cloudFailures;
        string cloudStatus = "cloud.guest";
        string cloudUiPayload;
        public static readonly Rect AccountWindow = new Rect(185, 62, 910, 596);

        void InitializeCloudAccount()
        {
            cloudConfig = CloudAccountConfig.Load();
            cloudLocal = new LocalCloudSave(this);
            if (!storageBlocked) cloudUiPayload = cloudLocal.ReadPayload();
            cloudStatus = cloudConfig.IsReady ? "cloud.guest" : "cloud.setup";
        }
        void CloudSavedLocally(string payload)
        {
            cloudUiPayload = payload;
            cloudDirty = true;
            if (cloudStatus == "cloud.synced") cloudStatus = "cloud.pending";
            // Debounce preparation edits. Never send a request for every pointer movement.
            if (cloudNextAttempt < Time.realtimeSinceStartup) cloudNextAttempt = Time.realtimeSinceStartup + 15;
        }
        void TickCloudAccount()
        {
            if (!startupReady || cloudConfig == null || !cloudConfig.IsReady || cloudBusy || storageBlocked ||
                Time.realtimeSinceStartup < cloudNextAttempt || run != null && run.Phase == ExpeditionPhase.Battle) return;
            if (Application.internetReachability == NetworkReachability.NotReachable) return;
            if ((!cloudAttemptedResume || (cloudFailures > 0 || cloudSync != null) && !UgsAccountBackend.Linked) && PlayerPrefs.GetInt(CloudOptInKey, 0) == 1)
            { cloudAttemptedResume = true; RunCloudOperation(ResumeCloudAccount); return; }
            if (cloudDirty && cloudSync != null && cloudSync.Pending == null && UgsAccountBackend.Linked &&
                Application.internetReachability != NetworkReachability.NotReachable)
                RunCloudOperation(SyncCloudAccount);
        }
        async void RunCloudOperation(Func<Task> operation)
        {
            if (cloudBusy) return;
            cloudBusy = true; cloudConfirmLocal = cloudConfirmSignOut = false; cloudStatus = "cloud.working";
            try { await operation(); cloudFailures = 0; }
            catch (OperationCanceledException) { cloudStatus = "cloud.cancelled"; }
            catch (FutureSaveVersionException) { cloudStatus = "cloud.future"; cloudDirty = false; }
            catch (CloudWriteConflictException)
            { cloudStatus = "cloud.changed"; cloudSync = null; cloudDirty = false; }
            catch (ArgumentException) { cloudStatus = "cloud.invalid"; cloudDirty = false; }
            catch (Exception)
            {
                // Do not log SDK exception bodies: a provider may include identity/token metadata.
                cloudStatus = "cloud.failed"; cloudFailures = Mathf.Min(5, cloudFailures + 1);
            }
            finally
            {
                cloudBusy = false;
                cloudNextAttempt = Time.realtimeSinceStartup + Mathf.Min(300, 30 * Mathf.Pow(2, cloudFailures));
            }
        }
        async Task ResumeCloudAccount()
        {
            await UgsAccountBackend.ResumeAsync(cloudConfig);
            if (!UgsAccountBackend.Linked) { cloudSync = null; cloudStatus = "cloud.signin_required"; return; }
            CreateCloudSync(); await SyncCloudAccount();
        }
        async Task ConnectCloudAccount()
        {
            if (googleSignIn == null)
            {
                var receiver = new GameObject("IgnisGoogleAccount-" + GetInstanceID());
                receiver.transform.SetParent(transform);
                googleSignIn = receiver.AddComponent<GoogleAccountSignIn>();
            }
            string token = await googleSignIn.SignInAsync(cloudConfig.GoogleWebClientId);
            try { await UgsAccountBackend.ConnectGoogleAsync(cloudConfig, token); }
            finally { token = null; }
            PlayerPrefs.SetInt(CloudOptInKey, 1); PlayerPrefs.Save();
            cloudAttemptedResume = true;
            CreateCloudSync(); await SyncCloudAccount();
        }
        void CreateCloudSync()
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            cloudSync = new CloudSaveSynchronizer(cloudConfig.Scope, playerId, cloudLocal, new UgsAccountBackend(playerId));
        }
        async Task SyncCloudAccount()
        {
            if (!UgsAccountBackend.Linked) { await ResumeCloudAccount(); return; }
            if (cloudSync == null) CreateCloudSync();
            var decision = await cloudSync.SyncAsync();
            cloudDirty = false;
            cloudStatus = decision == CloudSyncDecision.Restore ? "cloud.restore_available" :
                decision == CloudSyncDecision.Conflict ? "cloud.conflict" :
                decision == CloudSyncDecision.MissingRemote ? "cloud.remote_missing" : "cloud.synced";
            if (cloudSync.Pending == null && cloudLocal.ReadReceipt()?.PayloadHash != CloudSavePolicy.Hash(cloudLocal.ReadPayload()))
            { cloudDirty = true; cloudStatus = "cloud.pending"; }
        }
        async Task ResolveCloudAccount(bool useCloud)
        {
            await cloudSync.ResolveAsync(useCloud);
            cloudDirty = false; cloudStatus = "cloud.synced";
        }
        void SignOutCloudAccount()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) AuthenticationService.Instance.SignOut(true);
            GoogleAccountSignIn.ClearSession();
            PlayerPrefs.SetInt(CloudOptInKey, 0); PlayerPrefs.Save();
            cloudSync = null; cloudDirty = false; cloudStatus = "cloud.guest"; cloudConfirmSignOut = false;
            // Keep the receipt and local progress. Another identity cannot auto-upload it.
        }
        static string CloudText(string suffix) { return CatalogText.Get("cloud." + suffix); }
        void DrawAccountPanel()
        {
            Rect content = ExpeditionUiTheme.ContentRect(AccountWindow), footer = ExpeditionUiTheme.FooterRect(AccountWindow);
            ExpeditionUiTheme.Window(AccountWindow);
            LocalizedGui.Label(new Rect(content.x, content.y, content.width, 44), CloudText("title"), heading);
            ExpeditionUiTheme.Rule(new Rect(content.x, content.y + 57, content.width, 2));
            LocalizedGui.Label(new Rect(content.x, content.y + 70, content.width, 44), CatalogText.Get(cloudStatus), hint);
            bool linked = UgsAccountBackend.Linked;
            if (cloudSync != null && cloudSync.Pending != null)
            {
                Rect left = new Rect(content.x, content.y + 132, (content.width - 20) / 2, 188);
                Rect right = new Rect(left.xMax + 20, left.y, left.width, left.height);
                DrawCloudSummary(left, CloudText("device"), cloudUiPayload, null);
                var checkpoint = cloudSync.Pending.Checkpoint;
                DrawCloudSummary(right, CloudText("cloud_copy"), checkpoint?.Payload, checkpoint);
                LocalizedGui.Label(new Rect(content.x, content.y + 326, content.width, 56),
                    cloudConfirmLocal ? CloudText("replace_confirm") : CloudText("choose_help"), body);
                GUI.enabled = !cloudBusy;
                if (LocalizedGui.Button(new Rect(left.x, content.y + 394, left.width, 52),
                    cloudConfirmLocal ? CloudText("replace_cloud") : CloudText("use_device"), button))
                { if (cloudConfirmLocal) RunCloudOperation(() => ResolveCloudAccount(false)); else cloudConfirmLocal = true; }
                GUI.enabled = !cloudBusy && checkpoint != null;
                if (LocalizedGui.Button(new Rect(right.x, content.y + 394, right.width, 52), CloudText("use_cloud"), primary))
                    RunCloudOperation(() => ResolveCloudAccount(true));
            }
            else
            {
                LocalizedGui.Label(new Rect(content.x, content.y + 128, content.width, 94),
                    linked ? CloudText("linked_help") : CloudText("guest_help"), body);
                var receipt = cloudLocal?.ReadReceipt();
                string stamp = receipt == null || receipt.Version != 1 ? CloudText("never_synced") : CloudText("last_sync") + " " +
                    new DateTime(receipt.SyncedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                LocalizedGui.Label(new Rect(content.x, content.y + 234, content.width, 34), stamp, small);
                LocalizedGui.Label(new Rect(content.x, content.y + 278, content.width, 52), CloudText("ranking_later"), small);
                GUI.enabled = !cloudBusy && cloudConfig != null && cloudConfig.IsReady && !storageBlocked;
                if (LocalizedGui.Button(new Rect(content.x, content.y + 354, 402, 58),
                    linked ? CloudText("sync_now") : CloudText("google"), primary))
                    RunCloudOperation(linked ? (Func<Task>)SyncCloudAccount : ConnectCloudAccount);
                GUI.enabled = !cloudBusy && linked;
                if (LocalizedGui.Button(new Rect(content.x + 422, content.y + 354, content.width - 422, 58),
                    cloudConfirmSignOut ? CloudText("signout_confirm") : CloudText("signout"), button))
                { if (cloudConfirmSignOut) SignOutCloudAccount(); else cloudConfirmSignOut = true; }
                if (cloudConfirmSignOut) LocalizedGui.Label(new Rect(content.x, content.y + 420, content.width, 38), CloudText("signout_help"), small);
            }
            GUI.enabled = !cloudBusy;
            ExpeditionUiTheme.FooterRule(AccountWindow);
            if (LocalizedGui.Button(footer, CloudText("back"), button)) { modal = Modal.None; cloudConfirmLocal = cloudConfirmSignOut = false; }
            GUI.enabled = true;
        }
        void DrawCloudSummary(Rect area, string label, string payload, CloudCheckpoint checkpoint)
        {
            ExpeditionUiTheme.PanelBox(area);
            LocalizedGui.Label(new Rect(area.x + 18, area.y + 12, area.width - 36, 32), label, heading);
            if (payload == null) { LocalizedGui.Label(new Rect(area.x + 18, area.y + 56, area.width - 36, 70), CloudText("empty"), body); return; }
            var state = JsonUtility.FromJson<ExpeditionAccountSave>(payload);
            string summary = state.HasRun && state.Run != null ? CatalogText.Format("cloud.run_summary", state.Run.Wave, state.Run.Gold) : CloudText("no_run");
            summary += "\n" + CatalogText.Format("cloud.progress_summary", state.Progress.BestWave);
            if (checkpoint != null) summary += "\n" + new DateTime(checkpoint.SavedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            LocalizedGui.Label(new Rect(area.x + 18, area.y + 54, area.width - 36, 118), summary, body);
        }
        sealed class LocalCloudSave : ICloudLocalSave
        {
            readonly ExpeditionScreen screen;
            readonly ExpeditionSaveStore receiptStore;
            CloudSyncReceipt cachedReceipt;
            public LocalCloudSave(ExpeditionScreen screen)
            {
                this.screen = screen;
                receiptStore = new ExpeditionSaveStore(Path.Combine(Application.persistentDataPath, "CloudLink"), ValidateReceipt);
                var state = receiptStore.Load();
                if (state.State == SaveReadState.Damaged || state.State == SaveReadState.FutureVersion)
                    cachedReceipt = new CloudSyncReceipt { Version = 0 };
                else if (state.Payload != null) cachedReceipt = JsonUtility.FromJson<CloudSyncReceipt>(state.Payload);
            }
            static void ValidateReceipt(string payload)
            {
                var receipt = JsonUtility.FromJson<CloudSyncReceipt>(payload);
                if (receipt == null || receipt.Version != 1 || string.IsNullOrEmpty(receipt.Scope) || string.IsNullOrEmpty(receipt.PlayerId) ||
                    string.IsNullOrEmpty(receipt.Revision) || receipt.PayloadHash == null || receipt.PayloadHash.Length != 64 ||
                    receipt.SyncedUtcTicks < 0 || receipt.SyncedUtcTicks > DateTime.MaxValue.Ticks)
                    throw new ArgumentException("Invalid sync receipt.");
            }
            public string ReadPayload()
            {
                var state = screen.accountStore.Load();
                if (state.Payload == null) throw new InvalidOperationException("Local save unavailable.");
                return state.Payload;
            }
            public bool HasProgress
            {
                get { var state = JsonUtility.FromJson<ExpeditionAccountSave>(ReadPayload());
                    return state.HasRun || state.Progress.BestWave > 0 || state.TutorialMask != 0 || state.LessonMask != 0 ||
                        state.CombinationGuideSeen || state.History != null && state.History.Recent != null && state.History.Recent.Length > 0; }
            }
            public CloudSyncReceipt ReadReceipt()
            {
                return cachedReceipt;
            }
            public void WriteReceipt(CloudSyncReceipt receipt) { receiptStore.Commit(JsonUtility.ToJson(receipt)); cachedReceipt = receipt; }
            public void Validate(string payload) { screen.ValidateAccountJson(payload); }
            public void Restore(string payload)
            {
                Validate(payload);
                var state = JsonUtility.FromJson<ExpeditionAccountSave>(payload);
                var nextRun = state.HasRun ? ExpeditionRun.Restore(screen.database, state.Run) : null;
                var nextProgress = ExpeditionProgress.Restore(state.Progress);
                var nextJournal = ExpeditionJournal.Restore(state.Journal);
                var nextHistory = ExpeditionHistory.Restore(screen.database, state.History);
                string archive = Path.Combine(Application.persistentDataPath, "CloudRestoreBackup");
                new ExpeditionSaveStore(archive, Validate).Commit(ReadPayload());
                screen.accountStore.Commit(payload);
                screen.cloudUiPayload = payload;
                screen.loadedAccount = state; screen.run = nextRun; screen.progress = nextProgress; screen.journal = nextJournal;
                screen.recordHistory = nextHistory; screen.savePresent = nextRun != null;
                screen.tutorialMask = state.TutorialMask;
                screen.lessonMask = state.LessonMask | (state.CombinationGuideSeen ? (int)ExpeditionLesson.Combination : 0);
                screen.home = true; screen.selectedId = null; screen.selectedRecord = null;
                screen.ReleasePreparationPreview();
                screen.error = null;
                if (nextRun != null) screen.playtest.Begin(nextRun, null);
            }
        }
    }
}
