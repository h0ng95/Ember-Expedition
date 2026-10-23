using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using Expedition.Core;

// Compare legal early growth choices. Results describe fixed automated policies,
// not player win rates. Continuation rewards are hypothetical after a loss.
internal static class EarlyBalanceProbe
{
    static readonly string[] Policies = {
        "starter3", "basic5", "knight_first", "archer_first", "priest_first",
        "compact_knight", "growth3"
    };

    static string Branch(string form)
    {
        switch (CatalogRoster.FamilyCode(form))
        {
            case "KN": return "E-KN-G";
            case "AR": return "E-AR-P";
            case "PR": return "E-PR-H";
            case "FI": return "E-FI-B";
            case "IC": return "E-IC-F";
            default: return "E-BA-G";
        }
    }

    static void Prepare(ExpeditionRun run, string policy)
    {
        int capacity = policy == "growth3" ? 3 : 5;
        string priority = policy == "archer_first" ? "ally-02" :
            policy == "priest_first" ? "ally-03" : "ally-01";
        if (policy != "starter3")
            while (run.CanSummon && (policy != "basic5" || run.DeployedIds.Count < capacity))
            {
                string donor = run.Summon();
                if (run.DeployedIds.Count < capacity)
                {
                    if (!run.SetDeployed(donor, true)) throw new Exception("Deploy failed");
                    continue;
                }
                var active = run.DeployedIds.Select(run.Roster.Find).ToArray();
                var target = active.FirstOrDefault(x => x.Id == priority && x.FormId.StartsWith("B-")) ??
                    active.FirstOrDefault(x => x.FormId.StartsWith("B-")) ??
                    active.FirstOrDefault(x => x.FormId.StartsWith("E-") && x.Growth < 6);
                if (target == null) break;
                if (!run.Transfer(donor, target.Id, false)) throw new Exception("Transfer failed");
                if (target.FormId.StartsWith("B-") && target.Growth >= 2)
                {
                    if (!run.Evolve(target.Id, Branch(target.FormId))) throw new Exception("Branch failed");
                }
                else if (target.FormId.StartsWith("E-") && target.Growth >= 6)
                {
                    if (!run.Evolve(target.Id, "F-" + CatalogRoster.FamilyCode(target.FormId)))
                        throw new Exception("Final failed");
                }
            }
        int front = 0, back = 0;
        float[] lanes = { 1.8f, -1.8f, .6f, -.6f, 0f };
        foreach (string id in run.DeployedIds.ToArray())
        {
            bool tank = CatalogRoster.FamilyCode(run.Roster.Find(id).FormId) == "KN";
            float x = tank ? -1.3f : -3.7f;
            float y = tank ? (front++ == 0 ? 0f : -1.7f) : lanes[back++];
            if (policy == "compact_knight") y = 0f;
            run.Move(id, x, y);
        }
    }

    static string N(double value) { return value.ToString("0.000", CultureInfo.InvariantCulture); }

    static int Main(string[] args)
    {
        try
        {
            var db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
            int first = int.Parse(args[2]), count = int.Parse(args[3]);
            var output = new StringBuilder("{\"firstSeed\":" + first + ",\"count\":" + count +
                ",\"note\":\"Fixed automated policies; hypothetical prior rewards after a loss. " +
                "reachWins counts uninterrupted wins. HP is remaining team HP divided by starting team HP.\",\"rows\":[");
            bool comma = false;
            int retryAttempts = 0, retryWins = 0;
            foreach (string policy in Policies)
            {
                int[] wins = new int[5], reach = new int[5], timeouts = new int[5];
                double[] time = new double[5], hp = new double[5], survivors = new double[5], evolved = new double[5];
                for (int seed = first; seed < first + count; seed++)
                {
                    var run = ExpeditionRun.New(db, (uint)seed);
                    bool unbeaten = true;
                    for (int wave = 0; wave < 5; wave++)
                    {
                        Prepare(run, policy);
                        evolved[wave] += run.DeployedIds.Count(id => !run.Roster.Find(id).FormId.StartsWith("B-"));
                        var battle = run.BeginBattle();
                        float maxHp = battle.Units.Where(u => u.Team == CatalogTeam.Ally).Sum(u => u.MaxHp);
                        while (!battle.Ended && battle.Time < 75f) battle.Step(.25f);
                        bool win = battle.Ended && battle.Winner == CatalogTeam.Ally;
                        if (win) wins[wave]++; else unbeaten = false;
                        if (unbeaten) reach[wave]++;
                        if (!battle.Ended) timeouts[wave]++;
                        time[wave] += battle.Time;
                        survivors[wave] += battle.Units.Count(u => u.Team == CatalogTeam.Ally && u.IsAlive);
                        hp[wave] += battle.Units.Where(u => u.Team == CatalogTeam.Ally).Sum(u => Math.Max(0f, u.Hp)) / maxHp;
                        if (policy == "basic5" && wave == 4 && !win)
                        {
                            // Real loss path: no hypothetical reward and no fixture resources.
                            run.ResolveBattle(false);
                            run.Continue();
                            Prepare(run, "knight_first");
                            var retry = run.BeginBattle();
                            while (!retry.Ended && retry.Time < 75f) retry.Step(.25f);
                            retryAttempts++;
                            if (retry.Ended && retry.Winner == CatalogTeam.Ally) retryWins++;
                            continue;
                        }
                        run.ResolveBattle(true);
                        run.Continue();
                    }
                }
                for (int wave = 0; wave < 5; wave++)
                {
                    if (comma) output.Append(','); comma = true;
                    output.Append("{\"policy\":\"").Append(policy).Append("\",\"wave\":").Append(wave + 1)
                        .Append(",\"wins\":").Append(wins[wave]).Append(",\"reachWins\":").Append(reach[wave])
                        .Append(",\"timeouts\":").Append(timeouts[wave]).Append(",\"seconds\":").Append(N(time[wave] / count))
                        .Append(",\"hpFraction\":").Append(N(hp[wave] / count))
                        .Append(",\"survivors\":").Append(N(survivors[wave] / count))
                        .Append(",\"evolvedAtStart\":").Append(N(evolved[wave] / count)).Append('}');
                    Console.WriteLine(policy + " wave=" + (wave + 1) + " wins=" + wins[wave] + "/" + count +
                        " reach=" + reach[wave] + " sec=" + N(time[wave] / count) +
                        " hp=" + N(hp[wave] / count) + " evolved=" + N(evolved[wave] / count));
                }
            }
            output.Append("],\"basic5BossRetry\":{\"attempts\":").Append(retryAttempts)
                .Append(",\"winsAfterSpendingSavedGoldOnGrowth\":").Append(retryWins).Append("}}");
            Console.WriteLine("basic5 boss retry after legal growth: " + retryWins + "/" + retryAttempts);
            File.WriteAllText(args[1], output.ToString());
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
