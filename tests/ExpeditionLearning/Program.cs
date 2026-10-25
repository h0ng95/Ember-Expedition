using System;
using System.IO;
using System.Web.Script.Serialization;
using Expedition.Core;

static class Program
{
    static int checks;
    static CatalogDatabase db;
    static JavaScriptSerializer json = new JavaScriptSerializer();
    static void Check(bool ok, string text) { checks++; if (!ok) throw new Exception(text); }
    static ExpeditionRun RoundTrip(ExpeditionRun run)
    { return ExpeditionRun.Restore(db, json.Deserialize<ExpeditionRunSave>(json.Serialize(run.Capture()))); }
    static void Win(ExpeditionRun run) { run.BeginBattle(); Check(run.ResolveBattle(true), "resolve win"); Check(run.Continue(), "continue win"); }
    static int Main(string[] args)
    {
        try
        {
            db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
            for (int baseline = 0; baseline <= 30; baseline++)
            {
                var progress = ExpeditionProgress.Restore(new ExpeditionProgressSave { Version = 2, BestWave = baseline, ResearchIds = new string[0] });
                var run = ExpeditionRun.New(db, 42, researchBaselineBestWave: baseline);
                int originalEarned = progress.ResearchEarned, earnedGold = 0;
                Check(run.BattleSpeed == 1 && run.VictoryGoldEarned == 0 && run.ResearchEarnedThisRun == 0, "fresh summary");
                Check(run.SetBattleSpeed(2) && !run.SetBattleSpeed(3), "speed validation");
                string recruited = run.Summon(); Check(recruited != null && run.GoldSpent == 20, "summon expense");
                for (int wave = 1; wave <= 30; wave++)
                {
                    run.BeginBattle(); Check(run.ResolveBattle(true), "win"); earnedGold += run.LastReward;
                    progress.Observe(run);
                    int balance = progress.ResearchPoints;
                    Check(!progress.Observe(run) && progress.ResearchPoints == balance, "summary must not grant repeat rewards");
                    Check(run.ResearchEarnedThisRun == progress.ResearchEarned - originalEarned, "first-clear PT mismatch");
                    Check(run.VictoryGoldEarned == earnedGold && run.GoldSpent == 20, "cumulative gold mismatch");
                    run = RoundTrip(run);
                    Check(run.BattleSpeed == 2 && run.ResearchBaselineBestWave == baseline && run.HasResearchBaseline, "metadata lost on restart");
                    Check(run.Continue(), "continue");
                }
                Check(run.Phase == ExpeditionPhase.Completed && run.ResearchEarnedThisRun == 36 - originalEarned, "clear summary");
            }
            var failed = ExpeditionRun.New(db, 43, researchBaselineBestWave: 5);
            for (int i = 0; i < 8; i++) Win(failed);
            int initialGold = failed.Gold;
            for (int i = 0; i < 3; i++) { failed.BeginBattle(); failed.ResolveBattle(false); failed.Continue(); failed = RoundTrip(failed); }
            Check(failed.Phase == ExpeditionPhase.Defeated && failed.Wave == 9 && failed.Victories == 8 &&
                failed.ResearchEarnedThisRun == 3 && failed.Gold == initialGold && failed.VictoryGoldEarned == initialGold - 40, "defeat summary");
            var old = failed.Capture(); old.HasResearchBaseline = false; old.BattleSpeed = 0;
            var restored = ExpeditionRun.Restore(db, old);
            Check(!restored.ResearchEarnedThisRun.HasValue && restored.BattleSpeed == 1, "old saves require unknown PT and default speed");
            old.HasResearchBaseline = true; old.ResearchBaselineBestWave = 999; old.BattleSpeed = 999;
            restored = ExpeditionRun.Restore(db, old);
            Check(!restored.HasResearchBaseline && restored.BattleSpeed == 1, "damaged optional metadata fallback");

            var learning = ExpeditionRun.New(db, 44);
            Check(ExpeditionLearning.GrowthExample(learning) == null && !ExpeditionLearning.CanExploreTactics(learning), "no fake affordances");
            string donor = learning.Summon();
            string before = json.Serialize(learning.Capture());
            var example = ExpeditionLearning.GrowthExample(learning);
            Check(example != null && example.DonorId == donor && before == json.Serialize(learning.Capture()), "growth suggestion mutates state");
            Check(ExpeditionLearning.GrowthExample(learning, 1) == null, "hidden page donor suggested");
            learning.SetMaterialLocked(donor, true); Check(ExpeditionLearning.GrowthExample(learning) == null, "protected donor suggested");
            learning.SetMaterialLocked(donor, false); learning.SetDeployed(donor, true);
            Check(ExpeditionLearning.GrowthExample(learning) == null, "field sacrifice suggested");
            learning.SetDeployed(donor, false); learning.Transfer(donor, "ally-01", false);
            Check(ExpeditionLearning.EvolutionExample(learning) == null, "growth 1 cannot evolve");
            donor = learning.Summon(); learning.Transfer(donor, "ally-01", false);
            Check(ExpeditionLearning.EvolutionExample(learning).Id == "ally-01", "ready evolution missed");
            learning.Evolve("ally-01", "E-KN-G");
            Check(ExpeditionLearning.ExistingKnowledge(learning) == 3, "existing player migration");
            while (learning.Wave < 6) Win(learning);
            Check(ExpeditionLearning.CanExploreTactics(learning), "affordable tactic missing");
            learning.PurchaseTactic("bulwark"); Check(ExpeditionLearning.ExistingKnowledge(learning) == 7, "existing tactic migration");
            Check(ExpeditionLearning.RestoreMask(0) == 0 && ExpeditionLearning.RestoreMask(7) == 7 && ExpeditionLearning.RestoreMask(99) == 7, "guide preference bounds");
            Console.WriteLine("PASS: learning eligibility, speed persistence and result accounting, " + checks + " assertions.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
