using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Expedition.Core;

class ResetTests
{
    static int checks;
    static CatalogDatabase db;
    static JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
    static void Check(bool v, string message) { checks++; if (!v) throw new Exception(message); }
    static void Reject(Action a, string why) { bool rejected = false; try { a(); } catch { rejected = true; } Check(rejected, why); }
    static T Copy<T>(T obj) { return json.Deserialize<T>(json.Serialize(obj)); }
    static ExpeditionRun Reload(ExpeditionRun run) { return ExpeditionRun.Restore(db, Copy(run.Capture())); }
    static void Advance(ExpeditionRun run, int victories)
    { while (run.Victories < victories) { run.BeginBattle(); run.ResolveBattle(true); if (!run.HasEnded) run.Continue(); } }
    static ExpeditionHistorySave Read(ExpeditionHistory history) { return Copy(history.Capture()); }
    static void Main(string[] args)
    {
        db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
        string[] nodes = CatalogLegacyResearch.Nodes.Select(n => n.Id).ToArray();
        foreach (string form in new[] { "standard", "tactical", "ember", "frost" })
        for (uint seed = 1; seed <= 20; seed++)
        {
            var a = ExpeditionRun.New(db, seed, startFormationId: form);
            var b = ExpeditionRun.New(db, seed, legacyResearchIds: nodes, startFormationId: form, researchBaselineBestWave: 30);
            Check(a.RulesVersion == 1 && b.RulesVersion == 1 && b.LegacyResearchIds.Count == 0 && b.TacticIds.Count == 0, "inherited power");
            Check(a.Gold == 40 && b.Gold == 40 && b.Lantern == 3 && b.Roster.Members.Count == 3 && !b.HasResearchBaseline, "fresh conditions");
            Check(a.Summon() == b.Summon() && json.Serialize(a.Roster.Capture()) == json.Serialize(b.Roster.Capture()), "account changes random summon");
            var x = a.BeginBattle(); var y = b.BeginBattle();
            while (!x.Ended && !y.Ended) { x.Step(.25f); y.Step(.25f); }
            Check(x.Winner == y.Winner && x.Time == y.Time && x.Ended == y.Ended, "account changes battle outcome");
            foreach (var u in x.Units) Check(y.Get(u.Id).Hp == u.Hp && y.Get(u.Id).MaxHp == u.MaxHp, "account changes unit stats");
            var restored = Reload(b);
            Check(restored.RunId == b.RunId && restored.InitialSeed == seed && restored.RulesVersion == 1, "identity roundtrip");
        }
        Reject(() => ExpeditionRun.New(db, 1, startFormationId: "unknown"), "invalid starting formation");
        var legacy = ExpeditionRun.NewLegacy(db, 9, legacyResearchIds: nodes, startFormationId: "ember");
        var old = legacy.Capture(); old.RunId = null; old.InitialSeed = 0;
        var migration = ExpeditionRun.Restore(db, Copy(old));
        Check(migration.UsesLegacyRules && migration.LegacyResearchIds.Count == 6 && migration.InitialSeed == 0, "old run changed");
        Check(Reload(migration).RunId == migration.RunId, "migration identity not stable");
        Check(migration.BeginBattle().Get("ally-01").MaxHp > db.Get("B-KN").Hp, "old power removed mid-run");
        var account = ExpeditionProgress.Restore(new ExpeditionProgressSave { Version = 2, BestWave = 30,
            CompletedWaves = 30, ResearchIds = nodes });
        Check(account.ResearchEarned == 36 && account.ResearchPoints == 9 && !account.PurchaseResearch("anything"), "old PT archive");
        var freshAccount = ExpeditionProgress.New();
        var run = ExpeditionRun.New(db, 45001);
        var history = new ExpeditionHistory(); var journal = ExpeditionJournal.New();
        for (int wave = 1; wave <= 30; wave++)
        {
            if (wave == 6) Check(run.PurchaseTactic("bulwark"), "first research");
            if (wave == 11) Check(run.PurchaseTactic("shieldcraft") && run.PurchaseTactic("spellcraft"), "upper research");
            int count = run.TacticIds.Count, gold = run.Gold;
            Check(!run.PurchaseTactic("shieldcraft") && run.Gold == gold, "duplicate/locked purchase");
            if (wave >= 11) Check(!run.PurchaseTactic("cinders") && run.TacticIds.Count == 3, "research limit");
            Check(Reload(run).TacticIds.Count == count && Reload(run).Gold == gold, "resume resets research");
            var before = run.Capture();
            run.BeginBattle(); run = Reload(run);
            Check(run.Phase == ExpeditionPhase.Battle, "battle restore");
            run.ResolveBattle(true); run = Reload(run);
            freshAccount.Observe(run); journal.Observe(run); history.Observe(run);
            Check(freshAccount.ResearchEarned == 0 && !freshAccount.PurchaseResearch("tactical-start"), "new PT granted");
            Check(!run.ResolveBattle(true), "reward replay");
            var snapshot = new ExpeditionAccountSave { Version = 2, HasRun = true, Run = run.Capture(), Progress = freshAccount.Capture(),
                Journal = journal.Capture(), History = history.Capture(), TutorialMask = 3, LessonMask = 7 };
            Copy(snapshot).Validate(db); checks++;
            run.Continue(); run = Reload(run);
            Check(run.TacticIds.Count == count && run.Gold >= before.Gold, "next battle resets research");
        }
        Check(run.Score == 333000 && run.HasEnded && history.Recent.Count == 1 && !history.Observe(run), "complete score or duplicate");
        Check(ExpeditionRun.New(db, 2).TacticIds.Count == 0, "new run retained research");
        Check(ExpeditionHistory.Restore(db, Read(history)).Best.Score == 333000, "best score reload");
        var forged = Copy(run.Capture()); forged.LegacyResearchIds = nodes;
        Reject(() => ExpeditionRun.Restore(db, forged), "v9 permanent power accepted");
        forged = Copy(run.Capture()); forged.RulesVersion = 0;
        Reject(() => ExpeditionRun.Restore(db, forged), "v9 downgraded rules");
        var bad = Read(history); bad.Recent[0].Score++;
        Reject(() => ExpeditionHistory.Restore(db, bad), "tampered score accepted");
        bad = Read(history); bad.Recent = new[] { bad.Recent[0], bad.Recent[0] };
        Reject(() => ExpeditionHistory.Restore(db, bad), "duplicate record accepted");
        bad = Read(history); bad.Best = null;
        Reject(() => ExpeditionHistory.Restore(db, bad), "missing best accepted");
        for (int wins = 0; wins < 30; wins++)
        {
            var failed = ExpeditionRun.New(db, (uint)(wins + 1)); Advance(failed, wins);
            int gold = failed.Gold;
            for (int life = 3; life > 0; life--)
            { failed.BeginBattle(); failed.ResolveBattle(false); Check(failed.Lantern == life - 1 && failed.Gold == gold, "failure economy"); failed.Continue(); failed = Reload(failed); }
            Check(failed.Score == wins * 10000 && history.Observe(failed), "defeat scoring");
        }
        var ongoing = ExpeditionRun.New(db, 6); Advance(ongoing, 6); ongoing.PurchaseTactic("bulwark");
        Check(!history.Observe(ongoing) && history.Observe(ongoing, true), "abandon registration");
        Check(!history.Recent[0].Ranked && history.Recent[0].Score == 0, "abandon ranked");
        Check(history.Recent.Count == 30 && history.Best.Score == 333000 && history.Best.FinalRun.RunId == run.RunId, "best evicted with recent");
        ExpeditionHistory.Restore(db, Read(history)); checks++;
        var oldEnd = ExpeditionRun.NewLegacy(db, 7); Advance(oldEnd, 30); history.Observe(oldEnd);
        Check(!history.Recent[0].Ranked && history.Recent[0].Score == 0, "old run ranked");
        int high = 0;
        for (int c = 0; c <= 30; c++)
        { int low = c * 10000 + (c == 30 ? 30000 : 0); Check(c == 0 || low > high, "progress monotonicity"); high = low + 3000; }
        Console.WriteLine("PASS reset/history: " + checks + " checks, 80 paired real battles, 30-wave save path, 30 defeat boundaries, legacy preservation and score validation.");
    }
}
