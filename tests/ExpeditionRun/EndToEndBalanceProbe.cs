using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Expedition.Core;

// Real ExpeditionRun progression: losses spend lanterns, never award victory gold,
// and only Continue decides whether a run can retry or has ended.
internal static class EndToEndBalanceProbe
{
    sealed class Attempt
    {
        public int wave, lanternBefore, gold, supply, deployed, seconds;
        public bool win, timedOut;
        public string party, formation;
        public string[] tactics, actions;
        public double allyHpFraction, damage, heal, hybridDamage, hybridHeal, hybridProtected;
        public int hybridSkills;
    }

    sealed class SeedResult
    {
        public string account, policy, spending;
        public int seed, defeatedAt, attempts, wins, firstHybridWave, finalGold, finalSupply;
        public bool complete;
        public Attempt[] trace;
    }

    sealed class Row
    {
        public string account, policy, spending;
        public int complete, completedWithHybrid, completedWithoutHybrid,
            reach10, reach15, reach20, reach25, reach29, reach30;
        public int losses, timeouts, acquiredHybrid;
        public double meanGoldAtEnd, meanSupplyAtEnd, meanPartyAtEnd,
            meanHybridDamage, meanHybridHeal, meanHybridProtected, meanDamage, meanHeal;
        public int[] stoppedAt;
    }

    sealed class WaveRow
    {
        public string account, policy, spending;
        public int wave, reached, firstWins, attempts, wins, timeouts;
        public double meanSeconds, meanHealthFraction;
    }

    sealed class Report
    {
        public int firstSeed, count;
        public string note;
        public Row[] rows;
        public WaveRow[] waves;
        public SeedResult[] seedResults;
    }

    static void BuyTactics(ExpeditionRun run, HybridBalanceProbe.Policy policy,
        string spending, List<string> actions)
    {
        if (spending == "none") return;
        if (spending == "early" && run.Wave < 6 ||
            spending == "delayed" && run.Wave < 11) return;
        if (spending == "selective")
        {
            if (run.Wave < 6 || run.DeployedIds.Count < 5 || run.TacticIds.Count > 0) return;
            // One role-matched base node after the party is staffed, rather than
            // buying every available node before recruiting/growing key allies.
            string id = policy.Id == "H-03" || policy.Id == "H-06" ? "marksmanship" :
                policy.Id == "H-01" || policy.Id == "H-04" ? "spellcraft" : "bulwark";
            if (run.CanPurchaseTactic(id))
            {
                if (!run.PurchaseTactic(id)) throw new InvalidOperationException("Affordable tactic failed.");
                actions.Add("tactic " + id);
            }
            return;
        }
        foreach (string id in new[] { "bulwark", "shieldcraft", "marksmanship" })
        {
            if (!run.CanPurchaseTactic(id)) continue;
            if (!run.PurchaseTactic(id)) throw new InvalidOperationException("Affordable tactic failed.");
            actions.Add("tactic " + id);
            if (spending == "delayed") break;
        }
    }

