using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Expedition.Core;

// Fixed legal strategies, not estimates of human win rates. After a loss, a wave is
// measured counterfactually with the victory reward so later encounters remain comparable.
internal static class HybridBalanceProbe
{
    static bool FrontFirst;
    sealed class Policy
    {
        public string Id, First, Second;
        public bool NeedsBackupPriest;
        public Policy(string id, string first, string second, bool backup = false)
        { Id = id; First = first; Second = second; NeedsBackupPriest = backup; }
    }

    static readonly Policy[] Policies = {
        new Policy("final", null, null),
        new Policy("H-01", "KN", "FI"), new Policy("H-02", "KN", "PR", true),
        new Policy("H-03", "AR", "IC"), new Policy("H-04", "FI", "IC"),
        new Policy("H-05", "BA", "PR", true), new Policy("H-06", "AR", "BA")
    };
    static readonly string[] Formations = { "spread", "compact" };

    sealed class Progress
    {
        public string FirstId, SecondId, BackupPriestId, HybridUnitId;
        public int FirstSeenWave, SecondSeenWave, BackupPriestWave, AcquiredWave;
        public int Summons, Transfers, Evolutions, Combines, GoldSpent, BenchBlocked;
    }

    public sealed class WaveTrace
    {
        public int wave, gold, supply, summons, deployedCount;
        public string[] party, actions;
        public bool win, reached, timedOut;
        public double seconds, healthFraction, hybridDamage, hybridHeal;
        public int survivors, hybridSkills;
    }

    public sealed class Acquisition
    {
        public string policy, formation, finalParty;
        public int seed, firstSeenWave, secondSeenWave, backupPriestWave,
            acquiredWave, summons, transfers, evolutions, combines, goldSpent, benchBlocked;
    }

    public sealed class Summary
    {
        public string policy, formation;
        public int wave, wins, reachWins, timeouts, hybridDeployed;
        public double? meanWinSeconds, meanLossSeconds;
        public double meanHealthFraction, meanSurvivors, meanGoldBeforeBattle,
            meanSupplyBeforeBattle, meanHybridDamage, meanHybridHeal, meanHybridSkills;
    }

    public sealed class PolicySummary
    {
        public string policy, formation;
        public int acquiredBy5, acquiredBy10, acquiredBy15, backupPriestBy15;
        public double meanAcquisitionWave, meanSummons, meanGoldSpent;
    }

    public sealed class SampleTrace
    {
        public string policy, formation;
        public int seed;
        public WaveTrace[] waves;
    }

    public sealed class Report
    {
        public int firstSeed, count;
        public string note, difficulty, growthOrder;
        public PolicySummary[] policySummaries;
        public Summary[] rows;
        public Acquisition[] acquisitions;
        public SampleTrace[] sampleTraces;
    }

    sealed class Totals
    {
        public int Wins, Reach, Timeouts, HybridDeployed;
        public double WinSeconds, LossSeconds, Health, Survivors, Gold, Supply,
            HybridDamage, HybridHeal, HybridSkills;
    }

    sealed class Result
    {
        public Acquisition Acquisition;
        public WaveTrace[] Waves;
    }

    static string Family(CatalogMember member)
    { return member == null ? null : CatalogRoster.FamilyCode(member.FormId); }

    static string Branch(string family)
    {
        switch (family)
        {
            case "KN": return "E-KN-G"; case "AR": return "E-AR-P";
            case "PR": return "E-PR-H"; case "FI": return "E-FI-B";
            case "IC": return "E-IC-F"; case "BA": return "E-BA-G";
            default: throw new InvalidOperationException("Unknown family: " + family);
        }
    }

    static void Discover(ExpeditionRun run, Policy policy, Progress progress, string id)
    {
        if (policy.First == null || progress.AcquiredWave != 0) return;
        CatalogMember member = run.Roster.Find(id);
        string family = Family(member);
        if (family == policy.First && progress.FirstId == null)
        { progress.FirstId = id; progress.FirstSeenWave = run.Wave; }
        else if (family == policy.Second && progress.SecondId == null)
        { progress.SecondId = id; progress.SecondSeenWave = run.Wave; }
        else if (policy.NeedsBackupPriest && family == "PR" &&
            id != progress.FirstId && id != progress.SecondId && progress.BackupPriestId == null)
        { progress.BackupPriestId = id; progress.BackupPriestWave = run.Wave; }
    }

