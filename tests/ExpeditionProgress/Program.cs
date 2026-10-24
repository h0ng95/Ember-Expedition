using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Expedition.Core;

internal static class ExpeditionProgressTests
{
    static CatalogDatabase catalog;
    static int checks;

    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }

    static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, message);
    }

    static void DifficultyAndSave()
    {
        var normal = ExpeditionRun.New(catalog, 123);
        var explicitNormal = ExpeditionRun.New(catalog, 123, ExpeditionDifficulty.Normal);
        Check(normal.Difficulty == ExpeditionDifficulty.Normal && normal.ContentWaves == 30 &&
            ExpeditionRun.ReleaseWaves == 30 && normal.Capture().Version == 8 &&
            normal.Capture().ContentWaves == 30 && normal.PreviousClearWaves == 0,
            "Default run remains current Normal content.");
        Check(normal.Capture().RandomState == explicitNormal.Capture().RandomState &&
            normal.Gold == explicitNormal.Gold &&
            normal.Summon() == explicitNormal.Summon() &&
            normal.Capture().RandomState == explicitNormal.Capture().RandomState,
            "Explicit Normal preserves default summon economy and RNG.");
        CatalogBattle a = normal.BeginBattle(), b = explicitNormal.BeginBattle();
        for (int i = 0; i < 120 && !a.Ended && !b.Ended; i++)
        { a.Step(.25f); b.Step(.25f); }
        Check(a.Time == b.Time && a.Ended == b.Ended && a.Winner == b.Winner &&
            a.Units.Count == b.Units.Count && a.Units.Zip(b.Units, (x, y) =>
                x.Id == y.Id && x.Hp == y.Hp && x.Shield == y.Shield &&
                x.X == y.X && x.Y == y.Y).All(same => same),
            "Default and explicit Normal have identical battle state.");
        Reject(() => ExpeditionRun.New(catalog, 1, ExpeditionDifficulty.Hard),
            "Hard cannot begin before its content exists.");
        Reject(() => ExpeditionRun.New(catalog, 1, (ExpeditionDifficulty)99),
            "Unknown difficulty cannot begin.");
        var save = ExpeditionRun.New(catalog, 1).Capture(); save.Difficulty = ExpeditionDifficulty.Hard;
        Reject(() => ExpeditionRun.Restore(catalog, save), "A current save cannot inject Hard.");
        save = ExpeditionRun.New(catalog, 1).Capture(); save.Difficulty = (ExpeditionDifficulty)99;
        Reject(() => ExpeditionRun.Restore(catalog, save), "Unknown saved difficulty is invalid.");
        save = ExpeditionRun.New(catalog, 1).Capture(); save.ContentWaves = 31;
        Reject(() => ExpeditionRun.Restore(catalog, save), "A current save cannot claim unreleased content.");
        save = ExpeditionRun.New(catalog, 1).Capture(); save.ContentWaves = 0;
        Reject(() => ExpeditionRun.Restore(catalog, save), "A v6 save must state its content length.");
        save = ExpeditionRun.New(catalog, 1).Capture(); save.PreviousClearWaves = 30;
        Reject(() => ExpeditionRun.Restore(catalog, save), "A v6 save cannot invent a prior thirty-wave clear.");
    }

    static void OldSaveMigration(string directory)
    {
        var json = new JavaScriptSerializer();
        string[] names = { "preparation", "battle", "result-win", "result-loss",
            "completed", "defeated" };
        ExpeditionPhase[] phases = { ExpeditionPhase.Preparation, ExpeditionPhase.Battle,
            ExpeditionPhase.Result, ExpeditionPhase.Result, ExpeditionPhase.Completed,
            ExpeditionPhase.Defeated };
        for (int version = 3; version <= 4; version++)
        for (int i = 0; i < names.Length; i++)
        {
            string source = File.ReadAllText(Path.Combine(directory, "v3-" + names[i] + ".json"));
            Check(!source.Contains("\"Difficulty\"") && !source.Contains("\"ContentWaves\""),
                "Genuine v3 fixture omits new fields: " + names[i]);
            ExpeditionRunSave old = json.Deserialize<ExpeditionRunSave>(source);
            Check(old.Version == 3, "Fixture has genuine old version.");
            if (version == 4)
            { old.Version = 4; old.ContentWaves = 15; old.Difficulty = ExpeditionDifficulty.Normal; }
            ExpeditionRun restored = ExpeditionRun.Restore(catalog, old);
            bool oldClear = names[i] == "completed";
            Check(restored.Phase == (oldClear ? ExpeditionPhase.Preparation : phases[i]) &&
                restored.Difficulty == ExpeditionDifficulty.Normal &&
                restored.ContentWaves == 30 && restored.Capture().Version == 8 &&
                restored.Wave == (oldClear ? 16 : old.Wave) &&
                restored.Gold == old.Gold + (oldClear ? 50 : 0) &&
                restored.PreviousClearWaves == (oldClear ? 15 : 0) &&
                old.Version == version && old.ContentWaves == (version == 3 ? 0 : 15),
                "Old save phase migrates as Normal: v" + version + " " + names[i]);
            ExpeditionRunSave snapshot = restored.Capture();
            Check(snapshot.RandomState == old.RandomState &&
                json.Serialize(snapshot.Roster) == json.Serialize(old.Roster) &&
                snapshot.DeployedIds.SequenceEqual(old.DeployedIds) &&
                snapshot.UndoHistory.Length == old.UndoHistory.Length &&
                snapshot.Victories == old.Victories && snapshot.RewardedWave == old.RewardedWave,
                "Migration preserves roster, RNG, deployment, undo and victory state.");
            ExpeditionRun reloaded = ExpeditionRun.Restore(catalog, snapshot);
            Check(reloaded.Gold == restored.Gold && reloaded.PreviousClearWaves == restored.PreviousClearWaves &&
                reloaded.Phase == restored.Phase, "v7 reload neither repeats nor loses migration credit.");
            if (phases[i] == ExpeditionPhase.Battle)
                Check(restored.ResumeBattle() != null, "Genuine v3 battle resumes.");
            if (oldClear)
            {
                var progress = ExpeditionProgress.New();
                Check(progress.Observe(reloaded) && progress.CompletedWaves == 15 &&
                    progress.BestWave == 15 && !progress.HardUnlocked && !progress.Observe(reloaded),
                    "Old fifteen-wave clear backfills the prior completion only once.");
            }
            if (names[i] == "result-loss" || names[i] == "defeated")
                Check(restored.Gold == old.Gold && restored.LastReward == 0,
                    "Loss or defeat receives no expansion grant.");
        }
        foreach (int version in new[] { 3, 4 })
        {
            var old = json.Deserialize<ExpeditionRunSave>(File.ReadAllText(
                Path.Combine(directory, "v3-completed.json")));
            old.Version = version;
            if (version == 4) { old.ContentWaves = 15; old.Difficulty = ExpeditionDifficulty.Normal; }
            old.Phase = ExpeditionPhase.Result;
            ExpeditionRun result = ExpeditionRun.Restore(catalog, old);
            Check(result.Phase == ExpeditionPhase.Result && result.Wave == 15 && result.Gold == 525 &&
                result.LastReward == 50 && result.PreviousClearWaves == 15,
                "Old final victory result gains passage reward without skipping Result.");
            var progress = ExpeditionProgress.New();
            Check(progress.Observe(result) && progress.CompletedWaves == 15 && !progress.HardUnlocked,
                "Old final Result immediately records the historical clear.");
            ExpeditionRun reloaded = ExpeditionRun.Restore(catalog, result.Capture());
            Check(reloaded.Gold == 525 && reloaded.Continue() && reloaded.Wave == 16 &&
                reloaded.Gold == 525 && reloaded.Phase == ExpeditionPhase.Preparation,
                "v7 Result continues to wave sixteen without another grant.");
            old.Gold++;
            Reject(() => ExpeditionRun.Restore(catalog, old), "Invalid old final budget cannot bypass migration.");
        }
        var invalid = json.Deserialize<ExpeditionRunSave>(File.ReadAllText(
            Path.Combine(directory, "v3-preparation.json")));
        invalid.Version = 4; invalid.ContentWaves = 20;
        Reject(() => ExpeditionRun.Restore(catalog, invalid), "v4 cannot claim twenty-wave content.");
        invalid.ContentWaves = 15; invalid.Difficulty = ExpeditionDifficulty.Hard;
        Reject(() => ExpeditionRun.Restore(catalog, invalid), "Old v4 Hard save is unavailable.");

        var undo = ExpeditionRun.New(catalog, 72);
        Check(undo.Move("ally-01", -2f, 0f) && undo.CanUndo, "Preparation fixture has real undo history.");
        var oldUndo = undo.Capture(); oldUndo.Version = 4; oldUndo.ContentWaves = 15;
        var migratedUndo = ExpeditionRun.Restore(catalog, oldUndo);
        Check(migratedUndo.CanUndo && migratedUndo.Capture().UndoHistory.Length == 1 &&
            migratedUndo.Capture().RandomState == oldUndo.RandomState &&
            migratedUndo.Undo() && migratedUndo.Roster.Find("ally-01").X == -1.3f,
            "v4 migration retains a usable undo snapshot and original placement.");
    }

    static ExpeditionRunSave Decode(string s) { return new JavaScriptSerializer().Deserialize<ExpeditionRunSave>(s); }
    // Framework reflection order can change after Restore accesses fields; compare JSON values.
    static string Encode(object s)
    {
        var json = new JavaScriptSerializer();
        return json.Serialize(Canonical(json.DeserializeObject(json.Serialize(s))));
    }
    static object Canonical(object value)
    {
        var map = value as System.Collections.Generic.IDictionary<string, object>;
        if (map != null)
        {
            var sorted = new System.Collections.Generic.SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (var item in map) sorted.Add(item.Key, Canonical(item.Value));
            return sorted;
        }
        var array = value as object[];
        return array == null ? value : array.Select(Canonical).ToArray();
    }
    static void RejectSave(CatalogDatabase catalog, ExpeditionRunSave save, string message)
    {
        bool rejected = false;
        try { ExpeditionRun.Restore(catalog, save); } catch (ArgumentException) { rejected = true; }
        Check(rejected, message);
    }
    static void TwentyWaveMigration(string directory)
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
            Check(now.Version == 8 && now.ContentWaves == 30 && now.Difficulty == ExpeditionDifficulty.Normal &&
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
            Check(Encode(old) == original, "Migration leaves supplied v5 snapshot unchanged: " + name);
            var reloaded = ExpeditionRun.Restore(catalog, Decode(Encode(now)));
            Check(Encode(reloaded.Capture()) == Encode(now), "v7 JSON is idempotent: " + name);
            var progress = ExpeditionProgress.New(); progress.Observe(reloaded);
            Check(progress.CompletedWaves == (clear ? 20 : prior) && progress.BestWave == old.Victories &&
                !progress.HardUnlocked && !progress.Observe(reloaded), "Historical clear remains distinct from Normal30.");
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

    static void TwentyFiveWaveMigration(string directory)
    {
        string[] names = { "preparation", "battle", "result-win", "result-loss", "completed", "defeated" };
        foreach (string name in names)
        foreach (int prior in new[] { 0, 15, 20 })
        {
            var old = Decode(File.ReadAllText(Path.Combine(directory, "v6-" + name + ".json")));
            Check(old.Version == 6 && old.ContentWaves == 25, "Fixture uses the committed v6 save shape.");
            old.PreviousClearWaves = prior;
            string original = Encode(old);
            bool clear = name == "result-win" || name == "completed";
            var restored = ExpeditionRun.Restore(catalog, old);
            var now = restored.Capture();
            Check(now.Version == 8 && now.ContentWaves == 30 && now.Difficulty == ExpeditionDifficulty.Normal &&
                now.Wave == (name == "completed" ? 26 : old.Wave) &&
                now.Phase == (name == "completed" ? ExpeditionPhase.Preparation : old.Phase) &&
                now.Gold == old.Gold + (clear ? 50 : 0) &&
                now.LastReward == (clear ? 50 : old.LastReward) &&
                now.PreviousClearWaves == (clear ? 25 : prior), "v6 phase and one-time passage reward: " + name);
            Check(now.RandomState == old.RandomState && now.NextMemberNumber == old.NextMemberNumber &&
                now.LastSummonedFamily == old.LastSummonedFamily && now.FamilyStreak == old.FamilyStreak &&
                now.Victories == old.Victories && now.RewardedWave == old.RewardedWave &&
                now.Lantern == old.Lantern && string.Join(",", now.DeployedIds) == string.Join(",", old.DeployedIds) &&
                Encode(now.Roster) == Encode(old.Roster) && now.UndoHistory.Length == old.UndoHistory.Length,
                "v6 migration preserves roster, RNG, economy history and deployment: " + name);
            Check(Encode(old) == original, "Migration leaves supplied v6 snapshot unchanged: " + name);
            var reloaded = ExpeditionRun.Restore(catalog, Decode(Encode(now)));
            Check(Encode(reloaded.Capture()) == Encode(now), "v7 JSON is idempotent: " + name);
            var progress = ExpeditionProgress.New(); progress.Observe(reloaded);
            Check(progress.CompletedWaves == (clear ? 25 : prior) && progress.BestWave == old.Victories &&
                !progress.HardUnlocked && !progress.Observe(reloaded), "Historical clear remains distinct from Normal30.");
            if (name == "result-win")
                Check(reloaded.Continue() && reloaded.Wave == 26 && reloaded.Gold == now.Gold,
                    "Winning result continues to 26 without a second grant.");
            if (name == "preparation")
                Check(reloaded.CanUndo && reloaded.Undo() && reloaded.Roster.Find("ally-01").X == -1.3f,
                    "v6 undo remains usable.");
            if (name == "battle") Check(reloaded.ResumeBattle() != null, "v6 interrupted battle resumes.");
            old.Gold++;
            RejectSave(catalog, old, "Invalid old economy is rejected before bonus.");
        }
        var invalid = Decode(File.ReadAllText(Path.Combine(directory, "v6-completed.json")));
        invalid.ContentWaves = 30; RejectSave(catalog, invalid, "v6 cannot claim new content length.");
        invalid.ContentWaves = 25; invalid.PreviousClearWaves = 25;
        RejectSave(catalog, invalid, "v6 cannot inject an unrecorded prior twenty clear.");
        invalid.PreviousClearWaves = 0; invalid.Difficulty = ExpeditionDifficulty.Hard;
        RejectSave(catalog, invalid, "Old Hard content stays unavailable.");
        var early = ExpeditionRun.New(catalog, 62); early.Summon(); early.Move("ally-01", -2f, 0);
        var earlySave = early.Capture(); earlySave.Version = 6; earlySave.ContentWaves = 25;
        var earlyReload = ExpeditionRun.Restore(catalog, earlySave);
        Check(earlyReload.Wave == 1 && earlyReload.Gold == 20 && earlyReload.CanUndo &&
            earlyReload.PreviousClearWaves == 0, "An early v6 save receives no completion bonus.");
        var invented = earlyReload.Capture(); invented.PreviousClearWaves = 25;
        RejectSave(catalog, invented, "A prior clear cannot exceed victories.");
    }

    static void ProgressRules()
    {
        var progress = ExpeditionProgress.New();
        Check(progress.BestWave == 0 && progress.CompletedWaves == 0 && !progress.HardUnlocked,
            "New progress is empty and locked.");
        var run = ExpeditionRun.New(catalog, 9);
        Check(!progress.Observe(run), "Starting a run records no victory.");
        run.BeginBattle(); run.ResolveBattle(true);
        Check(progress.Observe(run) && progress.BestWave == 1 && !progress.Observe(run),
            "Victory Result records exactly once before Continue.");
        run.Continue();
        Check(!progress.Observe(run), "Continuing the victory is idempotent.");
        run.BeginBattle(); run.ResolveBattle(false); run.Continue();
        Check(!progress.Observe(run) && progress.BestWave == 1,
            "Loss preserves the best defeated wave.");
        Check(!progress.Observe(ExpeditionRun.New(catalog, 10)) && progress.BestWave == 1,
            "Restart does not reset account progress.");
        var complete = ExpeditionRun.New(catalog, 11);
        for (int wave = 1; wave <= ExpeditionRun.TotalWaves; wave++)
        {
            complete.BeginBattle(); complete.ResolveBattle(true);
            if (wave == 15)
                Check(progress.Observe(complete) && progress.BestWave == 15 &&
                    progress.CompletedWaves == 0 && !progress.HardUnlocked,
                    "Current fifteen-wave Result records progress but not a twenty-wave clear.");
            if (wave == 20)
                Check(progress.Observe(complete) && progress.BestWave == 20 &&
                    progress.CompletedWaves == 0 && !progress.HardUnlocked,
                    "Current wave twenty records passage, not completion.");
            if (wave == 25)
                Check(progress.Observe(complete) && progress.BestWave == 25 && progress.CompletedWaves == 0 &&
                    !progress.HardUnlocked, "Wave25 is passage, not Normal30 completion.");
            if (wave == 30)
                Check(progress.Observe(complete) && progress.CompletedWaves == 30 &&
                    progress.HardUnlocked, "Normal30 Result records completion and future Hard entitlement.");
            complete.Continue();
        }
        Check(complete.Phase == ExpeditionPhase.Completed && !progress.Observe(complete),
            "Completed phase repeats no account write.");
        ExpeditionProgressSave saved = progress.Capture();
        var reloaded = ExpeditionProgress.Restore(saved);
        saved.BestWave = saved.CompletedWaves = 0;
        Check(reloaded.BestWave == 30 && reloaded.CompletedWaves == 30 &&
            reloaded.Capture().BestWave == 30,
            "Captured progress reloads independently of supplied data.");
    }

    static void ProgressValidation()
    {
        Reject(() => ExpeditionProgress.Restore(null), "Missing progress is invalid.");
        var save = ExpeditionProgress.New().Capture(); save.Version = 3;
        Reject(() => ExpeditionProgress.Restore(save), "Unknown progress version is invalid.");
        save = ExpeditionProgress.New().Capture(); save.BestWave = -1;
        Reject(() => ExpeditionProgress.Restore(save), "Negative best wave is invalid.");
        save = ExpeditionProgress.New().Capture(); save.BestWave = 31;
        Reject(() => ExpeditionProgress.Restore(save), "Best wave beyond release is invalid.");
        save = ExpeditionProgress.New().Capture(); save.BestWave = 15; save.CompletedWaves = 14;
        Reject(() => ExpeditionProgress.Restore(save), "Impossible short clear is invalid.");
        save = ExpeditionProgress.New().Capture(); save.BestWave = 14; save.CompletedWaves = 15;
        Reject(() => ExpeditionProgress.Restore(save), "Completion cannot exceed best victory.");
        save = ExpeditionProgress.New().Capture(); save.BestWave = save.CompletedWaves = 20;
        var future = ExpeditionProgress.Restore(save);
        Check(future.CompletedWaves == 20 && !future.HardUnlocked,
            "Future twenty-wave clear remains distinct and locked.");
        save.BestWave = save.CompletedWaves = 30;
        future = ExpeditionProgress.Restore(save);
        Check(future.CompletedWaves == 30 && future.HardUnlocked,
            "Future Normal release clear grants Hard entitlement.");
        save.BestWave = save.CompletedWaves = 15;
        Check(ExpeditionProgress.Restore(save).CompletedWaves == 15,
            "Stable minimum preserves old fifteen-wave clears after expansion.");
    }

    static void LegacyResearchRules()
    {
        var json = new JavaScriptSerializer();
        var account = ExpeditionProgress.New();
        Check(account.ResearchPoints == 0 && !account.PurchaseResearch("ember-start") &&
            !account.PurchaseResearch(null) && !account.PurchaseResearch("bad"),
            "Research cannot be bought without earned points or a known ID.");
        var run = ExpeditionRun.New(catalog, 32);
        for (int wave = 1; wave <= 5; wave++)
        {
            run.BeginBattle(); run.ResolveBattle(true);
            Check(account.Observe(run) && account.ResearchPoints == wave + wave / 5,
                "First-clear reward includes a boss point.");
            Check(!account.Observe(run), "Result reload cannot award twice.");
            run.Continue();
            Check(!account.Observe(run), "Continue does not repeat research reward.");
        }
        Check(!account.PurchaseResearch("ember-insight") && account.ResearchPoints == 6,
            "Child requires its parent even with enough points.");
        Check(account.PurchaseResearch("ember-start") && account.ResearchPoints == 3 &&
            !account.PurchaseResearch("ember-start"), "Research purchase charges exactly once.");
        Check(run.LegacyResearchIds.Count == 0 && run.StartFormationId == "standard",
            "Buying account research does not change a running expedition.");
        var captured = account.Capture();
        var reloaded = ExpeditionProgress.Restore(json.Deserialize<ExpeditionProgressSave>(json.Serialize(captured)));
        captured.ResearchIds[0] = "bad";
        Check(reloaded.ResearchPoints == 3 && reloaded.ResearchIds.Single() == "ember-start",
            "Owned nodes and earned balance survive JSON independently of caller arrays.");
        var next = ExpeditionRun.New(catalog, 33, ExpeditionDifficulty.Normal, reloaded.ResearchIds, "ember");
        Check(next.Roster.Find("ally-02").FormId == "B-FI" && next.TacticIds.Count == 0 &&
            next.LegacyResearchIds.Single() == "ember-start", "New run uses unlocked formation and no tactics.");
        Check(!reloaded.Observe(next) && reloaded.ResearchPoints == 3,
            "Restart cannot reset or duplicate permanent rewards.");
        for (int wave = 1; wave <= 5; wave++)
        { next.BeginBattle(); next.ResolveBattle(true); Check(!reloaded.Observe(next), "Repeated clear is not farmable."); next.Continue(); }
        next.BeginBattle(); next.ResolveBattle(false);
        Check(!reloaded.Observe(next) && reloaded.ResearchPoints == 3, "Loss preserves earned research.");
        Reject(() => ExpeditionRun.New(catalog, 1, ExpeditionDifficulty.Normal, null, "ember"),
            "Locked formation cannot start.");
        Reject(() => ExpeditionRun.New(catalog, 1, ExpeditionDifficulty.Normal, null, "bad"),
            "Unknown formation cannot start.");

        var old = new ExpeditionProgressSave { Version = 1, BestWave = 30, CompletedWaves = 30 };
        var migrated = ExpeditionProgress.Restore(old);
        Check(migrated.ResearchPoints == 36 && migrated.ResearchIds.Count == 0 && migrated.HardUnlocked,
            "Existing progress backfills first-clear points and keeps entitlement.");
        foreach (var node in CatalogLegacyResearch.Nodes)
            Check(migrated.PurchaseResearch(node.Id), "Every permanent node can be earned within Normal.");
        Check(migrated.ResearchPoints == 9 && migrated.ResearchIds.Count == 6,
            "All six nodes cost 27 points, not an unbounded stat ladder.");
        var saved = migrated.Capture();
        Check(ExpeditionProgress.Restore(json.Deserialize<ExpeditionProgressSave>(json.Serialize(saved))).ResearchPoints == 9,
            "Fully researched account reload has exact balance.");
        foreach (string formation in new[] { "standard", "tactical", "ember", "frost" })
        {
            var formed = ExpeditionRun.New(catalog, 66, ExpeditionDifficulty.Normal, migrated.ResearchIds, formation);
            Check(formed.Roster.Members.Select(x => x.FormId).SequenceEqual(CatalogLegacyResearch.GetFormationUnits(formation)) &&
                formed.Gold == 40 && formed.DeployedIds.Count == 3, "Unlocked formation keeps budget and unit count.");
            var roundtrip = ExpeditionRun.Restore(catalog, json.Deserialize<ExpeditionRunSave>(json.Serialize(formed.Capture())));
            Check(roundtrip.StartFormationId == formation && roundtrip.LegacyResearchIds.Count == 6,
                "Formation and immutable research snapshot persist.");
        }
        old.ResearchIds = new[] { "ember-start" };
        Reject(() => ExpeditionProgress.Restore(old), "v1 cannot smuggle new research fields.");
        foreach (string[] ids in new[] { new[] { "unknown" }, new[] { "ember-start", "ember-start" },
            new[] { "ember-insight" }, new string[] { null } })
        {
            saved = migrated.Capture(); saved.ResearchIds = ids;
            Reject(() => ExpeditionProgress.Restore(saved), "Unknown, duplicate and orphan research rejected.");
        }
        saved = migrated.Capture(); saved.ResearchIds = null;
        Reject(() => ExpeditionProgress.Restore(saved), "Current account requires research array.");
        saved = migrated.Capture(); saved.BestWave = 1; saved.CompletedWaves = 0;
        Reject(() => ExpeditionProgress.Restore(saved), "Overspent research save rejected.");
        var formations = CatalogLegacyResearch.GetFormationUnits("standard"); formations[0] = "bad";
        Check(CatalogLegacyResearch.GetFormationUnits("standard")[0] == "B-KN", "Formation catalog cannot be mutated.");
    }

    static void V7Migration(string directory)
    {
        foreach (string name in new[] { "preparation", "battle", "result-win", "result-loss", "completed", "defeated" })
        {
            string source = File.ReadAllText(Path.Combine(directory, "v7-" + name + ".json"));
            Check(!source.Contains("TacticIds") && !source.Contains("LegacyResearchIds"), "Genuine v7 fixture lacks research.");
            var old = Decode(source);
            var current = ExpeditionRun.Restore(catalog, old);
            Check(old.Version == 7 && current.Capture().Version == 8 && current.Gold == old.Gold &&
                current.Wave == old.Wave && current.Phase == old.Phase && current.TacticIds.Count == 0 &&
                current.LegacyResearchIds.Count == 0 && current.StartFormationId == "standard" &&
                current.Capture().RandomState == old.RandomState && Encode(current.Capture().Roster) == Encode(old.Roster),
                "v7 preserves economy, phase, RNG and party without expansion grants.");
            string encoded = Encode(current.Capture());
            Check(Encode(ExpeditionRun.Restore(catalog, Decode(encoded)).Capture()) == encoded, "v8 save is idempotent.");
            if (name == "preparation") Check(current.Undo(), "v7 undo survives research migration.");
            if (name == "battle") Check(current.ResumeBattle() != null, "v7 battle resumes.");
        }
    }

    static void Main(string[] args)
    {
        try
        {
            catalog = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
            DifficultyAndSave(); OldSaveMigration(args[1]); TwentyWaveMigration(args[1]); TwentyFiveWaveMigration(args[1]); ProgressRules(); ProgressValidation();
            V7Migration(args[1]); LegacyResearchRules();
            Console.WriteLine("Expedition progress: " + checks + " checks passed.");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.GetType().FullName + ": " + e.Message);
            Environment.ExitCode = 1;
        }
    }
}