    static SeedResult Simulate(CatalogDatabase db, int seed,
        HybridBalanceProbe.Policy policy, string spending, string account)
    {
        // Same standard starter in both accounts isolates permanent research effects.
        string[] legacy = account == "researched" ?
            CatalogLegacyResearch.Nodes.Select(n => n.Id).ToArray() : new string[0];
        var run = ExpeditionRun.New(db, (uint)seed, ExpeditionDifficulty.Normal, legacy, "standard");
        var progress = new HybridBalanceProbe.Progress();
        var attempts = new List<Attempt>();
        int firstHybridWave = 0, retryWins = 0;
        while (run.Phase == ExpeditionPhase.Preparation)
        {
            int wave = run.Wave, lantern = run.Lantern;
            string formation = lantern == 2 ? "compact" : "spread";
            var actions = new List<string>();
            BuyTactics(run, policy, spending, actions);
            HybridBalanceProbe.Prepare(run, policy, progress, formation, actions);
            if (firstHybridWave == 0 && run.DeployedIds.Any(id => run.Roster.Find(id).FormId == policy.Id))
                firstHybridWave = wave;
            ExpeditionRunSave saved = run.Capture();
            var reloaded = ExpeditionRun.Restore(db, saved);
            if (reloaded.Wave != run.Wave || reloaded.Gold != run.Gold ||
                reloaded.Capture().RandomState != saved.RandomState ||
                !reloaded.TacticIds.SequenceEqual(run.TacticIds) ||
                !reloaded.LegacyResearchIds.SequenceEqual(legacy) || reloaded.StartFormationId != "standard" ||
                HybridBalanceProbe.Party(reloaded) != HybridBalanceProbe.Party(run))
                throw new InvalidOperationException("Run save changed legal preparation.");
            run = reloaded;
            var attempt = new Attempt { wave = wave, lanternBefore = lantern,
                formation = formation, gold = run.Gold,
                supply = run.Roster.SupplyUsed, deployed = run.DeployedIds.Count,
                party = HybridBalanceProbe.Party(run), actions = actions.ToArray(),
                tactics = run.TacticIds.ToArray() };
            bool hasHybrid = run.DeployedIds.Any(id => run.Roster.Find(id).FormId == policy.Id);
            var battle = run.BeginBattle();
            if (battle == null) throw new InvalidOperationException("Legal battle could not begin.");
            battle.Event += e => {
                if (e.SourceId == null || !e.SourceId.StartsWith("ally-", StringComparison.Ordinal)) return;
                if (e.Kind == CatalogEventKind.Damage) attempt.damage += e.Amount;
                if (e.Kind == CatalogEventKind.Heal) attempt.heal += e.Amount;
                if (hasHybrid && e.SourceId == progress.HybridUnitId)
                {
                    if (e.Kind == CatalogEventKind.Damage) attempt.hybridDamage += e.Amount;
                    if (e.Kind == CatalogEventKind.Heal) attempt.hybridHeal += e.Amount;
                    if (e.Kind == CatalogEventKind.ShieldBlocked) attempt.hybridProtected += e.Amount;
                    if (e.Kind == CatalogEventKind.SkillUsed) attempt.hybridSkills++;
                }
            };
            while (!battle.Ended && battle.Time < 75f) battle.Step(.25f);
            attempt.win = battle.Ended && battle.Winner == CatalogTeam.Ally;
            attempt.timedOut = !battle.Ended;
            attempt.seconds = (int)Math.Round(battle.Time);
            var allies = battle.Units.Where(u => u.Team == CatalogTeam.Ally).ToArray();
            attempt.allyHpFraction = Math.Round(allies.Average(u => Math.Max(0, u.Hp) / u.MaxHp), 3);
            attempts.Add(attempt);
            if (!run.ResolveBattle(attempt.win)) throw new InvalidOperationException("Resolve rejected result.");
            if (run.LastVictory != attempt.win || !run.Continue())
                throw new InvalidOperationException("Result progression failed.");
            if (attempt.win && lantern < 3) retryWins++;
            if (run.Phase != ExpeditionPhase.Preparation) break;
        }
        int wins = attempts.Count(a => a.win);
        if (run.Phase == ExpeditionPhase.Completed && wins < ExpeditionRun.TotalWaves)
            throw new InvalidOperationException("Completed without real victories.");
        return new SeedResult { account = account, policy = policy.Id, spending = spending, seed = seed,
            complete = run.Phase == ExpeditionPhase.Completed,
            defeatedAt = run.Phase == ExpeditionPhase.Defeated ? run.Wave : 0,
            attempts = attempts.Count, wins = wins, firstHybridWave = firstHybridWave,
            finalGold = run.Gold, finalSupply = run.Roster.SupplyUsed,
            trace = attempts.ToArray() };
    }

    static string Fingerprint(SeedResult result)
    {
        return result.complete + "/" + result.defeatedAt + "/" + result.finalGold + "/" +
            string.Join(";", result.trace.Select(x => x.wave + ":" + x.win + ":" +
                x.gold + ":" + x.supply + ":" + x.party + ":" + x.damage.ToString("R", CultureInfo.InvariantCulture)).ToArray());
    }

