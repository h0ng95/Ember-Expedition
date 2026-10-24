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
                    run.ContentWaves == 25 && run.Gold == old.Gold + (cleared ? 50 : 0) &&
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
                Check(restored.Capture().Version == 6 && JsonUtility.ToJson(restored.Capture()) == migratedJson,
                    "v" + version + " " + name + ": v6 JSON did not round-trip");
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
            ValidateTwentyWaveMigration(catalog, fixtures);
            var normal = ExpeditionRun.New(catalog, 27);
            var blocked = normal.Capture(); blocked.Difficulty = ExpeditionDifficulty.Hard;
            bool rejected = false;
            try { ExpeditionRun.Restore(catalog, JsonUtility.FromJson<ExpeditionRunSave>(JsonUtility.ToJson(blocked))); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "unfinished Hard save was playable");
            blocked = normal.Capture(); blocked.PreviousClearWaves = 30; rejected = false;
            try { ExpeditionRun.Restore(catalog, blocked); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "v6 save invented a thirty-wave clear");
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
            Check(current.Phase == ExpeditionPhase.Completed && account.BestWave == 25 &&
                account.CompletedWaves == 25 && !account.HardUnlocked,
                "current Normal completion must not unlock future Hard");
            // Synthetic future record tests the 30-clear boundary; it is not current playable content.
            var future = ExpeditionProgress.Restore(JsonUtility.FromJson<ExpeditionProgressSave>(
                "{\"Version\":1,\"BestWave\":30,\"CompletedWaves\":30}"));
            Check(future.HardUnlocked, "Normal 30-clear entitlement boundary failed");
            Debug.Log("PASS: Normal progress Unity JSON, v3/v4/v5 to v6 migration and Hard gate; " + assertions + " checks.");
        }

        static ExpeditionRunSave Decode(string s) { return JsonUtility.FromJson<ExpeditionRunSave>(s); }
        static string Encode(object s) { return JsonUtility.ToJson(s); }
    static void RejectSave(CatalogDatabase catalog, ExpeditionRunSave save, string message)
    {
        bool rejected = false;
        try { ExpeditionRun.Restore(catalog, save); } catch (ArgumentException) { rejected = true; }
        Check(rejected, message);
    }
    static void ValidateTwentyWaveMigration(CatalogDatabase catalog, string directory)
    {
        string[] names = { "preparation", "battle", "result-win", "result-loss", "completed", "defeated" };
        foreach (string name in names)
        foreach (int prior in new[] { 0, 15 })
        {
            var old = Decode(File.ReadAllText(Path.Combine(directory, "v5-" + name + ".json")));
            Check(old.Version == 5 && old.ContentWaves == 20, "Fixture uses the committed v5 save shape.");
            old.PreviousClearWaves = prior;
            string original = Encode(old);
            bool clear = name == "result-win" || name == "completed";
            var restored = ExpeditionRun.Restore(catalog, old);
            var now = restored.Capture();
            Check(now.Version == 6 && now.ContentWaves == 25 && now.Difficulty == ExpeditionDifficulty.Normal &&
                now.Wave == (name == "completed" ? 21 : old.Wave) &&
                now.Phase == (name == "completed" ? ExpeditionPhase.Preparation : old.Phase) &&
                now.Gold == old.Gold + (clear ? 50 : 0) &&
                now.LastReward == (clear ? 50 : old.LastReward) &&
                now.PreviousClearWaves == (clear ? 20 : prior), "v5 phase and one-time passage reward: " + name);
            Check(now.RandomState == old.RandomState && now.NextMemberNumber == old.NextMemberNumber &&
                now.LastSummonedFamily == old.LastSummonedFamily && now.FamilyStreak == old.FamilyStreak &&
                now.Victories == old.Victories && now.RewardedWave == old.RewardedWave &&
                now.Lantern == old.Lantern && string.Join(",", now.DeployedIds) == string.Join(",", old.DeployedIds) &&
                Encode(now.Roster) == Encode(old.Roster) && now.UndoHistory.Length == old.UndoHistory.Length,
                "v5 migration preserves roster, RNG, economy history and deployment: " + name);
            Check(Encode(old) == original, "Migration leaves supplied v5 snapshot unchanged.");
            var reloaded = ExpeditionRun.Restore(catalog, Decode(Encode(now)));
            Check(Encode(reloaded.Capture()) == Encode(now), "v6 JSON is idempotent: " + name);
            var progress = ExpeditionProgress.New(); progress.Observe(reloaded);
            Check(progress.CompletedWaves == (clear ? 20 : prior) && progress.BestWave == old.Victories &&
                !progress.HardUnlocked && !progress.Observe(reloaded), "Historical clear remains distinct from Normal25.");
            if (name == "result-win")
                Check(reloaded.Continue() && reloaded.Wave == 21 && reloaded.Gold == now.Gold,
                    "Winning result continues to 21 without a second grant.");
            if (name == "preparation")
                Check(reloaded.CanUndo && reloaded.Undo() && reloaded.Roster.Find("ally-01").X == -1.3f,
                    "v5 undo remains usable.");
            if (name == "battle") Check(reloaded.ResumeBattle() != null, "v5 interrupted battle resumes.");
            old.Gold++;
            RejectSave(catalog, old, "Invalid old economy is rejected before bonus.");
        }
        var invalid = Decode(File.ReadAllText(Path.Combine(directory, "v5-completed.json")));
        invalid.ContentWaves = 25; RejectSave(catalog, invalid, "v5 cannot claim new content length.");
        invalid.ContentWaves = 20; invalid.PreviousClearWaves = 20;
        RejectSave(catalog, invalid, "v5 cannot inject an unrecorded prior twenty clear.");
        invalid.PreviousClearWaves = 0; invalid.Difficulty = ExpeditionDifficulty.Hard;
        RejectSave(catalog, invalid, "Old Hard content stays unavailable.");
        var early = ExpeditionRun.New(catalog, 62); early.Summon(); early.Move("ally-01", -2f, 0);
        var earlySave = early.Capture(); earlySave.Version = 5; earlySave.ContentWaves = 20;
        var earlyReload = ExpeditionRun.Restore(catalog, earlySave);
        Check(earlyReload.Wave == 1 && earlyReload.Gold == 20 && earlyReload.CanUndo &&
            earlyReload.PreviousClearWaves == 0, "An early v5 save receives no completion bonus.");
        var invented = earlyReload.Capture(); invented.PreviousClearWaves = 20;
        RejectSave(catalog, invented, "A prior clear cannot exceed victories.");
    }

        public static void BuildAndroid()
        {
            Validate();
            ExpeditionStoryValidation.BuildAndroid();
        }
    }
}
