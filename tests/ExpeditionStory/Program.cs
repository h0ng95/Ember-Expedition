using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Expedition.Core;

internal static class Program
{
    static int checks;
    static CatalogDatabase catalog;
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }

    static ExpeditionRun WinThrough(int wave)
    {
        var run = ExpeditionRun.New(catalog, 7);
        for (int i = 1; i <= wave; i++)
        { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
        return run;
    }

    static void CatalogAndMapping()
    {
        string[] ids = { "intro", "forest", "forest-clear", "castle", "castle-clear", "mine", "mine-clear" };
        Check(ExpeditionStory.Entries.Select(e => e.Id).SequenceEqual(ids), "Seven story records keep their canonical order.");
        Check(ExpeditionStory.Entries.All(e => !string.IsNullOrEmpty(e.Title) && !string.IsNullOrEmpty(e.Body) &&
            ExpeditionStory.Get(e.Id) == e), "Every record has a stable title and body.");
        Check(ExpeditionStory.Get("missing") == null && ExpeditionStory.Get(null) == null,
            "Unknown records have no content.");
        Check(ExpeditionStory.ForWave(1).Id == "forest" && ExpeditionStory.ForWave(5).Id == "forest" &&
            ExpeditionStory.ForWave(6).Id == "castle" && ExpeditionStory.ForWave(10).Id == "castle" &&
            ExpeditionStory.ForWave(11).Id == "mine" && ExpeditionStory.ForWave(15).Id == "mine" &&
            ExpeditionStory.ForWave(0) == null && ExpeditionStory.ForWave(16) == null,
            "Regions follow the current fifteen-wave boundaries.");
        Check(ExpeditionStory.BossClear(5).Id == "forest-clear" &&
            ExpeditionStory.BossClear(10).Id == "castle-clear" &&
            ExpeditionStory.BossClear(15).Id == "mine-clear" &&
            ExpeditionStory.BossClear(14) == null, "Only defeated regional bosses have clear records.");
        Check(new[] { "forest", "castle", "mine" }.All(id =>
            ExpeditionStory.Get(id).Body.Count(c => c == '.') == 2),
            "Preparation region excerpts contain two sentences each.");
        Check(new[] { "forest-clear", "castle-clear", "mine-clear" }.All(id =>
            ExpeditionStory.Get(id).Body.Count(c => c == '\n') == 1),
            "Boss clear records fit their two-line result excerpt.");
    }

    static void UnlocksAndPersistence()
    {
        var journal = ExpeditionJournal.New();
        Check(journal.UnlockedCount == 1 && journal.IsUnlocked("intro") &&
            !journal.IsUnlocked("forest") && !journal.IsUnlocked("mine-clear"),
            "Only the opening account is available before an expedition.");
        var run = ExpeditionRun.New(catalog, 7);
        string beforeRun = Json.Serialize(run.Capture());
        Check(journal.Observe(run) && journal.IsUnlocked("forest") && journal.UnlockedCount == 2 &&
            !journal.Observe(run) && Json.Serialize(run.Capture()) == beforeRun,
            "First observation unlocks forest once without changing the run.");
        run.BeginBattle(); run.ResolveBattle(false); run.Continue();
        Check(!journal.Observe(run) && !journal.IsUnlocked("forest-clear") &&
            !journal.IsUnlocked("castle"), "Loss and lantern retry cannot open a clear record.");
        var five = WinThrough(5);
        Check(journal.Observe(five) && journal.IsUnlocked("forest-clear") &&
            journal.IsUnlocked("castle") && journal.UnlockedCount == 4,
            "Five victories reveal the first clear and next region.");
        var saved = journal.Capture();
        string[] callerArray = saved.UnlockedIds;
        var restored = ExpeditionJournal.Restore(saved);
        callerArray[0] = "mine-clear";
        Check(restored.IsUnlocked("intro") && !restored.IsUnlocked("mine-clear") &&
            restored.UnlockedCount == 4, "Restored journal copies the supplied array.");
        var ten = WinThrough(10);
        string beforeTen = Json.Serialize(ten.Capture());
        Check(restored.Observe(ten) && restored.IsUnlocked("castle-clear") && restored.IsUnlocked("mine") &&
            !restored.IsUnlocked("mine-clear") && Json.Serialize(ten.Capture()) == beforeTen,
            "Ten victories backfill castle and mine without touching roster, money, or RNG.");
        var restartedRun = ExpeditionRun.New(catalog, 91);
        Check(!restored.Observe(restartedRun) && restored.IsUnlocked("mine") &&
            restored.UnlockedCount == 6, "A new run retains previously discovered records.");
        var fifteen = WinThrough(15);
        Check(restored.Observe(fifteen) && restored.IsUnlocked("mine-clear") &&
            restored.UnlockedCount == 7 && !restored.Observe(fifteen),
            "The final regional clear unlocks once and does not imply a central-beacon ending.");
        Check(ExpeditionJournal.Restore(restored.Capture()).UnlockedCount == 7,
            "Independent journal capture survives restart.");
    }

    static void LegacyAndInvalidData()
    {
        var ten = WinThrough(10);
        ExpeditionRunSave old = ten.Capture();
        old.Version = 2; old.Wave = 10; old.Phase = ExpeditionPhase.Completed;
        old.Gold -= 50; old.LastReward = 0;
        ExpeditionRun migrated = ExpeditionRun.Restore(catalog, old);
        var journal = ExpeditionJournal.New();
        Check(migrated.Phase == ExpeditionPhase.Preparation && migrated.Wave == 11 &&
            journal.Observe(migrated) && journal.UnlockedCount == 6 &&
            !journal.IsUnlocked("mine-clear") && old.Version == 2 && old.Gold == 290,
            "A genuine old completed-ten save backfills earned chapters without mutating its input.");
        var failed = WinThrough(9);
        failed.BeginBattle(); failed.ResolveBattle(false); failed.Continue();
        var lossJournal = ExpeditionJournal.New();
        Check(failed.Wave == 10 && failed.Victories == 9 && lossJournal.Observe(failed) &&
            lossJournal.IsUnlocked("forest-clear") && lossJournal.IsUnlocked("castle") &&
            !lossJournal.IsUnlocked("castle-clear") && !lossJournal.IsUnlocked("mine"),
            "A lost tenth fight does not count as a boss clear.");
        var result = WinThrough(9);
        result.BeginBattle(); result.ResolveBattle(true);
        ExpeditionRunSave oldResult = result.Capture(); oldResult.Version = 2;
        oldResult.Gold -= 50; oldResult.LastReward = 0;
        var restoredResult = ExpeditionRun.Restore(catalog, oldResult);
        var resultJournal = ExpeditionJournal.New();
        Check(resultJournal.Observe(restoredResult) && resultJournal.IsUnlocked("castle-clear") &&
            !resultJournal.IsUnlocked("mine") && restoredResult.Wave == 10,
            "An old tenth-boss result records its clear before continuing.");
        restoredResult.Continue();
        Check(resultJournal.Observe(restoredResult) && resultJournal.IsUnlocked("mine"),
            "Continuing the old result reveals the new region.");
        Check(ExpeditionJournal.Restore(null).UnlockedCount == 1 &&
            ExpeditionJournal.Restore(new ExpeditionJournalSave { Version = 2,
                UnlockedIds = new[] { "mine-clear" } }).UnlockedCount == 1 &&
            ExpeditionJournal.Restore(new ExpeditionJournalSave { Version = 1,
                UnlockedIds = null }).UnlockedCount == 1,
            "Missing or incompatible story-only saves reset safely to intro.");
        var dirty = new ExpeditionJournalSave { Version = 1,
            UnlockedIds = new[] { "forest", "forest", "unknown", null, "castle-clear" } };
        var cleaned = ExpeditionJournal.Restore(dirty);
        Check(cleaned.UnlockedCount == 3 && cleaned.IsUnlocked("intro") &&
            cleaned.IsUnlocked("forest") && cleaned.IsUnlocked("castle-clear") &&
            !cleaned.IsUnlocked("unknown") && dirty.UnlockedIds.Length == 5,
            "Duplicates and unknown IDs are ignored without changing caller data.");
        ExpeditionJournalSave copy = cleaned.Capture();
        copy.UnlockedIds[1] = "mine-clear";
        Check(!cleaned.IsUnlocked("mine-clear") && cleaned.IsUnlocked("forest"),
            "A captured array cannot mutate the live journal.");
    }

    static int Main(string[] args)
    {
        try
        {
            catalog = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
            CatalogAndMapping();
            UnlocksAndPersistence();
            LegacyAndInvalidData();
            Console.WriteLine("PASS: expedition story, " + checks + " checks.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error); return 1; }
    }
}