    static bool Reserved(Progress p, string id)
    { return id == p.FirstId || id == p.SecondId || id == p.BackupPriestId || id == p.HybridUnitId; }

    static bool CanGrow(CatalogMember member, bool targetBeforeCombine)
    {
        if (member == null) return false;
        if (member.FormId.StartsWith("B-", StringComparison.Ordinal)) return member.Growth < 2;
        return !targetBeforeCombine && member.FormId.StartsWith("E-", StringComparison.Ordinal) &&
            member.Growth < 6;
    }

    static CatalogMember Recipient(ExpeditionRun run, Policy policy, Progress p, string donor)
    {
        var ordered = new List<string>();
        // Spend only the two transfers for a branch tank before recipe growth.
        // Once it is an E form, the original target-first priorities resume.
        CatalogMember starter = run.Roster.Find("ally-01");
        if (FrontFirst && starter != null && starter.FormId == "B-KN")
            ordered.Add(starter.Id);
        if (policy.First != null && p.AcquiredWave == 0)
        { ordered.Add(p.FirstId); ordered.Add(p.SecondId); }
        if (policy.NeedsBackupPriest) ordered.Add(p.BackupPriestId);
        if (policy.Id == "final") ordered.Add("ally-01");
        foreach (string id in run.DeployedIds)
            if (Family(run.Roster.Find(id)) == "PR" && !ordered.Contains(id)) ordered.Add(id);
        foreach (string id in run.DeployedIds)
            if (Family(run.Roster.Find(id)) == "KN" && !ordered.Contains(id)) ordered.Add(id);
        foreach (string id in run.DeployedIds)
            if (!ordered.Contains(id)) ordered.Add(id);
        foreach (CatalogMember member in run.Roster.Members)
            if (!ordered.Contains(member.Id)) ordered.Add(member.Id);
        foreach (string id in ordered)
        {
            if (id == null || id == donor || id == p.HybridUnitId) continue;
            CatalogMember member = run.Roster.Find(id);
            bool target = p.AcquiredWave == 0 && (id == p.FirstId || id == p.SecondId);
            if (CanGrow(member, target) && run.CanTransfer(donor, id)) return member;
        }
        return null;
    }

    static void Grow(ExpeditionRun run, Policy policy, Progress p, string donor,
        List<string> actions)
    {
        CatalogMember recipient = Recipient(run, policy, p, donor);
        if (recipient == null) return;
        string recipientId = recipient.Id;
        if (!run.Transfer(donor, recipientId, false))
            throw new InvalidOperationException("Legal transfer failed: " + donor + " -> " + recipientId);
        p.Transfers++;
        actions.Add("transfer " + donor + " -> " + recipientId);
        recipient = run.Roster.Find(recipientId);
        if (recipient.FormId.StartsWith("B-", StringComparison.Ordinal) && recipient.Growth >= 2 ||
            recipient.FormId.StartsWith("E-", StringComparison.Ordinal) && recipient.Growth >= 6 &&
                recipientId != p.FirstId && recipientId != p.SecondId)
        {
            string form = recipient.FormId.StartsWith("B-", StringComparison.Ordinal) ?
                Branch(Family(recipient)) : "F-" + Family(recipient);
            if (!run.Evolve(recipientId, form))
                throw new InvalidOperationException("Ready evolution failed: " + recipientId + " -> " + form);
            p.Evolutions++;
            actions.Add("evolve " + recipientId + " -> " + form);
        }
    }

    static string BenchFiller(ExpeditionRun run, Progress p)
    {
        var bench = run.Roster.Members.Where(m => !run.DeployedIds.Contains(m.Id) &&
            m.Id != p.FirstId && m.Id != p.SecondId).ToArray();
        CatalogMember priest = bench.FirstOrDefault(m => m.Id == p.BackupPriestId);
        if (priest != null) return priest.Id;
        CatalogMember any = bench.FirstOrDefault(m => Family(m) == "PR") ??
            bench.FirstOrDefault(m => Family(m) == "KN") ?? bench.FirstOrDefault();
        return any == null ? null : any.Id;
    }

