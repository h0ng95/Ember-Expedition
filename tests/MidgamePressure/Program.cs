using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Expedition.Core;

// Comparisons freeze legal acquisition to normal-052. Explicit production mode runs
// normal-067 end-to-end. Isolated candidates never feed rewards into progression.
static class MidgamePressureProbe
{
    public sealed class Outcome
    {
        public string candidate, firstLost, topEnemyAbility;
        public bool win, timeout;
        public float seconds, health, bossHp, enemyHp, firstLossAt = -1, incoming, burn, enemyShield;
        public int survivors;
        public object[] contributions;
        public object[] remainingEnemies;
    }
    public sealed class Anchor
    {
        public int seed, wave, gold;
        public string starter, policy, branch, spending, party;
        public string[] tactics;
        public Outcome[] outcomes;
    }
    public sealed class Route
    {
        public int seed, victories, stoppedAt;
        public string starter, policy, branch, spending;
        public bool complete;
    }
    static readonly MethodInfo place = typeof(HybridBalanceProbe).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static);
    static int fights;
    static Outcome Fight(CatalogBattle battle, string candidate, bool detail)
    {
        fights++;
        var o = new Outcome { candidate = candidate };
        var incoming = new Dictionary<string, float>();
        var report = detail ? new CatalogBattleReport(battle) : null;
        battle.Event += e => {
            var source = battle.Get(e.SourceId); var target = battle.Get(e.TargetId);
            if (e.Kind == CatalogEventKind.Defeated && target != null && target.Team == CatalogTeam.Ally && o.firstLossAt < 0)
            { o.firstLossAt = battle.Time; o.firstLost = target.FormId; }
            if (e.Kind == CatalogEventKind.Shield && target != null && target.Team == CatalogTeam.Enemy) o.enemyShield += e.Amount;
            if (e.Kind != CatalogEventKind.Damage || source == null || source.Team != CatalogTeam.Enemy || target == null || target.Team != CatalogTeam.Ally) return;
            o.incoming += e.Amount; if (e.AbilityId == "BURN") o.burn += e.Amount;
            string id = source.Id + "/" + e.AbilityId;
            if (!incoming.ContainsKey(id)) incoming.Add(id, 0);
            incoming[id] += e.Amount;
        };
        while (!battle.Ended && battle.Time < 75f) battle.Step(battle.Time < 74.75f ? .25f : 1f / 60f);
        var allies = battle.Units.Where(u => u.Team == CatalogTeam.Ally).ToArray();
        var enemies = battle.Units.Where(u => u.Team == CatalogTeam.Enemy).ToArray();
        var boss = enemies.FirstOrDefault(u => u.IsBoss);
        o.win = battle.Ended && battle.Winner == CatalogTeam.Ally; o.timeout = !battle.Ended; o.seconds = battle.Time;
        o.health = allies.Average(u => Math.Max(0, u.Hp) / u.MaxHp); o.survivors = allies.Count(u => u.IsAlive);
        o.enemyHp = enemies.Sum(u => Math.Max(0,u.Hp)); o.bossHp = boss == null ? 0 : Math.Max(0,boss.Hp) / boss.MaxHp;
        if (detail) o.remainingEnemies = enemies.Where(u => u.IsAlive).Select(u => (object)new { id=u.Id,hp=u.Hp }).ToArray();
        o.topEnemyAbility = incoming.OrderByDescending(p => p.Value).Select(p => p.Key).FirstOrDefault();
        if (report != null)
        {
            o.contributions = report.Allies.Select(a => (object)new { form=a.FormId, damage=a.Damage, heal=a.Healing, shield=a.Protected }).ToArray();
            report.Dispose();
        }
        return o;
    }
    static void Modify(CatalogBattle battle, string candidate)
    {
        var boss = battle.Units.FirstOrDefault(u => u.IsBoss);
        if (candidate == "furnace-hp95" || candidate == "furnace-hp90")
        { boss.MaxHp *= candidate.EndsWith("95") ? .95f : .9f; boss.Hp = boss.MaxHp; }
        foreach (var unit in battle.Units.Where(u => u.Team == CatalogTeam.Enemy && u.Id.StartsWith("frost-acolyte")))
        {
            if (candidate == "acolyte-power95") unit.Power *= .95f;
            if (candidate == "acolyte-power90") unit.Power *= .9f;
            if (candidate == "acolyte-interval22") unit.AttackInterval = 2.2f;
        }
    }
    static void Main(string[] args)
    {
        var db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
        int first = int.Parse(args[2]), count = int.Parse(args[3]);
        if (first < 1 || count < 1 || count > 12) throw new ArgumentException("seed count");
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        var anchors = new List<Anchor>(); var routes = new List<Route>(); int actual = 0;
        string output = Path.GetDirectoryName(args[1]); Directory.CreateDirectory(output);
        string growthOrder = args.Length > 4 ? args[4] : "Balanced";
        bool selectedOnly = args.Length > 5 && args[5] == "selected";
        bool production = args.Length > 5 && args[5] == "production";
        int rules = production ? 6 : 5;
        HybridBalanceProbe.ConfigureGrowthOrder(growthOrder);
        foreach (string starter in new[] { "standard", "tactical", "ember", "frost" })
        foreach (var policy in HybridBalanceProbe.Policies)
        foreach (string branch in new[] { "defense", "precision", "arcana" })
        foreach (string spending in new[] { "early", "delayed" })
        for (int seed = first; seed < first + count; seed++)
        {
            var initial = ExpeditionRun.New(db, (uint)seed, startFormationId: starter).Capture();
            initial.Version = rules + 8; initial.RulesVersion = rules;
            var run = ExpeditionRun.Restore(db, initial); var progress = new HybridBalanceProbe.Progress(); var seen = new HashSet<int>();
            string[] path = branch == "defense" ? new[] { "bulwark", "shieldcraft", "marksmanship" } :
                branch == "precision" ? new[] { "marksmanship", "exploitation", "bulwark" } : new[] { "spellcraft", "cinders", "bulwark" };
            while (run.Phase == ExpeditionPhase.Preparation)
            {
                if (run.Wave >= (spending == "early" ? 6 : 11)) foreach (string id in path)
                    if (run.CanPurchaseTactic(id)) { run.PurchaseTactic(id); if (spending == "delayed") break; }
                HybridBalanceProbe.Prepare(run, policy, progress, run.Lantern == 2 ? "compact" : "spread", new List<string>());
                var snapshot = run.Capture(); Anchor anchor = null; var outcomes = new List<Outcome>();
                if (new[] { 15, 18, 19 }.Contains(run.Wave) && seen.Add(run.Wave))
                {
                    anchor = new Anchor { seed=seed,wave=run.Wave,gold=run.Gold,starter=starter,policy=policy.Id,branch=branch,spending=spending,
                        party=HybridBalanceProbe.Party(run),tactics=run.TacticIds.ToArray() };
                    string[] candidates = production ? new string[0] : selectedOnly ? new[] { "adopted" } : run.Wave == 15 ? new[] { "compact", "spread", "furnace-hp95", "furnace-hp90" } :
                        new[] { "compact", "spread", "acolyte-power95", "acolyte-power90", "acolyte-interval22" };
                    foreach (string candidate in candidates)
                    {
                        var candidateSave = snapshot;
                        if (candidate == "adopted")
                        { candidateSave = run.Capture(); candidateSave.RulesVersion = 6; candidateSave.Version = 14; }
                        var copy = ExpeditionRun.Restore(db, candidateSave);
                        if (candidate == "compact" || candidate == "spread") place.Invoke(null, new object[] { copy, candidate });
                        var battle = copy.BeginBattle(); Modify(battle, candidate);
                        outcomes.Add(Fight(battle, candidate, true));
                    }
                }
                var current = Fight(run.BeginBattle(), "current", anchor != null); actual++;
                if (anchor != null)
                {
                    outcomes.Insert(0, current); anchor.outcomes = outcomes.ToArray(); anchors.Add(anchor);
                    string reason = current.timeout ? "timeout" : current.win ? "win" : "defeat";
                    string fixture = Path.Combine(output, "legal-" + run.Wave + "-" + reason + ".json");
                    if (!File.Exists(fixture)) File.WriteAllText(fixture, serializer.Serialize(snapshot));
                }
                run.ResolveBattle(current.win); run.Continue();
            }
            routes.Add(new Route { seed=seed,starter=starter,policy=policy.Id,branch=branch,spending=spending,
                victories=run.Victories,stoppedAt=run.Victories==30?0:run.Wave,complete=run.Victories==30 });
            if (routes.Count % 84 == 0) Console.WriteLine("Progress " + routes.Count + " legal routes, " + anchors.Count + " reached comparisons");
        }
        var summary = anchors.GroupBy(a => a.wave).Select(g => new { wave=g.Key,n=g.Count(),
            candidates=g.First().outcomes.Select(o => new { candidate=o.candidate,
                wins=g.Count(a => a.outcomes.Single(r => r.candidate==o.candidate).win),
                timeouts=g.Count(a => a.outcomes.Single(r => r.candidate==o.candidate).timeout),
                gained=g.Count(a => !a.outcomes[0].win && a.outcomes.Single(r => r.candidate==o.candidate).win),
                lost=g.Count(a => a.outcomes[0].win && !a.outcomes.Single(r => r.candidate==o.candidate).win) }).ToArray() }).ToArray();
        File.WriteAllText(args[1], serializer.Serialize(new { firstSeed=first,seedsPerPolicy=count,growthOrder,selectedOnly,baselineRules=CatalogBalance.Version(rules),actualBattles=actual,
            isolatedBattles=fights-actual,progressionRules=CatalogBalance.Version(rules),production,note="Legal baseline progression. Paired candidates change only placement or named enemy stats. Fixed policies share seeds; not human win rates. Damage includes shields; shields generated may expire unused.",routes,anchors,summary }));
        Console.WriteLine(serializer.Serialize(summary));
        Console.WriteLine("PASS " + routes.Count + " legal routes; " + actual + " progression and " + (fights-actual) + " isolated battles.");
    }
}
