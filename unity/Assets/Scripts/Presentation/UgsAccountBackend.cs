using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Expedition.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

namespace Expedition.Presentation
{
    [Serializable]
    public sealed class CloudAccountConfig
    {
        public bool Enabled;
        public bool RankingsEnabled;
        public string EnvironmentName, GoogleWebClientId;
        public static CloudAccountConfig Load()
        {
            var asset = Resources.Load<TextAsset>("Content/cloud-account-config");
            return asset == null ? new CloudAccountConfig() : JsonUtility.FromJson<CloudAccountConfig>(asset.text);
        }
        public bool IsReady
        {
            get { Guid id; return Enabled && Guid.TryParse(Application.cloudProjectId, out id) &&
                !string.IsNullOrEmpty(EnvironmentName) && !string.IsNullOrEmpty(GoogleWebClientId) &&
                GoogleWebClientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal); }
        }
        public string Scope { get { return Application.cloudProjectId + "/" + EnvironmentName; } }
    }
    [Serializable]
    public sealed class CloudAccountDocument
    {
        public int Version = 1;
        public CloudCheckpoint Current, Previous;
    }
    // Only backup/restore lives here. These client-writable snapshots MUST NOT authorize a ranked score.
    public sealed class UgsAccountBackend : ICloudSaveBackend
    {
        const string Key = "ignis_account_v1";
        readonly string playerId;
        public UgsAccountBackend(string playerId) { this.playerId = playerId; }
        public static async Task InitializeAsync(CloudAccountConfig config)
        {
            if (!config.IsReady) throw new InvalidOperationException("Cloud account configuration is incomplete.");
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName(config.EnvironmentName));
        }
        public static bool Linked
        {
            get { return UnityServices.State == ServicesInitializationState.Initialized &&
                AuthenticationService.Instance.IsAuthorized &&
                !string.IsNullOrEmpty(AuthenticationService.Instance.PlayerInfo?.GetGoogleId()); }
        }
        public static async Task ResumeAsync(CloudAccountConfig config)
        {
            await InitializeAsync(config);
            if (!AuthenticationService.Instance.IsAuthorized && !AuthenticationService.Instance.SessionTokenExists) return;
            if (!AuthenticationService.Instance.IsAuthorized)
                await AuthenticationService.Instance.SignInAnonymouslyAsync(); // Restores a cached linked session too.
            await AuthenticationService.Instance.GetPlayerInfoAsync();
        }
        public static async Task ConnectGoogleAsync(CloudAccountConfig config, string idToken)
        {
            await InitializeAsync(config);
            var auth = AuthenticationService.Instance;
            if (!auth.IsAuthorized) await auth.SignInAnonymouslyAsync();
            // A linked session must never relink/force-transfer a different Google identity.
            if (!string.IsNullOrEmpty(auth.PlayerInfo?.GetGoogleId()))
            { auth.SignOut(true); await auth.SignInWithGoogleAsync(idToken); }
            else
            {
                try { await auth.LinkWithGoogleAsync(idToken); }
                catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                { auth.SignOut(true); await auth.SignInWithGoogleAsync(idToken); }
            }
            await auth.GetPlayerInfoAsync();
            if (!Linked) throw new InvalidOperationException("Google identity not linked.");
        }
        void RequireOwner()
        {
            if (!Linked || AuthenticationService.Instance.PlayerId != playerId)
                throw new InvalidOperationException("Cloud session changed.");
        }
        public async Task<CloudRead> ReadAsync()
        {
            RequireOwner();
            var items = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key });
            RequireOwner();
            if (!items.ContainsKey(Key)) return new CloudRead();
            var item = items[Key];
            string json = item.Value.GetAs<string>();
            if (json == null || Encoding.UTF8.GetByteCount(json) > 4 * 1024 * 1024) throw new ArgumentException("Invalid cloud data size.");
            var document = JsonUtility.FromJson<CloudAccountDocument>(json);
            if (document != null && document.Version > 1) throw new FutureSaveVersionException();
            if (document == null || document.Version != 1 || document.Current == null || string.IsNullOrEmpty(item.WriteLock))
                throw new ArgumentException("Invalid cloud document.");
            return new CloudRead { Checkpoint = document.Current, WriteLock = item.WriteLock };
        }
        public async Task WriteAsync(CloudCheckpoint value, CloudRead previous)
        {
            RequireOwner();
            if (previous.Checkpoint != null && string.IsNullOrEmpty(previous.WriteLock)) throw new CloudWriteConflictException();
            string json = JsonUtility.ToJson(new CloudAccountDocument { Current = value, Previous = previous.Checkpoint });
            if (Encoding.UTF8.GetByteCount(json) > 4 * 1024 * 1024) throw new ArgumentException("Cloud document too large.");
            // A null write lock is used ONLY for a server-confirmed missing key. Existing saves always use CAS.
            // UGS has no create-only lock: simultaneous first uploads are a documented launch test case.
            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, SaveItem> {
                    { Key, new SaveItem(json, previous.WriteLock) }
                });
            }
            catch (CloudSaveConflictException) { throw new CloudWriteConflictException(); }
            RequireOwner();
        }
    }
}
