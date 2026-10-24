using System;
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
                oldJournal.UnlockedCount == 7 && !oldJournal.Observe(run),
                "fifteen-wave clear did not unlock once");
            RoundTrip(oldJournal);
            Debug.Log("Expedition story validated: Unity JSON and old-save observation.");
        }

        public static void BuildAndroid()
        {
            Validate();
            AllyBodyScaleValidation.BuildAndroid();
        }
    }
}
