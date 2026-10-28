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
    static string starter = "standard";
    static string researchBranch = "defense";
    static string rulesProfile = "current";
    static bool interruptions;
    static int boundaryChecks;
    static string context;
    static readonly JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };

    static void Verify(bool value, string message)
    {
        boundaryChecks++;
        if (!value) throw new InvalidOperationException(message);
    }

    // Reflection/property enumeration order is not part of the save contract.
    // Preserve array order and values, but compare JSON objects by sorted keys.
    static object SortJson(object value)
    {
        var map = value as IDictionary<string, object>;
        if (map != null) return new SortedDictionary<string, object>(
            map.ToDictionary(pair => pair.Key, pair => SortJson(pair.Value)), StringComparer.Ordinal);
        var items = value as object[];
        return items == null ? value : items.Select(SortJson).ToArray();
    }

    static string StableJson(object value)
    {
        return serializer.Serialize(SortJson(serializer.DeserializeObject(serializer.Serialize(value))));
    }

    static ExpeditionRun JsonReload(CatalogDatabase db, ExpeditionRun run)
    {
        string json = StableJson(run.Capture());
        var restored = ExpeditionRun.Restore(db, serializer.Deserialize<ExpeditionRunSave>(json));
        string after = StableJson(restored.Capture());
        int difference = 0;
        while (difference < Math.Min(json.Length, after.Length) && json[difference] == after[difference]) difference++;
        Verify(after == json, "JSON reload changed state at " + run.Phase + " offset=" + difference +
            " before=" + json.Substring(difference, Math.Min(140, json.Length - difference)) +
            " after=" + after.Substring(difference, Math.Min(140, after.Length - difference)));
        return restored;
    }

    static string BattleFingerprint(CatalogBattle battle)
    {
        return battle.Ended + "/" + battle.Winner + "/" + battle.Time.ToString("R", CultureInfo.InvariantCulture) + "/" +
            string.Join(";", battle.Units.Select(u => u.Id + ":" + u.Hp.ToString("R", CultureInfo.InvariantCulture) +
                ":" + u.Shield.ToString("R", CultureInfo.InvariantCulture)).ToArray());
    }
    sealed class Attempt
    {
        public int wave, lanternBefore, gold, supply, deployed, seconds;
        public bool win, timedOut;
        public string party, formation;
        public string[] tactics, actions;
        public double allyHpFraction, damage, heal, hybridDamage, hybridHeal, hybridProtected;
        public int hybridSkills;
        public string firstAllyDefeated;
        public Dictionary<string, double> incomingBySource, incomingByTarget, outgoingBySource,
            healingBySource, protectedBySource;
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
        public string note, starter, growthOrder, researchBranch, rulesProfile;
        public bool interruptions;
        public int boundaryChecks;
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
        string[] path = researchBranch == "precision" ? new[] { "marksmanship", "exploitation", "bulwark" } :
            researchBranch == "arcana" ? new[] { "spellcraft", "cinders", "bulwark" } :
            new[] { "bulwark", "shieldcraft", "marksmanship" };
        foreach (string id in path)
        {
            if (!run.CanPurchaseTactic(id)) continue;
            if (!run.PurchaseTactic(id)) throw new InvalidOperationException("Affordable tactic failed.");
            actions.Add("tactic " + id);
            if (spending == "delayed") break;
        }
    }

    static void AddDamage(Dictionary<string, double> totals, string id, double amount)
    {
        if (id == null) return;
        double previous;
        totals.TryGetValue(id, out previous);
        totals[id] = previous + amount;
    }

    static SeedResult Simulate(CatalogDatabase db, int seed,
        HybridBalanceProbe.Policy policy, string spending, string account)
    {
        // Archived account inputs never grant new-run power.
        string[] legacy = account == "researched" ?
            CatalogLegacyResearch.Nodes.Select(n => n.Id).ToArray() : account == "unlocked" ?
            CatalogLegacyResearch.Nodes.Where(n => n.Tier == 1).Select(n => n.Id).ToArray() : new string[0];
        var run = ExpeditionRun.New(db, (uint)seed, ExpeditionDifficulty.Normal, legacy, starter);
        if (rulesProfile == "v045" || rulesProfile == "v046")
        {
            var previous = run.Capture(); previous.RulesVersion = rulesProfile == "v045" ? 1 : 2;
            previous.Version = previous.RulesVersion + 8;
            run = ExpeditionRun.Restore(db, previous);
        }
        var accountProgress = ExpeditionProgress.New();
        var journal = ExpeditionJournal.New();
        var progress = new HybridBalanceProbe.Progress();
        var attempts = new List<Attempt>();
        int firstHybridWave = 0, retryWins = 0;
        while (run.Phase == ExpeditionPhase.Preparation)
        {
            int wave = run.Wave, lantern = run.Lantern;
            context = account + "/" + starter + "/" + policy.Id + "/" + spending + "/seed=" + seed + "/wave=" + wave + "/lantern=" + lantern;
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
                !reloaded.LegacyResearchIds.SequenceEqual(run.UsesLegacyRules ? legacy : new string[0]) || reloaded.StartFormationId != starter ||
                HybridBalanceProbe.Party(reloaded) != HybridBalanceProbe.Party(run))
                throw new InvalidOperationException("Run save changed legal preparation.");
            run = reloaded;
            if (interruptions) run = JsonReload(db, run);
            var attempt = new Attempt { wave = wave, lanternBefore = lantern,
                formation = formation, gold = run.Gold,
                supply = run.Roster.SupplyUsed, deployed = run.DeployedIds.Count,
                party = HybridBalanceProbe.Party(run), actions = actions.ToArray(),
                tactics = run.TacticIds.ToArray(),
                incomingBySource = new Dictionary<string, double>(),
                incomingByTarget = new Dictionary<string, double>(),
                outgoingBySource = new Dictionary<string, double>(),
                healingBySource = new Dictionary<string, double>(),
                protectedBySource = new Dictionary<string, double>() };
            bool hasHybrid = run.DeployedIds.Any(id => run.Roster.Find(id).FormId == policy.Id);
            var battle = run.BeginBattle();
            if (battle == null) throw new InvalidOperationException("Legal battle could not begin.");
            CatalogBattle uninterrupted = null;
            if (interruptions)
            {
                uninterrupted = ExpeditionRun.Restore(db, run.Capture()).ResumeBattle();
                battle.Step(1.37f);
                run = JsonReload(db, run);
                battle = run.ResumeBattle();
                Verify(battle.Time == 0f, "Interrupted battle must restart from saved preparation");
                Verify(BattleFingerprint(battle) == BattleFingerprint(uninterrupted), "Restart changed initial actors");
                string beforeInvalid = StableJson(run.Capture());
                Verify(run.Summon() == null && !run.Continue() && !run.PurchaseTactic("bulwark") &&
                    StableJson(run.Capture()) == beforeInvalid, "Battle rejected actions mutated state");
            }
            battle.Event += e => {
                if (run.Wave >= 23 && e.Kind == CatalogEventKind.Damage)
                {
                    if (e.SourceId != null && e.TargetId != null &&
                        e.SourceId.StartsWith("ally-", StringComparison.Ordinal))
                        AddDamage(attempt.outgoingBySource, e.SourceId, e.Amount);
                    else if (e.SourceId != null && e.TargetId != null &&
                        e.TargetId.StartsWith("ally-", StringComparison.Ordinal))
                    {
                        AddDamage(attempt.incomingBySource, e.SourceId, e.Amount);
                        AddDamage(attempt.incomingByTarget, e.TargetId, e.Amount);
                    }
                }
                if (run.Wave >= 23 && e.Kind == CatalogEventKind.Defeated &&
                    e.TargetId != null && e.TargetId.StartsWith("ally-", StringComparison.Ordinal) &&
                    attempt.firstAllyDefeated == null) attempt.firstAllyDefeated = e.TargetId;
                if (run.Wave >= 23 && e.SourceId != null &&
                    e.SourceId.StartsWith("ally-", StringComparison.Ordinal))
                {
                    if (e.Kind == CatalogEventKind.Heal)
                        AddDamage(attempt.healingBySource, e.SourceId, e.Amount);
                    if (e.Kind == CatalogEventKind.ShieldBlocked)
                        AddDamage(attempt.protectedBySource, e.SourceId, e.Amount);
                }
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
            // Match the game's 60 Hz timeout boundary, including the last partial batch.
            while (!battle.Ended && battle.Time < 75f) battle.Step(battle.Time < 74.75f ? .25f : 1f / 60f);
            if (uninterrupted != null)
            {
                while (!uninterrupted.Ended && uninterrupted.Time < 75f) uninterrupted.Step(1f / 60f);
                Verify(BattleFingerprint(battle) == BattleFingerprint(uninterrupted), "Resume or step batching changed battle outcome");
            }
            attempt.win = battle.Ended && battle.Winner == CatalogTeam.Ally;
            attempt.timedOut = !battle.Ended;
            attempt.seconds = (int)Math.Round(battle.Time);
            var allies = battle.Units.Where(u => u.Team == CatalogTeam.Ally).ToArray();
            attempt.allyHpFraction = Math.Round(allies.Average(u => Math.Max(0, u.Hp) / u.MaxHp), 3);
            attempts.Add(attempt);
            int goldBefore = run.Gold, lanternBefore = run.Lantern;
            if (!run.ResolveBattle(attempt.win)) throw new InvalidOperationException("Resolve rejected result.");
            if (interruptions)
            {
                Verify(run.Gold == goldBefore + run.LastReward && (attempt.win || run.LastReward == 0) &&
                    run.Lantern == lanternBefore - (attempt.win ? 0 : 1), "Result currency or lantern mismatch");
                run = JsonReload(db, run);
                string beforeDuplicate = StableJson(run.Capture());
                Verify(!run.ResolveBattle(attempt.win) && StableJson(run.Capture()) == beforeDuplicate,
                    "Duplicate result paid rewards or spent lantern twice");
                accountProgress.Observe(run); journal.Observe(run);
                Verify(!accountProgress.Observe(run) && !journal.Observe(run), "Progress or story awarded twice");
                accountProgress = ExpeditionProgress.Restore(serializer.Deserialize<ExpeditionProgressSave>(serializer.Serialize(accountProgress.Capture())));
                journal = ExpeditionJournal.Restore(serializer.Deserialize<ExpeditionJournalSave>(serializer.Serialize(journal.Capture())));
                Verify(accountProgress.BestWave == run.Victories &&
                    accountProgress.ResearchEarned == 0, "Fresh progress awarded retired permanent research points");
            }
            if (run.LastVictory != attempt.win || !run.Continue())
                throw new InvalidOperationException("Result progression failed.");
            if (interruptions)
            {
                run = JsonReload(db, run);
                string beforeDuplicate = StableJson(run.Capture());
                Verify(!run.Continue() && StableJson(run.Capture()) == beforeDuplicate, "Duplicate Continue advanced twice");
            }
            if (attempt.win && lantern < 3) retryWins++;
            if (run.Phase != ExpeditionPhase.Preparation) break;
        }
        int wins = attempts.Count(a => a.win);
        if (run.Phase == ExpeditionPhase.Completed && wins < ExpeditionRun.TotalWaves)
            throw new InvalidOperationException("Completed without real victories.");
        if (interruptions)
        {
            Verify(run.Phase == ExpeditionPhase.Completed || run.Phase == ExpeditionPhase.Defeated, "Run did not terminate");
            Verify(accountProgress.HardUnlocked == (run.Phase == ExpeditionPhase.Completed), "Clear eligibility disagrees with actual completion");
            string terminal = StableJson(run.Capture());
            Verify(run.BeginBattle() == null && run.Summon() == null && !run.Undo() &&
                StableJson(run.Capture()) == terminal, "Terminal run accepts gameplay actions");
        }
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
        try { Execute(args); }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAILED " + context + ": " + error.GetType().Name + ": " + error.Message);
            Environment.ExitCode = 1;
        }
    }

    static void Execute(string[] args)
    {
        if (args.Length < 5 || args.Length > 12)
            throw new ArgumentException("catalog report firstSeed count growthOrder [spending] [policy] [account] [starter] [interruptions] required");
        starter = args.Length >= 9 ? args[8] : "standard";
        interruptions = args.Length >= 10 && bool.Parse(args[9]);
        researchBranch = args.Length >= 11 ? args[10] : "defense";
        rulesProfile = args.Length >= 12 ? args[11] : "current";
        if (rulesProfile != "current" && rulesProfile != "v045" && rulesProfile != "v046") throw new ArgumentException("Unknown rules profile.");
        if (researchBranch != "defense" && researchBranch != "precision" && researchBranch != "arcana")
            throw new ArgumentException("Unsupported research branch.");
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
        if (accountFilter != "all" && accountFilter != "fresh" && accountFilter != "researched" && accountFilter != "unlocked")
            throw new ArgumentException("Unsupported account profile.");
        var all = new List<SeedResult>();
        var rows = new List<Row>();
        var waves = new List<WaveRow>();
        foreach (string account in new[] { "fresh", "researched", "unlocked" })
        foreach (var policy in HybridBalanceProbe.Policies)
            foreach (string spending in new[] { "none", "early", "delayed", "selective" })
            {
                if (filter != "all" && spending != filter) continue;
                if (accountFilter != "all" && account != accountFilter) continue;
                if (accountFilter == "all" && account == "unlocked") continue;
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
        var report = new Report { firstSeed = first, count = count, starter = starter, growthOrder = args[4], researchBranch = researchBranch, rulesProfile = rulesProfile,
            interruptions = interruptions, boundaryChecks = boundaryChecks,
            note = "Real run progression only. Losses consume lantern; only wins pay rewards. Growth order: " +
                args[4] + ". Account labels describe archived legacy inputs only; new expeditions ignore all permanent power and starter unlocks. Starter: " + starter + ". Fixed automated policies, not human win rates. Trace beyond first seed keeps losses only. Wave rates are conditional on reaching the wave.",
            rows = rows.ToArray(), waves = waves.ToArray(), seedResults = all.ToArray() };
        File.WriteAllText(args[1], new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(report));
    }
}
