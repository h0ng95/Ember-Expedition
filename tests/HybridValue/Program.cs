using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Expedition.Core;

// Paired, controlled combat experiments. These are not simulated acquisition runs.
internal static class HybridValueProbe
{
    public sealed class Result
    {
        public bool win;
        public double seconds, damage, healing, protection, openingDps, openingCommonHealth, hpFraction, firstDeath;
        public int alive, count, casts;
    }
    public sealed class Row
    {
        public string id, hybrid, branchA, branchB, team, layout, effects, reserve;
        public int wave, supply, equivalentSummonGold;
        public Result separate, combined, refilled;
    }
    static CatalogDatabase db;
    static int serial, assertions;
    static void Require(bool condition, string reason)
    { assertions++; if (!condition) throw new Exception(reason); }
    static readonly int[] Waves = { 9, 10, 14, 15, 19, 20, 24, 25, 28, 29, 30 };
    static readonly string[][] SongTeams = { new[] { "KN", "AR", "FI" }, new[] { "KN", "PR", "FI" }, new[] { "KN", "KN", "AR" }, new[] { "KN", "FI", "IC" } };
    static readonly string[][] CommanderTeams = { new[] { "KN", "PR", "FI" }, new[] { "KN", "PR", "AR" }, new[] { "KN", "PR", "BA" }, new[] { "KN", "FI", "IC" } };
    static string Branch(string family)
    {
        switch (family) {
            case "KN": return "E-KN-G"; case "AR": return "E-AR-P"; case "PR": return "E-PR-H";
            case "FI": return "E-FI-B"; case "IC": return "E-IC-F"; case "BA": return "E-BA-G";
            default: throw new Exception(family);
        }
    }
    static void Recruit(CatalogRoster roster, string id, string form, float x, float y)
    {
        string family = CatalogRoster.FamilyCode(form);
        Require(roster.Recruit(id, "B-" + family, x, y), "recruit");
        if (form.StartsWith("B-")) return;
        int transfers = form.StartsWith("F-") ? 6 : 2;
        for (int i = 0; i < transfers; i++)
        {
            string donor = "material-" + (++serial);
            Require(roster.Recruit(donor, "B-FI", -5, 0), "material");
            Require(roster.Transfer(donor, id, false), "growth");
            if (i == 1) Require(roster.Evolve(id, form.StartsWith("F-") ? Branch(family) : form), "branch");
        }
        if (form.StartsWith("F-")) Require(roster.Evolve(id, form), "final");
    }
    static CatalogRoster Roster(Row row, string[] families)
    {
        var r = new CatalogRoster(db);
        for (int i = 0; i < 3; i++)
        {
            string form = row.wave <= 10 ? i == 0 ? Branch(families[i]) : "B-" + families[i] :
                row.wave <= 15 ? i == 0 ? "F-" + families[i] : Branch(families[i]) :
                row.wave <= 20 ? i <= 1 ? "F-" + families[i] : Branch(families[i]) : "F-" + families[i];
            float x = families[i] == "KN" ? -1.4f : row.layout == "spread" ? -4.1f : -3.2f;
            float y = i == 0 ? 0 : (i == 1 ? -1 : 1) * (row.layout == "spread" ? 2f : 1.2f);
            Recruit(r, "common-" + i, form, x, y);
        }
        float ax = row.layout == "forward" ? -1.8f : row.layout == "spread" ? -3.5f : -2.4f;
        Recruit(r, "anchor", row.branchA, ax, -.45f);
        Recruit(r, "material", row.branchB, row.hybrid == "H-05" ? -3.6f : -2.1f, .45f);
        Recruit(r, "reserve", row.reserve, -5, 0);
        r.CommitPreparation();
        row.supply = r.SupplyUsed;
        row.equivalentSummonGold = (r.SupplyUsed - 3) * ExpeditionRun.SummonCost;
        return r;
    }
    static Result Fight(CatalogRoster r, Row row, CatalogBattle encounter, string mode)
    {
        int originalSupply = r.SupplyUsed;
        float rx = r.Find("material").X, ry = r.Find("material").Y;
        if (mode != "separate") Require(r.Combine("anchor", "material", row.hybrid), "combine");
        Require(r.SupplyUsed == originalSupply, "combine changed investment");
        string[] tactics = row.effects == "none" ? new string[0] : row.wave < 11 ? new[] { "marksmanship" } : new[] { "marksmanship", "exploitation" };
        string[] legacy = row.effects == "none" ? new string[0] : CatalogLegacyResearch.Nodes.Select(n => n.Id).ToArray();
        var battle = new CatalogBattle(db, tactics, legacy);
        foreach (var m in r.Members)
        {
            if (m.Id == "reserve" && mode != "refilled") continue;
            battle.AddUnit(m.Id, CatalogTeam.Ally, m.FormId, m.BranchId, m.InheritedTraitId,
                m.Id == "reserve" ? rx : m.X, m.Id == "reserve" ? ry : m.Y);
        }
        foreach (var e in encounter.Units.Where(u => u.Team == CatalogTeam.Enemy))
            battle.AddEnemy(e.Id, e.MaxHp, e.Power, e.Defense, e.AttackInterval, e.Range, e.MoveSpeed,
                e.IsBoss, e.X, e.Y, e.EnemyRole, e.BossPatternsEnabled);
        var result = new Result { firstDeath = 75, count = battle.Units.Count(u => u.Team == CatalogTeam.Ally) };
        double openingDamage = 0, healthArea = 0, window = 0;
        battle.Event += e => {
            var a = battle.Get(e.SourceId); var t = battle.Get(e.TargetId);
            if (e.Kind == CatalogEventKind.Defeated && t != null && t.Team == CatalogTeam.Ally) result.firstDeath = Math.Min(result.firstDeath, e.Time);
            if (a == null || a.Team != CatalogTeam.Ally) return;
            if (e.Kind == CatalogEventKind.Damage && t != null && t.Team == CatalogTeam.Enemy) { result.damage += e.Amount; if (e.Time <= 20) openingDamage += e.Amount; }
            if (e.Kind == CatalogEventKind.Heal) result.healing += e.Amount;
            if (e.Kind == CatalogEventKind.ShieldBlocked) result.protection += e.Amount;
            if (e.Kind == CatalogEventKind.SkillUsed && e.AbilityId == row.hybrid) result.casts++;
        };
        while (!battle.Ended && battle.Time < 75)
        {
            float before = battle.Time;
            battle.Step(1f / 60f);
            double dt = Math.Max(0, Math.Min(20, battle.Time) - Math.Min(20, before));
            if (dt > 0) { window += dt; healthArea += dt * Enumerable.Range(0, 3).Average(i => { var u = battle.Get("common-" + i); return u.Hp / u.MaxHp; }); }
        }
        result.win = battle.Winner == CatalogTeam.Ally; result.seconds = battle.Time;
        result.openingDps = openingDamage / Math.Max(.001, window);
        result.openingCommonHealth = healthArea / Math.Max(.001, window);
        var allies = battle.Units.Where(u => u.Team == CatalogTeam.Ally).ToArray();
        result.alive = allies.Count(u => u.IsAlive); result.hpFraction = allies.Sum(u => u.Hp) / allies.Sum(u => u.MaxHp);
        Require(result.count == (mode == "combined" ? 4 : 5), "wrong deployed count");
        return result;
    }
    static void Main(string[] args)
    {
        db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
        var rows = new List<Row>();
        var templates = new Dictionary<int, CatalogBattle>();
        var template = ExpeditionRun.New(db, 43001);
        for (int wave = 1; wave <= 30; wave++)
        {
            templates[wave] = template.BeginBattle(); template.ResolveBattle(true); template.Continue();
        }
        foreach (string hybrid in new[] { "H-05", "H-06" })
        foreach (int wave in Waves)
        foreach (string[] families in hybrid == "H-05" ? SongTeams : CommanderTeams)
        foreach (string a in hybrid == "H-05" ? new[] { "E-BA-G", "E-BA-A" } : new[] { "E-AR-P", "E-AR-T" })
        foreach (string b in hybrid == "H-05" ? new[] { "E-PR-H", "E-PR-G" } : new[] { "E-BA-G", "E-BA-A" })
        foreach (string layout in new[] { "compact", "spread", "forward" })
        foreach (string effects in new[] { "none", "researched" })
        {
            var row = new Row { hybrid = hybrid, wave = wave, branchA = a, branchB = b, layout = layout, effects = effects,
                team = string.Join("+", families), reserve = families.Contains("PR") ? "B-AR" : "B-PR" };
            row.id = string.Join("/", new[] { hybrid, wave.ToString(), row.team, a, b, layout, effects });
            CatalogRoster roster = Roster(row, families); var snapshot = roster.Capture();
            row.separate = Fight(CatalogRoster.Restore(db, snapshot), row, templates[wave], "separate");
            row.combined = Fight(CatalogRoster.Restore(db, snapshot), row, templates[wave], "combined");
            row.refilled = Fight(CatalogRoster.Restore(db, snapshot), row, templates[wave], "refilled");
            if (rows.Count % 211 == 0)
            {
                var repeated = Fight(CatalogRoster.Restore(db, snapshot), row, templates[wave], "refilled");
                Require(new JavaScriptSerializer().Serialize(repeated) == new JavaScriptSerializer().Serialize(row.refilled), "non-deterministic");
            }
            rows.Add(row);
        }
        File.WriteAllText(args[1], new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(new {
            note = "Controlled equal-investment encounter snapshots, not acquisition or human win rates. Reserve is already owned in all arms. Opening metrics stop at min(20s, battle end). Damage includes absorbed shields, excludes overkill; healing is effective HP restored.",
            assertions = assertions, cases = rows.Count, battles = rows.Count * 3, rows = rows.ToArray() }));
        foreach (string h in new[] { "H-05", "H-06" })
        {
            var group = rows.Where(r => r.hybrid == h).ToArray();
            Console.WriteLine(h + " n=" + group.Length + " wins separate/combined/refilled=" + group.Count(r => r.separate.win) + "/" + group.Count(r => r.combined.win) + "/" + group.Count(r => r.refilled.win) +
                " losses-to-wins=" + group.Count(r => !r.separate.win && r.refilled.win) + " wins-to-losses=" + group.Count(r => r.separate.win && !r.refilled.win));
        }
        Console.WriteLine("PASS paired combat: " + assertions + " assertions, " + rows.Count + " cases / " + rows.Count * 3 + " battles.");
    }
}
