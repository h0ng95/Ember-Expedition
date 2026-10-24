using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Expedition.Core;

internal static class ExpeditionPreparationHintsTests
{
    static CatalogDatabase catalog;
    static readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    static int checks;

    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }

    static ExpeditionRun AtWave(int wave)
    {
        var run = ExpeditionRun.New(catalog, 27);
        while (run.Wave < wave)
        { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
        return run;
    }

    static string[] Hints(ExpeditionRun run)
    {
        string before = json.Serialize(run.Capture());
        string[] result = ExpeditionPreparationHints.ForBoss(run, catalog);
        Check(json.Serialize(run.Capture()) == before, "Reading hints does not mutate run or undo history.");
        Check(result.Length <= 3 && result.All(line => line.Length <= 30 && line.Length > 0),
            "Hints are at most three short lines.");
        return result;
    }

    static void AvailabilityAndRoster()
    {
        var run = AtWave(4);
        Check(Hints(run).Length == 0, "Ordinary preparation has no boss hints.");
        run = AtWave(5);
        Check(Hints(run).Length == 3, "Boss preparation has three lines.");
        string benchId = run.Summon();
        Check(benchId != null && Hints(run)[0] == "출전 3/5 · 대기 동료 1명",
            "A real bench member and short party lead the list.");
        run.SetDeployed(benchId, true);
        Check(!Hints(run).Any(line => line.StartsWith("출전 ")),
            "Deploying the only bench member removes bench advice.");
        Check(run.Undo() && Hints(run)[0] == "출전 3/5 · 대기 동료 1명",
            "Undo restores the earlier deployment advice.");
        Check(ExpeditionPreparationHints.ForBoss(ExpeditionRun.Restore(catalog, run.Capture()), catalog)[0] ==
            "출전 3/5 · 대기 동료 1명", "Saved preparation keeps the same advice.");
        run.BeginBattle();
        Check(Hints(run).Length == 0, "Battle does not display preparation hints.");
        run.ResolveBattle(false);
        Check(Hints(run).Length == 0, "Result does not display preparation hints.");
    }

    static void GrowthAndFrontline()
    {
        var run = AtWave(5);
        string first = run.Summon();
        Check(run.Transfer(first, "ally-01", false) &&
            !Hints(run).Any(line => line.Contains("진화 가능")),
            "Growth one is not advertised as ready.");
        string second = run.Summon();
        Check(run.Transfer(second, "ally-01", false) &&
            Hints(run).Any(line => line.Contains("분기 진화 가능")),
            "A deployed base member at growth two is ready to branch.");
        Check(run.Undo() && !Hints(run).Any(line => line.Contains("진화 가능")),
            "Undo removes a no-longer-ready evolution hint.");
        Check(run.Transfer(second, "ally-01", false) && run.Evolve("ally-01", "E-KN-G"),
            "Fixture legally branches its knight.");
        while (run.Wave < 10)
        { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
        Check(Hints(run).Any(line => line.Contains("최종 진화까지 성장 4")) &&
            !Hints(run).Any(line => line.Contains("최종 진화 가능")),
            "Ten-boss hint states the exact remaining E-knight growth, not false readiness.");
        for (int i = 0; i < 4; i++)
        {
            string donor = run.Summon();
            Check(donor != null && run.Transfer(donor, "ally-01", false),
                "Legal donor advances E-knight growth.");
        }
        Check(Hints(run).Any(line => line.Contains("최종 진화 가능")) &&
            !Hints(run).Any(line => line.Contains("최종 진화까지 성장")),
            "Growth six is ready now rather than one more step away.");
        var basic = AtWave(10);
        Check(Hints(basic).Any(line => line.Contains("첫 진화까지 성장 2")),
            "Unchanged basic knight gets a first-branch growth count.");
    }

    static void OnlyDeployedForms()
    {
        var run = AtWave(10);
        string benchId = run.Summon();
        CatalogMember bench = run.Roster.Find(benchId);
        bench.FormId = "E-KN-G"; bench.Growth = 6;
        Check(!Hints(run).Any(line => line.Contains("최종 진화 가능")),
            "A ready E form on the bench is not mistaken for a deployed option.");
        bench.FormId = "H-06"; bench.Growth = 0;
        Check(!Hints(run).Any(line => line.StartsWith("지휘:")),
            "A benched H-06 does not supply the battle command hint.");
        Check(run.SwapDeployment("ally-02", benchId), "Fixture deploys the hybrid form.");
        string[] deployed = Hints(run);
        Check(deployed.Any(line => line == "지휘: 본인+거리3 내 최대2명, 평타1회+60%") &&
            !deployed.Any(line => line.Contains("H-06") && line.Contains("진화")),
            "Deployed H-06 explains its actual next-basic support without fake growth.");
        run.Roster.Find("ally-01").FormId = "H-02";
        Check(!Hints(run).Any(line => line.Contains("진화 가능") || line.Contains("진화까지")),
            "A hybrid frontline has no fabricated branch or final growth path.");
    }

    static void Main(string[] args)
    {
        catalog = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
        AvailabilityAndRoster(); GrowthAndFrontline(); OnlyDeployedForms();
        Console.WriteLine("Expedition preparation hints: " + checks + " checks passed.");
    }
}
