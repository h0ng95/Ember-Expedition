using System;
using System.IO;
using Expedition.Core;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class ExpeditionStoryValidation
    {
        static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Story validation: " + message); }

        static ExpeditionJournal RoundTrip(ExpeditionJournal journal)
        {
            string json = JsonUtility.ToJson(journal.Capture());
            var save = JsonUtility.FromJson<ExpeditionJournalSave>(json);
            Check(save != null && save.Version == 1 && save.UnlockedIds != null,
                "Unity serializer lost the separate journal save");
            var restored = ExpeditionJournal.Restore(save);
            Check(JsonUtility.ToJson(restored.Capture()) == json,
                "Unity journal JSON changed after reload");
            return restored;
        }

        [MenuItem("Expedition/Validate Story")]
        public static void Validate()
        {
            TextAsset catalogAsset = Resources.Load<TextAsset>("Content/unit-catalog-v010");
            Check(catalogAsset != null, "unit catalog resource is missing");
            CatalogDatabase catalog = CatalogDatabase.FromJson(catalogAsset.text);
            var run = ExpeditionRun.New(catalog, 25);
            var journal = ExpeditionJournal.New();
            Check(journal.IsUnlocked("intro") && journal.UnlockedCount == 1,
                "new journal must begin with intro only");
            string runBefore = JsonUtility.ToJson(run.Capture());
            Check(journal.Observe(run) && journal.IsUnlocked("forest") &&
                JsonUtility.ToJson(run.Capture()) == runBefore,
                "observing a new expedition changed its save or missed forest");
            journal = RoundTrip(journal);
            for (int wave = 1; wave <= 10; wave++)
            { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
            Check(journal.Observe(run) && journal.UnlockedCount == 6 &&
                !journal.IsUnlocked("mine-clear"),
                "ten victories must reveal castle clear and mine, not mine clear");
            var old = run.Capture();
            old.Version = 2; old.Wave = 10; old.Phase = ExpeditionPhase.Completed;
            old.Gold -= 50; old.LastReward = 0;
            var migrated = ExpeditionRun.Restore(catalog,
                JsonUtility.FromJson<ExpeditionRunSave>(JsonUtility.ToJson(old)));
            var oldJournal = ExpeditionJournal.New();
            Check(migrated.Phase == ExpeditionPhase.Preparation && migrated.Wave == 11 &&
                migrated.Gold == run.Gold && oldJournal.Observe(migrated) &&
                oldJournal.UnlockedCount == 6 && !oldJournal.IsUnlocked("mine-clear"),
                "old ten-wave completion did not backfill the right chapters");
            oldJournal = RoundTrip(oldJournal);
            for (int wave = 11; wave <= ExpeditionRun.TotalWaves; wave++)
            { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
            Check(oldJournal.Observe(run) && oldJournal.IsUnlocked("mine-clear") &&
                oldJournal.IsUnlocked("frost-clear") && oldJournal.IsUnlocked("garden-clear") &&
                oldJournal.IsUnlocked("palace-clear") && oldJournal.UnlockedCount == 13 &&
                !oldJournal.Observe(run), "thirty-wave clear did not unlock once");
            RoundTrip(oldJournal);
            Debug.Log("Expedition story validated: Unity JSON and old-save observation.");
        }

        public static void BuildAndroid()
        {
            Validate();
            FrostEnemyArtValidation.GenerateManifest();
            FrostEnemyArtValidation.Validate();
            GardenEnemyArtValidation.GenerateManifest();
            GardenEnemyArtValidation.Validate();
            PalaceEnemyArtValidation.GenerateManifest();
            PalaceEnemyArtValidation.Validate();
            AllyBodyScaleValidation.BuildAndroid();
        }

        public static void ValidateNarrativeAndBuild()
        {
            Validate();
            ExpeditionDialogueValidation.Validate();
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 19);
            int measurements = 0;
            try
            {
                var style = new GUIStyle { font = font, wordWrap = true, padding = new RectOffset(2, 2, 2, 2) };
                Action<string, int, float, float> fit = (text, size, width, height) => {
                    style.fontSize = size;
                    Check(style.CalcHeight(new GUIContent(text), width) <= height, "text clipping: " + text);
                    measurements++;
                };
                foreach (StoryEntry entry in ExpeditionStory.Entries)
                {
                    fit(entry.Excerpt, 19, 866, 60);
                    fit(entry.Title, 18, 238, 44);
                    fit(entry.Title, 26, 560, 53);
                }
                fit(ExpeditionStory.HomeBrief, 19, 906, 64);
                fit(ExpeditionStory.ReplayBrief, 19, 906, 64);
                fit(ExpeditionStory.CompletionBrief, 19, 916, 45);
                fit("아직 발견하지 못한 이야기", 26, 560, 53);
                fit("서약의 결말 읽기", 18, 262, 60);
                fit("발견한 단서 읽기", 18, 393, 60);
                fit("원정의 기록을 다시 펼치다", 26, 464, 44);
                fit("왕국의 불을 찾는 서약", 26, 464, 44);
            }
            finally { UnityEngine.Object.DestroyImmediate(font); }

            var catalog = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            Directory.CreateDirectory("Logs/v059-fixtures");
            ExportNarrativeFixture("fresh", null, catalog);
            foreach (int boss in new[] { 5, 10, 15, 20, 25, 30 })
            {
                var run = ExpeditionRun.New(catalog, (uint)(59000 + boss));
                while (run.Wave < boss) { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
                run.BeginBattle(); run.ResolveBattle(true);
                ExportNarrativeFixture("result-" + boss, run, catalog);
            }
            Debug.Log("PASS narrative layout: " + measurements + " text measurements; 7 isolated account fixtures, stable journal IDs and Unity JSON.");
            PrototypeSetup.BuildAndroid();
        }

        static void ExportNarrativeFixture(string name, ExpeditionRun run, CatalogDatabase catalog)
        {
            var journal = ExpeditionJournal.New();
            var progress = ExpeditionProgress.New();
            var history = ExpeditionHistory.Restore(catalog, null);
            if (run != null) { journal.Observe(run); progress.Observe(run); history.Observe(run); }
            var account = new ExpeditionAccountSave {
                Version = 2, HasRun = run != null, Run = run == null ? null : run.Capture(),
                Journal = journal.Capture(), Progress = progress.Capture(), History = history.Capture(),
                TutorialMask = run == null ? 0 : 3, LessonMask = run == null ? 0 : 7
            };
            account.Validate(catalog);
            string json = JsonUtility.ToJson(account);
            JsonUtility.FromJson<ExpeditionAccountSave>(json).Validate(catalog);
            File.WriteAllText("Logs/v059-fixtures/" + name + ".json", json);
        }
    }
}
