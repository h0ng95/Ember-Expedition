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
            string completedJson = File.ReadAllText(Path.Combine(fixtures, "v3-completed.json"));
            for (int version = 3; version <= 4; version++)
            foreach (string name in names)
            {
                string json = File.ReadAllText(Path.Combine(fixtures, "v3-" + name + ".json"));
                Check(!json.Contains("\"Difficulty\"") && !json.Contains("\"ContentWaves\""),
                    name + ": fixture is not a genuine v3 shape");
                var old = JsonUtility.FromJson<ExpeditionRunSave>(json);
                Check(old.Version == 3, name + ": source fixture version changed");
                if (version == 4)
                { old.Version = 4; old.ContentWaves = 15; old.Difficulty = ExpeditionDifficulty.Normal; }
                var run = ExpeditionRun.Restore(catalog, old);
                bool cleared = name == "completed";
                Check(run.Difficulty == ExpeditionDifficulty.Normal &&
                    run.ContentWaves == 20 && run.Gold == old.Gold + (cleared ? 50 : 0) &&
                    run.Wave == (cleared ? 16 : old.Wave) &&
                    run.Phase == (cleared ? ExpeditionPhase.Preparation : old.Phase) &&
                    run.PreviousClearWaves == (cleared ? 15 : 0) && run.Victories == old.Victories &&
                    run.Capture().RandomState == old.RandomState &&
                    JsonUtility.ToJson(run.Capture().Roster) == JsonUtility.ToJson(old.Roster) &&
                    string.Join(",", run.Capture().DeployedIds) == string.Join(",", old.DeployedIds) &&
                    run.Capture().UndoHistory.Length == old.UndoHistory.Length,
                    "v" + version + " " + name + ": migration changed unrelated state");
                string migratedJson = JsonUtility.ToJson(run.Capture());
                var restored = ExpeditionRun.Restore(catalog, JsonUtility.FromJson<ExpeditionRunSave>(migratedJson));
                Check(restored.Capture().Version == 5 && JsonUtility.ToJson(restored.Capture()) == migratedJson,
                    "v" + version + " " + name + ": v5 JSON did not round-trip");
                var progress = ExpeditionProgress.New();
                progress.Observe(restored);
                string progressJson = JsonUtility.ToJson(progress.Capture());
                var reloaded = ExpeditionProgress.Restore(JsonUtility.FromJson<ExpeditionProgressSave>(progressJson));
                Check(reloaded.BestWave == old.Victories &&
                    reloaded.CompletedWaves == (cleared ? 15 : 0) &&
                    !reloaded.HardUnlocked && !reloaded.Observe(restored) &&
                    JsonUtility.ToJson(reloaded.Capture()) == progressJson,
                    "v" + version + " " + name + ": independent record changed or unlocked Hard early");
                if (name == "battle") Check(restored.ResumeBattle() != null,
                    "v" + version + ": interrupted battle did not resume");
                if (name == "result-loss" || name == "defeated")
                    Check(restored.Gold == old.Gold && restored.LastReward == 0,
                        "v" + version + ": loss received a passage reward");
            }
            for (int version = 3; version <= 4; version++)
            {
                var oldResult = JsonUtility.FromJson<ExpeditionRunSave>(completedJson);
                oldResult.Version = version;
                if (version == 4)
                { oldResult.ContentWaves = 15; oldResult.Difficulty = ExpeditionDifficulty.Normal; }
                oldResult.Phase = ExpeditionPhase.Result;
                var result = ExpeditionRun.Restore(catalog, oldResult);
                Check(result.Phase == ExpeditionPhase.Result && result.Wave == 15 &&
                    result.Gold == 525 && result.LastReward == 50 && result.PreviousClearWaves == 15,
                    "v" + version + ": final Result did not gain exactly fifty gold");
                var progress = ExpeditionProgress.New();
                Check(progress.Observe(result) && progress.CompletedWaves == 15 &&
                    !progress.HardUnlocked, "v" + version + ": historical clear was not recorded");
                var reload = ExpeditionRun.Restore(catalog, JsonUtility.FromJson<ExpeditionRunSave>(
                    JsonUtility.ToJson(result.Capture())));
                Check(reload.Gold == 525 && reload.Continue() && reload.Wave == 16 &&
                    reload.Gold == 525, "v" + version + ": passage reward duplicated");
                oldResult.Gold++;
                bool rejectedBudget = false;
                try { ExpeditionRun.Restore(catalog, oldResult); }
                catch (ArgumentException) { rejectedBudget = true; }
                Check(rejectedBudget, "v" + version + ": invalid old budget bypassed validation");
            }
            var normal = ExpeditionRun.New(catalog, 27);
            var blocked = normal.Capture(); blocked.Difficulty = ExpeditionDifficulty.Hard;
            bool rejected = false;
            try { ExpeditionRun.Restore(catalog, JsonUtility.FromJson<ExpeditionRunSave>(JsonUtility.ToJson(blocked))); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "unfinished Hard save was playable");
            blocked = normal.Capture(); blocked.PreviousClearWaves = 30; rejected = false;
            try { ExpeditionRun.Restore(catalog, blocked); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "v5 save invented a thirty-wave clear");
            var oldV4 = JsonUtility.FromJson<ExpeditionRunSave>(File.ReadAllText(
                Path.Combine(fixtures, "v3-preparation.json")));
            oldV4.Version = 4; oldV4.ContentWaves = 20; rejected = false;
            try { ExpeditionRun.Restore(catalog, oldV4); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "v4 save invented twenty-wave content");
            oldV4.ContentWaves = 15; oldV4.Difficulty = ExpeditionDifficulty.Hard; rejected = false;
            try { ExpeditionRun.Restore(catalog, oldV4); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "old v4 Hard save was playable");
            var current = ExpeditionRun.New(catalog, 27);
            var account = ExpeditionProgress.New();
            for (int wave = 1; wave <= ExpeditionRun.TotalWaves; wave++)
            {
                current.BeginBattle(); current.ResolveBattle(true);
                account.Observe(current);
                current.Continue();
            }
            Check(current.Phase == ExpeditionPhase.Completed && account.BestWave == 20 &&
                account.CompletedWaves == 20 && !account.HardUnlocked,
                "current Normal completion must not unlock future Hard");
            // Synthetic future record tests the 30-clear boundary; it is not current playable content.
            var future = ExpeditionProgress.Restore(JsonUtility.FromJson<ExpeditionProgressSave>(
                "{\"Version\":1,\"BestWave\":30,\"CompletedWaves\":30}"));
            Check(future.HardUnlocked, "Normal 30-clear entitlement boundary failed");
            Debug.Log("PASS: Normal progress Unity JSON, v3/v4 to v5 migration and Hard gate; " + assertions + " checks.");
        }

        public static void BuildAndroid()
        {
            Validate();
            ExpeditionStoryValidation.BuildAndroid();
        }
    }
}