    static string Replaceable(ExpeditionRun run, Progress p)
    {
        foreach (string id in run.DeployedIds.Reverse())
            if (!Reserved(p, id) && Family(run.Roster.Find(id)) != "KN" &&
                Family(run.Roster.Find(id)) != "PR") return id;
        foreach (string id in run.DeployedIds.Reverse())
            if (!Reserved(p, id) && Family(run.Roster.Find(id)) != "KN") return id;
        return run.DeployedIds.Reverse().FirstOrDefault(id => !Reserved(p, id));
    }

    static void EnsureDeployed(ExpeditionRun run, Progress p, string id, List<string> actions)
    {
        if (id == null || run.Roster.Find(id) == null || run.DeployedIds.Contains(id)) return;
        if (run.DeployedIds.Count < 5)
        {
            if (!run.SetDeployed(id, true)) throw new InvalidOperationException("Deploy failed: " + id);
        }
        else
        {
            string outId = Replaceable(run, p);
            if (outId == null) return;
            if (!run.SwapDeployment(outId, id))
                throw new InvalidOperationException("Expected deployment swap failed: " + outId + " -> " + id);
            actions.Add("swap " + outId + " -> " + id);
        }
        actions.Add("deploy " + id);
    }

    static void TryCombine(ExpeditionRun run, Policy policy, Progress p, List<string> actions)
    {
        if (policy.First == null || p.AcquiredWave != 0 || p.FirstId == null || p.SecondId == null)
            return;
        CatalogMember first = run.Roster.Find(p.FirstId), second = run.Roster.Find(p.SecondId);
        if (first == null || second == null || !first.FormId.StartsWith("E-", StringComparison.Ordinal) ||
            !second.FormId.StartsWith("E-", StringComparison.Ordinal)) return;
        if (policy.NeedsBackupPriest && (p.BackupPriestId == null ||
            run.Roster.Find(p.BackupPriestId) == null)) return;
        // Do not turn a full five-person party into four with no way to refill it.
        bool bothActive = run.DeployedIds.Contains(first.Id) && run.DeployedIds.Contains(second.Id);
        if (bothActive && BenchFiller(run, p) == null && run.Gold < ExpeditionRun.SummonCost)
            return;
        if (!run.CanCombine(first.Id, second.Id, policy.Id))
            throw new InvalidOperationException("Ready hybrid recipe was rejected: " + policy.Id);
        if (!run.Combine(first.Id, second.Id, policy.Id))
            throw new InvalidOperationException("Ready hybrid combine failed: " + policy.Id);
        p.HybridUnitId = first.Id;
        p.AcquiredWave = run.Wave;
        p.Combines++;
        actions.Add("combine " + first.Id + "+" + second.Id + " -> " + policy.Id);
        EnsureDeployed(run, p, p.HybridUnitId, actions);
        if (policy.NeedsBackupPriest) EnsureDeployed(run, p, p.BackupPriestId, actions);
        while (run.DeployedIds.Count < 5)
        {
            string fill = BenchFiller(run, p);
            if (fill == null) break;
            EnsureDeployed(run, p, fill, actions);
        }
    }

    static void Prepare(ExpeditionRun run, Policy policy, Progress p, string formation,
        List<string> actions)
    {
        foreach (CatalogMember member in run.Roster.Members) Discover(run, policy, p, member.Id);
        TryCombine(run, policy, p, actions);
        while (true)
        {
            if (!run.CanSummon)
            {
                if (run.Gold < ExpeditionRun.SummonCost) break;
                CatalogMember donorMember = run.Roster.Members.FirstOrDefault(m =>
                    !run.DeployedIds.Contains(m.Id) && !Reserved(p, m.Id) &&
                    Recipient(run, policy, p, m.Id) != null);
                string donor = donorMember == null ? null : donorMember.Id;
                if (donor == null) { p.BenchBlocked++; break; }
                Grow(run, policy, p, donor, actions);
                if (!run.CanSummon) { p.BenchBlocked++; break; }
            }
            string id = run.Summon();
            if (id == null) throw new InvalidOperationException("Affordable summon failed.");
            p.Summons++; p.GoldSpent += ExpeditionRun.SummonCost;
            actions.Add("summon " + id + ":" + run.Roster.Find(id).FormId);
            Discover(run, policy, p, id);
            if (run.DeployedIds.Count < 5) EnsureDeployed(run, p, id, actions);
            else if (!Reserved(p, id)) Grow(run, policy, p, id, actions);
            TryCombine(run, policy, p, actions);
        }
        TryCombine(run, policy, p, actions);
        if (p.AcquiredWave != 0)
        {
            EnsureDeployed(run, p, p.HybridUnitId, actions);
            if (policy.NeedsBackupPriest) EnsureDeployed(run, p, p.BackupPriestId, actions);
        }
        Place(run, formation);
    }