    static void Main(string[] args)
    {
        if (args.Length < 5 || args.Length > 8)
            throw new ArgumentException("catalog report firstSeed count growthOrder [spending] [policy] [account] required");
        int first = int.Parse(args[2]), count = int.Parse(args[3]);
        if (first < 1 || count < 1 || count > 1000) throw new ArgumentOutOfRangeException("seed range");
        HybridBalanceProbe.ConfigureGrowthOrder(args[4]);
        string filter = args.Length >= 6 ? args[5] : "all";
        if (filter != "all" && filter != "none" && filter != "early" &&
            filter != "delayed" && filter != "selective")
            throw new ArgumentException("Unsupported tactic spending policy.");
        var db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
        string policyFilter = args.Length >= 7 ? args[6] : "all";
        if (policyFilter != "all" && policyFilter != "focus" && !HybridBalanceProbe.Policies.Any(p => p.Id == policyFilter))
            throw new ArgumentException("Unsupported policy filter.");
        string accountFilter = args.Length >= 8 ? args[7] : "fresh";
        if (accountFilter != "all" && accountFilter != "fresh" && accountFilter != "researched")
            throw new ArgumentException("Unsupported account profile.");
        var all = new List<SeedResult>();
        var rows = new List<Row>();
        var waves = new List<WaveRow>();
        foreach (string account in new[] { "fresh", "researched" })
        foreach (var policy in HybridBalanceProbe.Policies)
            foreach (string spending in new[] { "none", "early", "delayed", "selective" })
            {
                if (filter != "all" && spending != filter) continue;
                if (accountFilter != "all" && account != accountFilter) continue;
                if (policyFilter == "focus")
                { if (policy.Id != "final" && policy.Id != "H-01" && policy.Id != "H-05") continue; }
                else if (policyFilter != "all" && policy.Id != policyFilter) continue;
                var results = new List<SeedResult>();
                for (int seed = first; seed < first + count; seed++)
                {
                    SeedResult result = Simulate(db, seed, policy, spending, account);
                    if (seed == first && Fingerprint(result) != Fingerprint(Simulate(db, seed, policy, spending, account)))
                        throw new InvalidOperationException("Same seed changed outcome.");
                    results.Add(result);
                    all.Add(result);
                }
                Func<int, int> reach = wave => results.Count(r => r.complete || r.defeatedAt >= wave);
                rows.Add(new Row { account = account, policy = policy.Id, spending = spending,
                    complete = results.Count(r => r.complete), reach10 = reach(10),
                    completedWithHybrid = results.Count(r => r.complete && r.firstHybridWave > 0),
                    completedWithoutHybrid = results.Count(r => r.complete && r.firstHybridWave == 0),
                    reach15 = reach(15), reach20 = reach(20), reach25 = reach(25),
                    reach29 = reach(29), reach30 = reach(30),
                    losses = results.Sum(r => r.attempts - r.wins),
                    timeouts = results.Sum(r => r.trace.Count(a => a.timedOut)),
                    acquiredHybrid = results.Count(r => r.firstHybridWave > 0),
                    meanGoldAtEnd = Math.Round(results.Average(r => r.finalGold), 1),
                    meanSupplyAtEnd = Math.Round(results.Average(r => r.finalSupply), 1),
                    meanPartyAtEnd = Math.Round(results.Average(r => r.trace.Last().deployed), 2),
                    meanHybridDamage = Math.Round(results.Average(r => r.trace.Sum(a => a.hybridDamage)), 1),
                    meanHybridHeal = Math.Round(results.Average(r => r.trace.Sum(a => a.hybridHeal)), 1),
                    meanHybridProtected = Math.Round(results.Average(r => r.trace.Sum(a => a.hybridProtected)), 1),
                    meanDamage = Math.Round(results.Average(r => r.trace.Sum(a => a.damage)), 1),
                    meanHeal = Math.Round(results.Average(r => r.trace.Sum(a => a.heal)), 1),
                    stoppedAt = results.GroupBy(r => r.defeatedAt).OrderBy(g => g.Key)
                        .SelectMany(g => new[] { g.Key, g.Count() }).ToArray() });
                Console.WriteLine(account + "/" + policy.Id + "/" + spending + " complete=" + rows.Last().complete + "/" + count);
                for (int wave = 1; wave <= ExpeditionRun.TotalWaves; wave++)
                {
                    var reached = results.Where(r => r.trace.Any(a => a.wave == wave)).ToArray();
                    var attempts = reached.SelectMany(r => r.trace.Where(a => a.wave == wave)).ToArray();
                    waves.Add(new WaveRow { account = account, policy = policy.Id, spending = spending,
                        wave = wave, reached = reached.Length, attempts = attempts.Length,
                        firstWins = reached.Count(r => r.trace.First(a => a.wave == wave).win),
                        wins = attempts.Count(a => a.win), timeouts = attempts.Count(a => a.timedOut),
                        meanSeconds = attempts.Length == 0 ? 0 : Math.Round(attempts.Average(a => a.seconds), 2),
                        meanHealthFraction = attempts.Length == 0 ? 0 : Math.Round(attempts.Average(a => a.allyHpFraction), 3) });
                }
                // Keep full trace for the first seed; other seeds retain bounded diagnostics.
                foreach (SeedResult result in results)
                    if (result.seed != first) result.trace = result.trace.Where(a => !a.win).ToArray();
            }
        var report = new Report { firstSeed = first, count = count,
            note = "Real run progression only. Losses consume lantern; only wins pay rewards. Growth order: " +
                args[4] + ". Accounts: fresh has no permanent research, researched has all six nodes (27 points). Both start standard. Fixed automated policies, not human win rates. Trace beyond first seed keeps losses only. Wave rates are conditional on reaching the wave.",
            rows = rows.ToArray(), waves = waves.ToArray(), seedResults = all.ToArray() };
        File.WriteAllText(args[1], new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(report));
    }
}
