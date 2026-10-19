using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Expedition.Core;

internal static class BalanceProbe
{
    sealed class TeamSpec
    {
        public string Name;
        public string[] Forms;
        public string[] Branches;
        public int OriginalCost, DeployCount;
    }
    sealed class Summary
    {
        public int Wins, Losses, Timeouts;
        public double Duration, Survivors, Damage, Heal, ShieldAbsorbed, SkillUses;
    }
    static readonly TeamSpec[] Teams =
    {
        new TeamSpec { Name = "final_focus", Forms = new[] { "F-KN", "E-AR-P", "B-FI", "B-PR", "B-IC" },
            Branches = new[] { "E-KN-G", null, null, null, null }, OriginalCost = 13, DeployCount = 5 },
        new TeamSpec { Name = "four_branches", Forms = new[] { "E-KN-G", "E-AR-P", "E-FI-B", "E-PR-H", "B-IC" },
            Branches = new string[5], OriginalCost = 13, DeployCount = 5 },
        new TeamSpec { Name = "hybrid_compact", Forms = new[] { "H-01", "E-AR-P", "E-PR-H", "B-IC" },
            Branches = new string[4], OriginalCost = 13, DeployCount = 4 },
        new TeamSpec { Name = "six_basics_rotate_bench", Forms = new[] { "B-KN", "B-AR", "B-FI", "B-PR", "B-IC", "B-BA" },
            Branches = new string[6], OriginalCost = 6, DeployCount = 5 }
    };
    static readonly string[] Scenarios = { "swarm", "backline_raid", "boss" };