    static bool Front(CatalogMember member)
    { return Family(member) == "KN" || member.FormId == "H-01" || member.FormId == "H-02"; }

    static void Place(ExpeditionRun run, string formation)
    {
        int front = 0, back = 0, mid = 0;
        float[] spreadFront = { 0f, -1.7f, 1.7f, -.85f, .85f };
        float[] compactFront = { -.45f, .45f, -1.25f, 1.25f, 0f };
        float[] spreadBack = { 1.5f, -1.5f, .5f, -.5f, 0f };
        float[] compactBack = { -1.1f, 0f, 1.1f, -1.8f, 1.8f };
        foreach (string id in run.DeployedIds.ToArray())
        {
            CatalogMember member = run.Roster.Find(id);
            float x, y;
            if (Front(member))
            { x = formation == "spread" ? -1.4f : -1.5f;
                y = formation == "spread" ? spreadFront[front++] : compactFront[front++]; }
            else if (member.FormId == "H-05")
            { x = formation == "spread" ? -2.45f : -2.05f; y = mid++ == 0 ? .2f : -.2f; }
            else
            { x = formation == "spread" ? -3.3f : -2.6f;
                y = formation == "spread" ? spreadBack[back++] : compactBack[back++]; }
            if (Math.Abs(x) > 6.4f || Math.Abs(y) > 2.35f)
                throw new InvalidOperationException("Formation escaped combat bounds.");
            if ((member.X != x || member.Y != y) && !run.Move(id, x, y))
                throw new InvalidOperationException("Formation move failed: " + id);
        }
        var positions = run.DeployedIds.Select(id => run.Roster.Find(id)).ToArray();
        for (int i = 0; i < positions.Length; i++)
            for (int j = i + 1; j < positions.Length; j++)
                if (positions[i].X == positions[j].X && positions[i].Y == positions[j].Y)
                    throw new InvalidOperationException("Formation stacked two deployed units.");
    }

    static string Party(ExpeditionRun run)
    {
        return string.Join(" | ", run.DeployedIds.Select(id => {
            CatalogMember member = run.Roster.Find(id);
            return id + ":" + member.FormId + "(g" + member.Growth + "," +
                member.X.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                member.Y.ToString("0.00", CultureInfo.InvariantCulture) + ")";
        }).ToArray());
    }

