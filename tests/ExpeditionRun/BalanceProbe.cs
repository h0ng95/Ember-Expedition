using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using Expedition.Core;

// Uses only legal summon/growth operations. Later encounters are also probed after
// a loss with hypothetical victory rewards; reachWins separately counts unbroken runs.
internal static class ExpeditionBalanceProbe
{
    static readonly string[] Policies = { "starter3", "basic5", "branches", "final", "hybrid" };
    static string Branch(string form)
    {
        switch (CatalogRoster.FamilyCode(form))
        {
            case "KN": return "E-KN-G"; case "AR": return "E-AR-P";
            case "PR": return "E-PR-H"; case "FI": return "E-FI-B";
            case "IC": return "E-IC-F"; default: return "E-BA-G";
        }
    }

    static void Grow(ExpeditionRun run, string policy, string donor)
    {
        var active = run.DeployedIds.Select(run.Roster.Find).ToArray();
        CatalogMember recipient = null;
        if (policy == "final")
            recipient = active.FirstOrDefault(x => x.Id == "ally-01" && !x.FormId.StartsWith("F-"));
        if (policy == "hybrid")
            recipient = active.FirstOrDefault(x => (x.Id == "ally-01" || x.Id == "ally-03") && x.FormId.StartsWith("B-"));
        if (recipient == null)
            recipient = active.FirstOrDefault(x => x.FormId.StartsWith("B-"));
        if (recipient == null)
            recipient = active.FirstOrDefault(x => x.FormId.StartsWith("E-"));
        if (recipient == null || !run.Transfer(donor, recipient.Id, false)) return;
        if (recipient.FormId.StartsWith("B-") && recipient.Growth >= 2) run.Evolve(recipient.Id, Branch(recipient.FormId));
        if (recipient.FormId.StartsWith("E-") && recipient.Growth >= 6)
            run.Evolve(recipient.Id, "F-" + CatalogRoster.FamilyCode(recipient.FormId));
    }

    static void Prepare(ExpeditionRun run, string policy)
    {
        // Wait until another summon can refill the slot consumed by the hybrid.
        if (policy == "hybrid" && run.Wave >= 6 && run.Gold >= ExpeditionRun.SummonCost &&
            run.Roster.Find("ally-01") != null && run.Roster.Find("ally-03") != null &&
            run.Roster.Find("ally-01").FormId.StartsWith("E-KN-") && run.Roster.Find("ally-03").FormId.StartsWith("E-PR-"))
            run.Combine("ally-01", "ally-03", "H-02");
        if (policy != "starter3")
            while (run.CanSummon && (policy != "basic5" || run.DeployedIds.Count < 5))
            {
                string id = run.Summon();
                if (run.DeployedIds.Count < 5) run.SetDeployed(id, true);
                else Grow(run, policy, id);
            }
        // Same role-aware formation for all policies, no enemy-specific adaptation.
        int back = 0, front = 0;
        foreach (string id in run.DeployedIds.ToArray())
        {
            CatalogMember member = run.Roster.Find(id);
            string family = CatalogRoster.FamilyCode(member.FormId);
            if (family == "KN" || member.FormId == "H-02")
                run.Move(id, -1.3f, front++ == 0 ? 0f : -1.7f);
            else
            {
                float[] lanes = { 1.8f, -1.8f, .6f, -.6f, 0f };
                run.Move(id, -3.7f, lanes[back++]);
            }
        }
    }

    static string Number(double value) { return value.ToString("0.00", CultureInfo.InvariantCulture); }
    static int Main(string[] args)
    {
        try
        {
            var db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
            int first = int.Parse(args[2]), count = int.Parse(args[3]);
            var json = new StringBuilder();
            json.Append("{\"firstSeed\":").Append(first).Append(",\"count\":").Append(count)
                .Append(",\"note\":\"Each wave uses legally grown parties assuming all preceding rewards; reachWins counts only uninterrupted wins. Fixed automated policies are not human win rates.\",\"rows\":[");
            bool comma = false;
            foreach (string policy in Policies)
            {
                int[] wins = new int[10], reach = new int[10], timeout = new int[10];
                double[] times = new double[10], survivors = new double[10], health = new double[10];
                for (int seed = first; seed < first + count; seed++)
                {
                    var run = ExpeditionRun.New(db, (uint)seed);
                    bool undefeated = true;
                    for (int wave = 0; wave < 10; wave++)
                    {
                        Prepare(run, policy);
                        var battle = run.BeginBattle();
                        while (!battle.Ended && battle.Time < 75) battle.Step(.25f);
                        bool win = battle.Ended && battle.Winner == CatalogTeam.Ally;
                        if (win) wins[wave]++;
                        else undefeated = false;
                        if (undefeated) reach[wave]++;
                        if (!battle.Ended) timeout[wave]++;
                        times[wave] += battle.Time;
                        survivors[wave] += battle.Units.Count(x => x.Team == CatalogTeam.Ally && x.IsAlive);
                        var allies = battle.Units.Where(x => x.Team == CatalogTeam.Ally).ToArray();
                        health[wave] += allies.Sum(x => x.Hp) / allies.Sum(x => x.MaxHp);
                        // Counterfactual continuation for isolated later-wave comparisons.
                        run.ResolveBattle(true); run.Continue();
                    }
                }
                for (int wave = 0; wave < 10; wave++)
                {
                    if (comma) json.Append(','); comma = true;
                    json.Append("{\"policy\":\"").Append(policy).Append("\",\"wave\":").Append(wave + 1)
                        .Append(",\"wins\":").Append(wins[wave]).Append(",\"reachWins\":").Append(reach[wave])
                        .Append(",\"timeouts\":").Append(timeout[wave]).Append(",\"meanSeconds\":").Append(Number(times[wave] / count))
                        .Append(",\"meanSurvivors\":").Append(Number(survivors[wave] / count))
                        .Append(",\"meanHealthFraction\":").Append(Number(health[wave] / count)).Append('}');
                    Console.WriteLine(policy + " wave=" + (wave + 1) + " wins=" + wins[wave] + "/" + count +
                        " reach=" + reach[wave] + " sec=" + Number(times[wave] / count));
                }
            }
            json.Append("]}");
            File.WriteAllText(args[1], json.ToString());
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