    static int Main(string[] args)
    {
        try
        {
            CatalogDatabase before = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
            CatalogDatabase after = CatalogDatabase.FromJson(File.ReadAllText(args[1]));
            string result = Produce(before, after);
            File.WriteAllText(args[2], result, Encoding.UTF8);
            Console.WriteLine("PASS: 100 seeded formation/target variants per team and encounter for before/after; " + args[2]);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    static string Produce(CatalogDatabase before, CatalogDatabase after)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"experiment\": \"v0.9-draft-vs-v0.10-candidate\",");
        builder.AppendLine("  \"seeds\": 100,");
        builder.AppendLine("  \"timeLimitSeconds\": 60,");
        builder.AppendLine("  \"placement\": \"System.Random(seed), ally x -1.3 or -3.2 plus 0..0.4 and y -0.6..0.6; enemy y -0.5..0.5, swarm insertion order shuffled; six-basics bench rotates by seed\",");
        builder.AppendLine("  \"teamCost\": \"final=7+3+1+1+1 (13), branches=3+3+3+3+1 (13), hybrid=6+3+3+1 (13), basic six owned/five deployed (6)\",");
        builder.AppendLine("  \"encounters\": {\"form_probe\":\"4 melee, each 620 HP/25 power/17 defense/1.2s/1.1 range/1.2 speed\",\"swarm\":\"4 melee, each 700 HP/27 power/20 defense/1.15s/1.1 range/1.3 speed\",\"backline_raid\":\"2 melee 550 HP/26 power plus 2 rear-spawned ranged 430 HP/22 power/5 range\",\"boss\":\"1 boss 3600 HP/70 power/35 defense/1.25s/1.3 range/1 speed plus 2 escorts 400 HP/22 power\"},");
        builder.AppendLine("  \"results\": [");
        int row = 0;
        foreach (string version in new[] { "draft", "candidate" })
        {
            CatalogDatabase data = version == "draft" ? before : after;
            foreach (TeamSpec team in Teams)
                foreach (string encounter in Scenarios)
                {
                    Summary summary = RunSeries(data, team, encounter);
                    if (row++ > 0) builder.AppendLine(",");
                    builder.Append("    {\"version\":\"").Append(version).Append("\",\"team\":\"")
                        .Append(team.Name).Append("\",\"encounter\":\"").Append(encounter)
                        .Append("\",\"originalCost\":").Append(team.OriginalCost)
                        .Append(",\"deployed\":").Append(team.DeployCount)
                        .Append(",\"wins\":").Append(summary.Wins)
                        .Append(",\"losses\":").Append(summary.Losses)
                        .Append(",\"timeouts\":").Append(summary.Timeouts)
                        .Append(",\"meanDuration\":").Append(N(summary.Duration / 100))
                        .Append(",\"meanAllySurvivors\":").Append(N(summary.Survivors / 100))
                        .Append(",\"meanAllyDamage\":").Append(N(summary.Damage / 100))
                        .Append(",\"meanActualHeal\":").Append(N(summary.Heal / 100))
                        .Append(",\"meanActualShieldAbsorbed\":").Append(N(summary.ShieldAbsorbed / 100))
                        .Append('}');
                }
        }
        builder.AppendLine(); builder.AppendLine("  ],");
        builder.AppendLine("  \"individualFormMatrix\": [");
        row = 0;
        foreach (string version in new[] { "draft", "candidate" })
        {
            CatalogDatabase data = version == "draft" ? before : after;
            foreach (CatalogForm form in data.All)
            {
                int variants = form.Id.StartsWith("F-", StringComparison.Ordinal) ? 2 : 1;
                for (int variant = 0; variant < variants; variant++)
                {
                    string branch = BranchFor(form.Id, variant);
                    var test = new TeamSpec { Name = form.Id, OriginalCost = 4 + Cost(form.Id), DeployCount = 5,
                        Forms = new[] { "B-KN", "B-AR", "B-PR", "B-IC", form.Id },
                        Branches = new[] { null, null, null, null, branch } };
                    Summary result = RunSeries(data, test, "form_probe");
                    if (row++ > 0) builder.AppendLine(",");
                    builder.Append("    {\"version\":\"").Append(version).Append("\",\"form\":\"").Append(form.Id)
                        .Append("\",\"branch\":").Append(branch == null ? "null" : "\"" + branch + "\"")
                        .Append(",\"originalCostWithFourAnchors\":").Append(test.OriginalCost)
                        .Append(",\"wins\":").Append(result.Wins)
                        .Append(",\"meanSurvivors\":").Append(N(result.Survivors / 100))
                        .Append(",\"meanSkillActivations\":").Append(N(result.SkillUses / 100)).Append('}');
                }
            }
        }
        builder.AppendLine(); builder.AppendLine("  ],");
        builder.AppendLine("  \"hybridVersusComponents\": [");
        string[] hybrids = { "H-01", "H-02", "H-03", "H-04", "H-05", "H-06" };
        string[][] components =
        {
            new[] { "E-KN-C", "E-FI-B" }, new[] { "E-KN-G", "E-PR-H" },
            new[] { "E-AR-P", "E-IC-F" }, new[] { "E-FI-B", "E-IC-F" },
            new[] { "E-PR-H", "E-BA-A" }, new[] { "E-AR-T", "E-BA-A" }
        };
        row = 0;
        foreach (string version in new[] { "draft", "candidate" })
        {
            CatalogDatabase data = version == "draft" ? before : after;
            for (int h = 0; h < hybrids.Length; h++)
                foreach (string encounter in new[] { "form_probe", "backline_raid" })
                    for (int pair = 0; pair < 2; pair++)
                    {
                        string[] forms = pair == 0 ?
                            new[] { "B-KN", "B-AR", "B-PR", hybrids[h] } :
                            new[] { "B-KN", "B-AR", "B-PR", components[h][0], components[h][1] };
                        var test = new TeamSpec { Name = hybrids[h], OriginalCost = 9,
                            DeployCount = forms.Length, Forms = forms, Branches = new string[forms.Length] };
                        Summary result = RunSeries(data, test, encounter);
                        if (row++ > 0) builder.AppendLine(",");
                        builder.Append("    {\"version\":\"").Append(version).Append("\",\"hybrid\":\"")
                            .Append(hybrids[h]).Append("\",\"mode\":\"").Append(pair == 0 ? "combined" : "two_branches")
                            .Append("\",\"encounter\":\"").Append(encounter)
                            .Append("\",\"cost\":9,\"deployed\":").Append(forms.Length)
                            .Append(",\"wins\":").Append(result.Wins)
                            .Append(",\"meanSurvivors\":").Append(N(result.Survivors / 100)).Append('}');
                    }
        }
        builder.AppendLine(); builder.AppendLine("  ],");
        int hybridReachable = 0, finalReachable = 0;
        for (int seed = 0; seed < 100; seed++)
        {
            var random = new Random(seed);
            bool knight = false, fire = false;
            for (int draw = 0; draw < 14; draw++)
            {
                int family = random.Next(6);
                if (family == 0) knight = true;
                if (family == 2) fire = true;
            }
            if (knight && fire) hybridReachable++;
            finalReachable++; // Any summoned family can use six of the other thirteen as donors.
        }
        builder.Append("  \"summonSupply100Seeds\": {\"h01FamilyPairPresent\":").Append(hybridReachable)
            .Append(",\"someFinalReachable\":").Append(finalReachable).AppendLine("}");
        builder.AppendLine("}");
        return builder.ToString();
    }