    static Result Simulate(CatalogDatabase db, int seed, Policy policy, string formation)
    {
        var run = ExpeditionRun.New(db, (uint)seed);
        var p = new Progress();
        var traces = new WaveTrace[ExpeditionRun.TotalWaves];
        bool unbroken = true;
        for (int w = 0; w < traces.Length; w++)
        {
            var actions = new List<string>();
            Prepare(run, policy, p, formation, actions);
            ExpeditionRunSave snapshot = run.Capture();
            var restored = ExpeditionRun.Restore(db, snapshot);
            if (restored.Difficulty != ExpeditionDifficulty.Normal || restored.ContentWaves != run.ContentWaves ||
                restored.Wave != run.Wave || restored.Gold != run.Gold ||
                restored.Roster.SupplyUsed != run.Roster.SupplyUsed ||
                Party(restored) != Party(run) || restored.Capture().RandomState != snapshot.RandomState)
                throw new InvalidOperationException("Preparation capture/restore drifted.");
            run = restored;
            var trace = new WaveTrace { wave = run.Wave, gold = run.Gold,
                supply = run.Roster.SupplyUsed, summons = p.Summons,
                deployedCount = run.DeployedIds.Count, party = run.DeployedIds.Select(id =>
                    run.Roster.Find(id).FormId).ToArray(), actions = actions.ToArray() };
            string hybridActor = p.HybridUnitId;
            CatalogBattle battle = run.BeginBattle();
            if (battle == null) throw new InvalidOperationException("Legal preparation could not begin battle.");
            if (hybridActor != null && run.DeployedIds.Contains(hybridActor))
                battle.Event += e => {
                    if (e.SourceId != hybridActor) return;
                    if (e.Kind == CatalogEventKind.Damage) trace.hybridDamage += e.Amount;
                    if (e.Kind == CatalogEventKind.Heal) trace.hybridHeal += e.Amount;
                    if (e.Kind == CatalogEventKind.SkillUsed && e.AbilityId == policy.Id)
                        trace.hybridSkills++;
                };
            while (!battle.Ended && battle.Time < 75f) battle.Step(.25f);
            trace.win = battle.Ended && battle.Winner == CatalogTeam.Ally;
            if (!trace.win) unbroken = false;
            trace.reached = unbroken;
            trace.timedOut = !battle.Ended;
            trace.seconds = Math.Round(battle.Time, 3);
            var allies = battle.Units.Where(u => u.Team == CatalogTeam.Ally).ToArray();
            trace.survivors = allies.Count(u => u.IsAlive);
            trace.healthFraction = allies.Length == 0 ? 0 :
                Math.Round(allies.Average(u => Math.Max(0, u.Hp) / u.MaxHp), 4);
            traces[w] = trace;
            // Isolated later waves assume prior victories; reached remains strictly unbroken.
            if (!run.ResolveBattle(true) || !run.Continue())
                throw new InvalidOperationException("Counterfactual progression failed.");
        }
        return new Result { Acquisition = new Acquisition { policy = policy.Id,
            formation = formation, seed = seed, firstSeenWave = p.FirstSeenWave,
            secondSeenWave = p.SecondSeenWave, backupPriestWave = p.BackupPriestWave,
            acquiredWave = p.AcquiredWave, summons = p.Summons, transfers = p.Transfers,
            evolutions = p.Evolutions, combines = p.Combines, goldSpent = p.GoldSpent,
            benchBlocked = p.BenchBlocked, finalParty = Party(run) }, Waves = traces };
    }

    static string Fingerprint(Result result)
    {
        var a = result.Acquisition;
        return a.firstSeenWave + "/" + a.secondSeenWave + "/" + a.backupPriestWave + "/" +
            a.acquiredWave + "/" + a.summons + "/" + a.transfers + "/" + a.evolutions + "/" +
            a.combines + "/" + a.goldSpent + "/" + a.finalParty + "/" +
            string.Join(";", result.Waves.Select(w => w.wave + ":" + w.gold + ":" + w.supply +
                ":" + w.win + ":" + w.seconds + ":" + w.survivors + ":" + w.healthFraction +
                ":" + w.hybridDamage + ":" + w.hybridHeal +
                ":" + w.hybridSkills + ":" + string.Join(",", w.party)).ToArray());
    }

