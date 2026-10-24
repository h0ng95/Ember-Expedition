using System;
using System.IO;
using Expedition.Core;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    // Uses Unity's field serializer on genuine saves written before difficulty existed.
    public static class ExpeditionProgressValidation
    {
        static int assertions;
        static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("Progress validation: " + message);
        }

        [MenuItem("Expedition/Validate Normal Progress")]
        public static void Validate()
        {
            assertions = 0;
            var catalog = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            string fixtures = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../tests/ExpeditionProgress/Fixtures"));
            string[] names = { "preparation", "battle", "result-win", "result-loss", "defeated", "completed" };
            foreach (string name in names)
            {
                string json = File.ReadAllText(Path.Combine(fixtures, "v3-" + name + ".json"));
                Check(!json.Contains("\"Difficulty\"") && !json.Contains("\"ContentWaves\""),
                    name + ": fixture is not a genuine v3 shape");
                var old = JsonUtility.FromJson<ExpeditionRunSave>(json);
                var run = ExpeditionRun.Restore(catalog, old);
                Check(old.Version == 3 && run.Difficulty == ExpeditionDifficulty.Normal &&
                    run.ContentWaves == 15 && run.Gold == old.Gold && run.Wave == old.Wave &&
                    run.Phase == old.Phase && run.Victories == old.Victories &&
                    run.Capture().RandomState == old.RandomState &&
                    JsonUtility.ToJson(run.Capture().Roster) == JsonUtility.ToJson(old.Roster),
                    name + ": legacy state changed");
                string migratedJson = JsonUtility.ToJson(run.Capture());
                var restored = ExpeditionRun.Restore(catalog, JsonUtility.FromJson<ExpeditionRunSave>(migratedJson));
                Check(restored.Capture().Version == 4 && JsonUtility.ToJson(restored.Capture()) == migratedJson,
                    name + ": v4 JSON did not round-trip");
                var progress = ExpeditionProgress.New();
                progress.Observe(restored);
                string progressJson = JsonUtility.ToJson(progress.Capture());
                var reloaded = ExpeditionProgress.Restore(JsonUtility.FromJson<ExpeditionProgressSave>(progressJson));
                Check(reloaded.BestWave == old.Victories &&
                    reloaded.CompletedWaves == (name == "completed" ? 15 : 0) &&
                    !reloaded.HardUnlocked && !reloaded.Observe(restored) &&
                    JsonUtility.ToJson(reloaded.Capture()) == progressJson,
                    name + ": independent record changed or unlocked Hard early");
            }
            var normal = ExpeditionRun.New(catalog, 27);
            var blocked = normal.Capture(); blocked.Difficulty = ExpeditionDifficulty.Hard;
            bool rejected = false;
            try { ExpeditionRun.Restore(catalog, JsonUtility.FromJson<ExpeditionRunSave>(JsonUtility.ToJson(blocked))); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "unfinished Hard save was playable");
            // Synthetic future record tests the 30-clear boundary; it is not current playable content.
            var future = ExpeditionProgress.Restore(JsonUtility.FromJson<ExpeditionProgressSave>(
                "{\"Version\":1,\"BestWave\":30,\"CompletedWaves\":30}"));
            Check(future.HardUnlocked, "Normal 30-clear entitlement boundary failed");
            Debug.Log("PASS: Normal progress Unity JSON, genuine v3 migration and Hard gate; " + assertions + " checks.");
        }

        public static void BuildAndroid()
        {
            Validate();
            ExpeditionStoryValidation.BuildAndroid();
        }
    }
}