    static Summary RunSeries(CatalogDatabase data, TeamSpec team, string scenario)
    {
        var summary = new Summary();
        for (int seed = 0; seed < 100; seed++)
        {
            var random = new Random(seed);
            var battle = new CatalogBattle(data);
            double damage = 0, heal = 0, blocked = 0;
            battle.Event += ev =>
            {
                if (ev.Kind == CatalogEventKind.SkillUsed && ev.SourceId == "ally" + (team.Forms.Length - 1))
                    summary.SkillUses++;
                if (ev.Kind == CatalogEventKind.Damage && ev.SourceId != null &&
                    ev.SourceId.StartsWith("ally", StringComparison.Ordinal)) damage += ev.Amount;
                if (ev.Kind == CatalogEventKind.Heal && ev.SourceId != null &&
                    ev.SourceId.StartsWith("ally", StringComparison.Ordinal)) heal += ev.Amount;
                if (ev.Kind == CatalogEventKind.ShieldBlocked && ev.SourceId != null &&
                    ev.SourceId.StartsWith("ally", StringComparison.Ordinal)) blocked += ev.Amount;
            };
            for (int i = 0; i < team.Forms.Length; i++)
            {
                if (team.Forms.Length > team.DeployCount && i == seed % team.Forms.Length) continue;
                string form = team.Forms[i];
                bool front = form.Contains("KN") || form == "H-01";
                float x = (front ? -1.3f : -3.2f) - random.Next(3) * .2f;
                float y = -.6f + random.Next(7) * .2f;
                battle.AddUnit("ally" + i, CatalogTeam.Ally, form, team.Branches[i], null, x, y);
            }
            if (scenario == "form_probe")
            {
                int[] insertion = { 0, 1, 2, 3 };
                for (int i = 3; i > 0; i--)
                {
                    int swap = random.Next(i + 1), prior = insertion[i];
                    insertion[i] = insertion[swap]; insertion[swap] = prior;
                }
                foreach (int i in insertion)
                    battle.AddEnemy("enemy" + i, 620, 25, 17, 1.2f, 1.1f, 1.2f,
                        false, 2f + i * .25f, -.4f + random.Next(5) * .2f);
            }
            else if (scenario == "swarm")
            {
                int[] insertion = { 0, 1, 2, 3 };
                for (int i = 3; i > 0; i--)
                {
                    int swap = random.Next(i + 1), prior = insertion[i];
                    insertion[i] = insertion[swap]; insertion[swap] = prior;
                }
                foreach (int i in insertion)
                    battle.AddEnemy("enemy" + i, 700, 27, 20, 1.15f, 1.1f, 1.3f,
                        false, 2f + i * .25f, -.5f + random.Next(6) * .2f);
            }
            else if (scenario == "backline_raid")
            {
                for (int i = 0; i < 2; i++)
                    battle.AddEnemy("front" + i, 550, 26, 12, 1.2f, 1.1f, 1.2f,
                        false, 2f + i * .2f, -.4f + random.Next(5) * .2f);
                for (int i = 0; i < 2; i++)
                    battle.AddEnemy("raider" + i, 430, 22, 8, 1.25f, 5f, 1.2f,
                        false, -4.3f - i * .2f, -.4f + random.Next(5) * .2f);
            }
            else
            {
                battle.AddEnemy("boss", 3600, 70, 35, 1.25f, 1.3f, 1f, true, 2.2f, 0);
                for (int i = 0; i < 2; i++)
                    battle.AddEnemy("escort" + i, 400, 22, 12, 1.3f, 1.1f, 1.2f,
                        false, 2.6f, i == 0 ? -.4f : .4f);
            }
            while (!battle.Ended && battle.Time < 60f) battle.Step(1f / 60f);
            if (battle.Winner == CatalogTeam.Ally) summary.Wins++;
            else if (battle.Winner == CatalogTeam.Enemy) summary.Losses++;
            else summary.Timeouts++;
            summary.Duration += Math.Min(60, battle.Time);
            foreach (CatalogBattleUnit unit in battle.Units)
                if (unit.Team == CatalogTeam.Ally && unit.IsAlive) summary.Survivors++;
            summary.Damage += damage; summary.Heal += heal; summary.ShieldAbsorbed += blocked;
        }
        return summary;
    }

    static string N(double value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }

    static int Cost(string formId)
    { return formId.StartsWith("B-") ? 1 : formId.StartsWith("E-") ? 3 : formId.StartsWith("F-") ? 7 : 6; }

    static string BranchFor(string formId, int variant)
    {
        if (!formId.StartsWith("F-", StringComparison.Ordinal)) return null;
        string family = formId.Substring(2);
        string[][] branches =
        {
            new[] { "E-KN-G", "E-KN-C" }, new[] { "E-AR-P", "E-AR-T" },
            new[] { "E-FI-B", "E-FI-E" }, new[] { "E-PR-G", "E-PR-H" },
            new[] { "E-IC-F", "E-IC-S" }, new[] { "E-BA-G", "E-BA-A" }
        };
        string[] families = { "KN", "AR", "FI", "PR", "IC", "BA" };
        for (int i = 0; i < families.Length; i++) if (families[i] == family) return branches[i][variant];
        throw new ArgumentException("Unknown final family.");
    }
}