    static void Main(string[] args)
    {
        if (args.Length != 4 && args.Length != 5)
            throw new ArgumentException("catalog report firstSeed count [TargetFirst|FrontFirst] required");
        if (args.Length == 5)
        {
            if (args[4] != "TargetFirst" && args[4] != "FrontFirst")
                throw new ArgumentException("Unsupported growth order");
            FrontFirst = args[4] == "FrontFirst";
        }
        int first = int.Parse(args[2]), count = int.Parse(args[3]);
        if (first < 1 || count < 1 || count > 1000 || (long)first + count > int.MaxValue)
            throw new ArgumentOutOfRangeException("seed range");
        CatalogDatabase db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
        var report = new Report { firstSeed = first, count = count,
            difficulty = "Normal", growthOrder = FrontFirst ? "FrontFirst" : "TargetFirst",
            note = "Fixed legal policies with the same seed and summon economy. A later wave assumes preceding rewards after losses; reachWins counts only uninterrupted victories. meanWinSeconds and meanLossSeconds are separate. These are not human win rates. H-02/H-05 wait for a replacement priest before consuming one. Ability damage includes shields blocked; heal is actual restored HP." };
        var summaries = new List<Summary>();
        var policySummaries = new List<PolicySummary>();
        var acquisitions = new List<Acquisition>();
        var samples = new List<SampleTrace>();
        foreach (Policy policy in Policies)
            foreach (string formation in Formations)
            {
                var totals = Enumerable.Range(0, ExpeditionRun.TotalWaves).Select(_ => new Totals()).ToArray();
                var records = new List<Acquisition>();
                for (int seed = first; seed < first + count; seed++)
                {
                    Result result = Simulate(db, seed, policy, formation);
                    if (seed == first)
                    {
                        Result repeated = Simulate(db, seed, policy, formation);
                        if (Fingerprint(result) != Fingerprint(repeated))
                            throw new InvalidOperationException("Same-seed simulation was not deterministic.");
                        samples.Add(new SampleTrace { policy = policy.Id, formation = formation,
                            seed = seed, waves = result.Waves });
                    }
                    acquisitions.Add(result.Acquisition);
                    records.Add(result.Acquisition);
                    for (int w = 0; w < result.Waves.Length; w++)
                    {
                        WaveTrace x = result.Waves[w]; Totals t = totals[w];
                        if (x.win) { t.Wins++; t.WinSeconds += x.seconds; }
                        else { t.LossSeconds += x.seconds; }
                        if (x.reached) t.Reach++;
                        if (x.timedOut) t.Timeouts++;
                        t.Gold += x.gold; t.Supply += x.supply;
                        t.Health += x.healthFraction; t.Survivors += x.survivors;
                        t.HybridDamage += x.hybridDamage; t.HybridHeal += x.hybridHeal;
                        t.HybridSkills += x.hybridSkills;
                        if (result.Acquisition.acquiredWave != 0 &&
                            result.Acquisition.acquiredWave <= x.wave &&
                            x.party.Contains(policy.Id)) t.HybridDeployed++;
                    }
                }
                for (int w = 0; w < totals.Length; w++)
                {
                    Totals t = totals[w];
                    summaries.Add(new Summary { policy = policy.Id, formation = formation,
                        wave = w + 1, wins = t.Wins, reachWins = t.Reach, timeouts = t.Timeouts,
                        hybridDeployed = t.HybridDeployed,
                        meanWinSeconds = t.Wins == 0 ? (double?)null : Math.Round(t.WinSeconds / t.Wins, 2),
                        meanLossSeconds = t.Wins == count ? (double?)null :
                            Math.Round(t.LossSeconds / (count - t.Wins), 2),
                        meanGoldBeforeBattle = Math.Round(t.Gold / count, 2),
                        meanSupplyBeforeBattle = Math.Round(t.Supply / count, 2),
                        meanHealthFraction = Math.Round(t.Health / count, 3),
                        meanSurvivors = Math.Round(t.Survivors / count, 2),
                        meanHybridDamage = Math.Round(t.HybridDamage / count, 2),
                        meanHybridHeal = Math.Round(t.HybridHeal / count, 2),
                        meanHybridSkills = Math.Round(t.HybridSkills / count, 2) });
                }
                var got = records.Where(a => a.acquiredWave > 0).ToArray();
                policySummaries.Add(new PolicySummary { policy = policy.Id, formation = formation,
                    acquiredBy5 = got.Count(a => a.acquiredWave <= 5),
                    acquiredBy10 = got.Count(a => a.acquiredWave <= 10),
                    acquiredBy15 = got.Length, backupPriestBy15 = records.Count(a => a.backupPriestWave > 0),
                    meanAcquisitionWave = got.Length == 0 ? 0 : Math.Round(got.Average(a => a.acquiredWave), 2),
                    meanSummons = Math.Round(records.Average(a => a.summons), 2),
                    meanGoldSpent = Math.Round(records.Average(a => a.goldSpent), 2) });
                Console.WriteLine(policy.Id + "/" + formation + " acquired=" + got.Length + "/" + count +
                    " wave10=" + totals[9].Wins + "/" + count +
                    " wave15=" + totals[14].Wins + "/" + count);
            }
        report.policySummaries = policySummaries.ToArray();
        report.rows = summaries.ToArray();
        report.acquisitions = acquisitions.ToArray();
        report.sampleTraces = samples.ToArray();
        var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        File.WriteAllText(args[1], json.Serialize(report));
    }
}
