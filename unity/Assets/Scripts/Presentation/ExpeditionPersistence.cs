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
            if (result.State == SaveReadState.Recovered)
                error = "저장 문제를 발견해 마지막 정상 기록으로 복구했습니다.\n최근 행동 일부는 다시 진행해야 할 수 있습니다.";
            storageBlocked = result.State == SaveReadState.Damaged || result.State == SaveReadState.FutureVersion;
            if (storageBlocked)
            {
                badSave = badProgress = true;
                error = result.State == SaveReadState.FutureVersion ?
                    "더 새로운 버전의 저장 기록입니다.\n게임을 업데이트해 주세요. 기존 파일은 보관됩니다." :
                    "저장과 백업을 모두 읽지 못했습니다.\n기록을 보관하고 덮어쓰기를 멈췄습니다.";
            }
        }
        void PersistAccount()
        {
            if (storageBlocked || badSave || badProgress) throw new InvalidOperationException("기존 기록을 보호하기 위해 저장을 중지했습니다.");
            if (accountStore == null || journal == null || progress == null) throw new InvalidOperationException("저장 준비 전입니다.");
            var snapshot = new ExpeditionAccountSave { Version = 1, Run = run == null ? null : run.Capture(),
                Progress = progress.Capture(), Journal = journal.Capture(), TutorialMask = tutorialMask, LessonMask = lessonMask };
            accountStore.Commit(JsonUtility.ToJson(snapshot));
            journalDirty = progressDirty = false;
            savePresent = run != null;
        }
        void SaveGuidePreferences()
        {
            if (!startupReady || savingAccount || storageBlocked || badSave || badProgress) return;
            try { PersistAccount(); }
            catch (Exception) { Notice("안내 상태를 저장하지 못했습니다. 다음 저장 때 다시 시도합니다."); }
        }
        void FinalizeAccountLoad()
        {
            if (storageBlocked || badSave || badProgress) return;
            // Migrate only after all old keys have been read. Keep those keys untouched.
            if (accountReadState == SaveReadState.Empty || accountReadState == SaveReadState.Recovered)
                try { PersistAccount(); }
                catch (Exception) { Notice("기록 백업을 저장하지 못했습니다. 다음 저장 때 다시 시도합니다."); }
        }
    }
}
