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
        Check(normal.Difficulty == ExpeditionDifficulty.Normal && normal.ContentWaves == 15 &&
            ExpeditionRun.ReleaseWaves == 30 && normal.Capture().Version == 4 &&
            normal.Capture().ContentWaves == 15,
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
        save = ExpeditionRun.New(catalog, 1).Capture(); save.ContentWaves = 30;
        Reject(() => ExpeditionRun.Restore(catalog, save), "A current save cannot claim unreleased content.");
        save = ExpeditionRun.New(catalog, 1).Capture(); save.ContentWaves = 0;
        Reject(() => ExpeditionRun.Restore(catalog, save), "A v4 save must state its content length.");
    }

    static void GenuineV3Files(string directory)
    {
        var json = new JavaScriptSerializer();
        string[] names = { "preparation", "battle", "result-win", "result-loss",
            "completed", "defeated" };
        ExpeditionPhase[] phases = { ExpeditionPhase.Preparation, ExpeditionPhase.Battle,
            ExpeditionPhase.Result, ExpeditionPhase.Result, ExpeditionPhase.Completed,
            ExpeditionPhase.Defeated };
        for (int i = 0; i < names.Length; i++)
        {
            string source = File.ReadAllText(Path.Combine(directory, "v3-" + names[i] + ".json"));
            Check(!source.Contains("\"Difficulty\"") && !source.Contains("\"ContentWaves\""),
                "Genuine v3 fixture omits new fields: " + names[i]);
            ExpeditionRunSave old = json.Deserialize<ExpeditionRunSave>(source);
            Check(old.Version == 3, "Fixture has genuine old version.");
            ExpeditionRun restored = ExpeditionRun.Restore(catalog, old);
            Check(restored.Phase == phases[i] &&
                restored.Difficulty == ExpeditionDifficulty.Normal &&
                restored.ContentWaves == 15 && restored.Capture().Version == 4 &&
                old.Version == 3 && old.ContentWaves == 0,
                "Genuine v3 phase migrates as Normal: " + names[i]);
            if (phases[i] == ExpeditionPhase.Battle)
                Check(restored.ResumeBattle() != null, "Genuine v3 battle resumes.");
            if (phases[i] == ExpeditionPhase.Completed)
            {
                var progress = ExpeditionProgress.New();
                Check(progress.Observe(restored) && progress.CompletedWaves == 15 &&
                    progress.BestWave == 15 && !progress.HardUnlocked,
                    "Genuine v3 clear backfills fifteen, without Hard unlock.");
            }
        }
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
                    progress.CompletedWaves == 15 && !progress.HardUnlocked,
                    "Result victory records current clear without Hard unlock.");
            complete.Continue();
        }
        Check(complete.Phase == ExpeditionPhase.Completed && !progress.Observe(complete),
            "Completed phase repeats no account write.");
        ExpeditionProgressSave saved = progress.Capture();
        var reloaded = ExpeditionProgress.Restore(saved);
        saved.BestWave = saved.CompletedWaves = 0;
        Check(reloaded.BestWave == 15 && reloaded.CompletedWaves == 15 &&
            reloaded.Capture().BestWave == 15,
            "Captured progress reloads independently of supplied data.");
    }

    static void ProgressValidation()
    {
        Reject(() => ExpeditionProgress.Restore(null), "Missing progress is invalid.");
        var save = ExpeditionProgress.New().Capture(); save.Version = 2;
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

    static void Main(string[] args)
    {
        try
        {
            catalog = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
            DifficultyAndSave(); GenuineV3Files(args[1]); ProgressRules(); ProgressValidation();
            Console.WriteLine("Expedition progress: " + checks + " checks passed.");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.GetType().FullName + ": " + e.Message);
            Environment.ExitCode = 1;
        }
    }
}
