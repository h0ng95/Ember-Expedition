using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class ExpeditionResetValidation
    {
        static int checks;
        static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Check(bool value, string why) { checks++; if (!value) throw new Exception("Reset integration: " + why); }
        static void Set(object obj, string name, object value) { obj.GetType().GetField(name, flags).SetValue(obj, value); }
        static object Get(object obj, string name) { return obj.GetType().GetField(name, flags).GetValue(obj); }
        static object Call(object obj, string name, params object[] args) { return obj.GetType().GetMethod(name, flags).Invoke(obj, args); }
        static void Advance(ExpeditionRun run, int target)
        { while (run.Victories < target) { run.BeginBattle(); run.ResolveBattle(true); if (!run.HasEnded) run.Continue(); } }
        static ExpeditionAccountSave Snapshot(CatalogDatabase db, ExpeditionRun run, ExpeditionHistory history, ExpeditionProgress progress = null)
        {
            progress = progress ?? ExpeditionProgress.New(); progress.Observe(run);
            var journal = ExpeditionJournal.New(); journal.Observe(run); history.Observe(run);
            var save = new ExpeditionAccountSave { Version = 2, HasRun = true, Run = run.Capture(), History = history.Capture(),
                Progress = progress.Capture(), Journal = journal.Capture(), TutorialMask = 3, LessonMask = 7 };
            JsonUtility.FromJson<ExpeditionAccountSave>(JsonUtility.ToJson(save)).Validate(db);
            return save;
        }
        public static void ValidateAndBuild()
        {
            AccountSettingsValidation.Validate();
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var emptyAccount = new ExpeditionAccountSave { Version = 2, HasRun = false,
                Progress = ExpeditionProgress.New().Capture(), Journal = ExpeditionJournal.New().Capture(),
                History = new ExpeditionHistory().Capture() };
            var emptyReload = JsonUtility.FromJson<ExpeditionAccountSave>(JsonUtility.ToJson(emptyAccount));
            emptyReload.Validate(db);
            Check(!emptyReload.HasRun && ExpeditionHistory.Restore(db, emptyReload.History).Best == null,
                "empty account round-trip must not create a run or best record");
            string root = Path.GetFullPath("Logs/v045-" + Guid.NewGuid().ToString("N"));
            var go = new GameObject("Reset QA"); go.SetActive(false);
            Font font = null;
            try
            {
                var screen = go.AddComponent<ExpeditionScreen>(); Set(screen, "database", db);
                Set(screen, "playtest", go.AddComponent<PlaytestRecorder>());
                Call(screen, "InitializeAccountStorage", root);
                var archived = ExpeditionProgress.Restore(new ExpeditionProgressSave { Version = 2, BestWave = 30, CompletedWaves = 30,
                    ResearchIds = CatalogLegacyResearch.Nodes.Select(n => n.Id).ToArray() });
                Set(screen, "progress", archived); Set(screen, "journal", ExpeditionJournal.New());
                Set(screen, "startupReady", true); Set(screen, "tutorialMask", 3); Set(screen, "lessonMask", 7);
                foreach (string form in new[] { "standard", "tactical", "ember", "frost" })
                {
                    Set(screen, "selectedFormationId", form); Call(screen, "NewRun");
                    var fresh = (ExpeditionRun)Get(screen, "run");
                    Check(fresh != null && fresh.StartFormationId == form && !fresh.UsesLegacyRules && fresh.LegacyResearchIds.Count == 0, "fresh start ignores archived nodes");
                    var saved = JsonUtility.FromJson<ExpeditionAccountSave>(((ExpeditionSaveStore)Get(screen, "accountStore")).Load().Payload);
                    saved.Validate(db); checks++;
                    Check(saved.Version == 2 && saved.Run.Version == 9 && saved.Progress.ResearchIds.Length == 6, "migration archive retained");
                }
                var run = (ExpeditionRun)Get(screen, "run");
                Advance(run, 30); Call(screen, "Save", "battle_win", null, 0f);
                var history = (ExpeditionHistory)Get(screen, "recordHistory");
                int count = history.Recent.Count;
                Call(screen, "Save", "checkpoint", null, -1f);
                Check(history.Recent.Count == count && history.Best.Score == 333000, "score registered twice");
                var store = (ExpeditionSaveStore)Get(screen, "accountStore");
                var reloaded = JsonUtility.FromJson<ExpeditionAccountSave>(store.Load().Payload); reloaded.Validate(db);
                Check(ExpeditionHistory.Restore(db, reloaded.History).Best.Score == 333000, "history lost on reload");
                font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 18);
                var text = new GUIStyle { font = font, fontSize = 18, wordWrap = true };
                Check(text.CalcHeight(new GUIContent("이번 원정 전용  /  새 원정 시작 시 연구가 초기화됩니다"), 760) <= 30, "research header clipped");
                text.fontSize = 15;
                Check(text.CalcHeight(new GUIContent("최근 원정 30개 보관 · 최고 기록 별도 보관 · 포기/이전 규칙은 순위 제외"), 1076) <= 36, "history caption clipped");
                string fixtures = Path.GetFullPath("Logs/v045-fixtures"); Directory.CreateDirectory(fixtures);
                var demo = ExpeditionRun.New(db, 45011); Advance(demo, 10);
                File.WriteAllText(Path.Combine(fixtures, "research.json"), JsonUtility.ToJson(Snapshot(db, demo, new ExpeditionHistory())));
                File.WriteAllText(Path.Combine(fixtures, "records.json"), store.Load().Payload);
                var old = ExpeditionRun.NewLegacy(db, 45008, legacyResearchIds: archived.ResearchIds, startFormationId: "ember");
                Advance(old, 5);
                File.WriteAllText(Path.Combine(fixtures, "legacy.json"), JsonUtility.ToJson(Snapshot(db, old, new ExpeditionHistory(), archived)));
                Debug.Log("PASS reset/history Unity integration: " + checks + " checks; actual NewRun, archive preservation, atomic result history, reload and labels.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); if (font != null) UnityEngine.Object.DestroyImmediate(font); }
            PrototypeSetup.BuildAndroid();
        }
    }
}
