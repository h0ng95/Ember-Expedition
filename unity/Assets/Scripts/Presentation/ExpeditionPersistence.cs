using System;
using System.IO;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        ExpeditionSaveStore accountStore;
        ExpeditionAccountSave loadedAccount;
        SaveReadState accountReadState;
        bool storageBlocked, savingAccount;
        ExpeditionHistory recordHistory = new ExpeditionHistory();

        void ValidateAccountJson(string json)
        {
            var snapshot = JsonUtility.FromJson<ExpeditionAccountSave>(json);
            if (snapshot == null) throw new InvalidDataException("Empty account.");
            snapshot.Validate(database);
        }
        void InitializeAccountStorage(string directory = null)
        {
            if (accountStore != null) return;
            accountStore = new ExpeditionSaveStore(directory ?? Path.Combine(Application.persistentDataPath, "Saves"), ValidateAccountJson);
            var result = accountStore.Load(); accountReadState = result.State;
            if (result.Payload != null) loadedAccount = JsonUtility.FromJson<ExpeditionAccountSave>(result.Payload);
            if (loadedAccount != null && (loadedAccount.Version >= 2 && !loadedAccount.HasRun ||
                loadedAccount.Version == 1 && loadedAccount.Run != null && loadedAccount.Run.Version == 0)) loadedAccount.Run = null;
            recordHistory = ExpeditionHistory.Restore(database, loadedAccount == null || loadedAccount.Version < 2 ? null : loadedAccount.History);
            if (result.State == SaveReadState.Recovered)
                error = CatalogText.Get("text.0436");
            storageBlocked = result.State == SaveReadState.Damaged || result.State == SaveReadState.FutureVersion;
            if (storageBlocked)
            {
                badSave = badProgress = true;
                error = result.State == SaveReadState.FutureVersion ?
                    CatalogText.Get("text.0437") :
                    CatalogText.Get("text.0438");
            }
        }
        void PersistAccount()
        {
            if (storageBlocked || badSave || badProgress) throw new InvalidOperationException(CatalogText.Get("text.0439"));
            if (accountStore == null || journal == null || progress == null) throw new InvalidOperationException(CatalogText.Get("text.0440"));
            recordHistory.Observe(run);
            var snapshot = new ExpeditionAccountSave { Version = 2, HasRun = run != null, Run = run == null ? null : run.Capture(),
                Progress = progress.Capture(), Journal = journal.Capture(), TutorialMask = tutorialMask, LessonMask = lessonMask & 7,
                CombinationGuideSeen = (lessonMask & (int)ExpeditionLesson.Combination) != 0,
                History = recordHistory.Capture() };
            accountStore.Commit(JsonUtility.ToJson(snapshot));
            journalDirty = progressDirty = false;
            savePresent = run != null;
        }
        void SaveGuidePreferences()
        {
            if (!startupReady || savingAccount || storageBlocked || badSave || badProgress) return;
            try { PersistAccount(); }
            catch (Exception) { Notice(CatalogText.Get("text.0441")); }
        }
        void FinalizeAccountLoad()
        {
            if (storageBlocked || badSave || badProgress) return;
            // Migrate only after all old keys have been read. Keep those keys untouched.
            if (accountReadState == SaveReadState.Empty || accountReadState == SaveReadState.Recovered ||
                loadedAccount != null && loadedAccount.Version < 2)
                try { PersistAccount(); }
                catch (Exception) { Notice(CatalogText.Get("text.0442")); }
        }
    }
}
