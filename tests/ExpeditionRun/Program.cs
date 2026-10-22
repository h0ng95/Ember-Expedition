using System;
using System.IO;
using System.Linq;
using Expedition.Core;

internal static class Program
{
    static CatalogDatabase catalog;
    static int assertions;

    static int Main(string[] args)
    {
        try
        {
            catalog = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
            InitialStateAndSupply();
            CapacityAndGoldBoundaries();
            GoldBudgetPath();
            SummonSequenceAndRestore();
            PreparationUndo();
            GrowthAndCombination();
            BattleResumeAndProgression();
            EnemyWavesAndRoles();
            BeastChargeAndRangedAttack();
            OpeningWavesPlayable();
            DefeatRules();
            RejectBadSaves();
            CompareDraftFights();
            Console.WriteLine("PASS: expedition run, " + assertions + " assertions.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error); return 1; }
    }

    static void Check(bool value, string message)
    {
        assertions++;
        if (!value) throw new Exception(message);
    }

    static void InitialStateAndSupply()
    {
        var run = ExpeditionRun.New(catalog, 14);
        Check(run.Phase == ExpeditionPhase.Preparation && run.Wave == 1 && run.Lantern == 3 &&
            run.Gold == 40 && ExpeditionRun.SummonCost == 20 && run.SummonsRemaining == 2 &&
            run.Roster.SupplyUsed == 3 && run.DeployedIds.Count == 3,
            "First preparation has starter team, forty gold, and three lanterns.");
        Check(run.Roster.Find("ally-01").FormId == "B-KN" &&
            run.Roster.Find("ally-02").FormId == "B-AR" &&
            run.Roster.Find("ally-03").FormId == "B-PR", "Starter roles are fixed.");
        Check(run.IsBossWave == false, "First battle is ordinary.");
        for (int wave = 1; wave <= ExpeditionRun.TotalWaves; wave++)
        {
            Check(run.Wave == wave && run.IsBossWave == (wave == 5 || wave == 10),
                "Only waves five and ten are bosses.");
            Check(!string.IsNullOrEmpty(run.EncounterName) && !run.EncounterName.Contains("FOREST"),
                "Encounter name is ready for Korean UI.");
            Check(run.BeginBattle() != null && run.ResolveBattle(true) && run.Continue(), "Wave advances once.");
        }
        Check(run.Gold == 290 && run.Roster.SupplyUsed == 3 && run.Phase == ExpeditionPhase.Completed &&
            run.Victories == 10 && run.LastReward == 0,
            "Ten-wave unspent economy earns 250 gold and final clear grants zero.");
        Check(run.Summon() == null && run.BeginBattle() == null && !run.Continue(), "Completed run is closed.");
    }

    static void FundSaveForWaveTen(ExpeditionRunSave save)
    {
        save.Wave = 10;
        save.Victories = 9;
        save.RewardedWave = 9;
        save.Gold = 290 - 20 * (save.Roster.SupplyUsed - 3);
        save.LastReward = 35;
    }

    static void CapacityAndGoldBoundaries()
    {
        var run = ExpeditionRun.New(catalog, 33);
        string first = run.Summon(), second = run.Summon();
        Check(first != null && second != null && run.Gold == 0 && !run.CanSummon,
            "Two opening purchases spend exactly forty gold.");
        var before = run.Capture();
        Check(run.Summon() == null && run.Gold == 0 && run.Capture().RandomState == before.RandomState,
            "No-gold summon leaves money and RNG unchanged.");

        var save = ExpeditionRun.New(catalog, 44).Capture();
        var roster = CatalogRoster.Restore(catalog, save.Roster);
        for (int i = 4; i <= 10; i++) roster.Recruit("ally-" + i.ToString("00"), "B-PR", -2f, 0f);
        save.Roster = roster.Capture(); save.NextMemberNumber = 10;
        FundSaveForWaveTen(save);
        run = ExpeditionRun.Restore(catalog, save);
        before = run.Capture();
        Check(!run.CanSummon && run.Gold >= 20 && run.Summon() == null &&
            run.Gold == before.Gold && run.Capture().RandomState == before.RandomState,
            "Full bench rejects summon without consuming gold or RNG.");
        Check(!run.SetDeployed("ally-01", false), "Cannot bench an active member when bench has seven.");
        Check(run.SetDeployed("ally-04", true) && run.CanSummon && run.Summon() != null,
            "Deploying a bench member opens exactly one summon place.");

        save = ExpeditionRun.New(catalog, 45).Capture();
        roster = CatalogRoster.Restore(catalog, save.Roster);
        for (int i = 4; i <= 12; i++) roster.Recruit("ally-" + i.ToString("00"), "B-PR", -2f, 0f);
        save.Roster = roster.Capture(); save.NextMemberNumber = 12;
        save.DeployedIds = new[] { "ally-01", "ally-02", "ally-03", "ally-04", "ally-05" };
        FundSaveForWaveTen(save);
        run = ExpeditionRun.Restore(catalog, save);
        Check(run.Transfer("ally-06", "ally-01", false), "Transfer frees a full bench place.");
        int goldBefore = run.Gold;
        string purchased = run.Summon();
        Check(purchased != null && run.Gold == goldBefore - 20 && !run.CanUndo && !run.Undo() &&
            run.Roster.Find("ally-06") == null && run.Roster.Find(purchased) != null,
            "Undo rejects restored donor when later nonrefundable summon fills bench.");
        run = ExpeditionRun.Restore(catalog, run.Capture());
        Check(!run.CanUndo && !run.Undo(), "Capacity-blocked Undo remains blocked after restore.");

        roster = new CatalogRoster(catalog);
        for (int i = 1; i <= 17; i++)
            Check(roster.Recruit("many-" + i, "B-PR", 0f, 0f), "Cumulative supply no longer stops at fourteen.");
        Check(CatalogRoster.Restore(catalog, roster.Capture()).SupplyUsed == 17,
            "Roster saves preserve supply above fourteen.");
    }

    static void GoldBudgetPath()
    {
        int[] supplied = { 5, 6, 7, 8, 9, 12, 13, 14, 15, 17 };
        int[] remaining = { 0, 0, 0, 0, 10, 0, 5, 10, 15, 10 };
        var run = ExpeditionRun.New(catalog, 555);
        for (int wave = 1; wave <= ExpeditionRun.TotalWaves; wave++)
        {
            while (run.Gold >= ExpeditionRun.SummonCost)
            {
                if (!run.CanSummon) FreeBenchPlace(run);
                string id = run.Summon();
                Check(id != null, "Budgeted summon succeeds after opening bench space.");
                if (wave == 1) Check(run.SetDeployed(id, true), "Opening two summons join the active five.");
            }
            Check(run.Roster.SupplyUsed == supplied[wave - 1] && run.Gold == remaining[wave - 1],
                "Gold budget and cumulative supply match the ten-wave plan at wave " + wave + ".");
            var restored = ExpeditionRun.Restore(catalog, run.Capture());
            Check(restored.Gold == run.Gold && restored.Roster.SupplyUsed == run.Roster.SupplyUsed,
                "Saved economy retains gold and cumulative summons.");
            run.BeginBattle(); run.ResolveBattle(true); run.Continue();
        }
        Check(run.Phase == ExpeditionPhase.Completed && run.Roster.SupplyUsed == 17 && run.Gold == 10,
            "Fourteen paid summons and three starters survive the full budget path.");
    }

    static void FreeBenchPlace(ExpeditionRun run)
    {
        foreach (string activeId in run.DeployedIds.ToArray())
        {
            CatalogMember receiver = run.Roster.Find(activeId);
            if (receiver.FormId.StartsWith("B-") && receiver.Growth == 2)
            {
                string branchPrefix = "E-" + CatalogRoster.FamilyCode(receiver.FormId) + "-";
                string branch = catalog.All.First(form => form.Id.StartsWith(branchPrefix)).Id;
                Check(run.Evolve(activeId, branch), "Growth-ready unit can branch for more donors.");
            }
            CatalogMember candidate = run.Roster.Members.FirstOrDefault(member => !run.DeployedIds.Contains(member.Id));
            string donor = candidate == null ? null : candidate.Id;
            if (donor != null && run.Transfer(donor, activeId, false)) return;
        }
        throw new Exception("No valid growth recipient available to open bench space.");
    }

    static void SummonSequenceAndRestore()
    {
        for (uint seed = 1; seed <= 100; seed++)
        {
            var a = ExpeditionRun.New(catalog, seed);
            var b = ExpeditionRun.Restore(catalog, a.Capture());
            string previous = null;
            int streak = 0;
            for (int wave = 1; wave <= ExpeditionRun.TotalWaves; wave++)
            {
                while (a.CanSummon)
                {
                    string idA = a.Summon(), idB = b.Summon();
                    string familyA = CatalogRoster.FamilyCode(a.Roster.Find(idA).FormId);
                    string familyB = CatalogRoster.FamilyCode(b.Roster.Find(idB).FormId);
                    Check(idA == idB && familyA == familyB, "Restored RNG yields the same summon.");
                    Check(streak < 3 || familyA != previous, "Fourth consecutive summon excludes prior family.");
                    streak = familyA == previous ? streak + 1 : 1;
                    previous = familyA;
                    b = ExpeditionRun.Restore(catalog, b.Capture());
                }
                if (wave < ExpeditionRun.TotalWaves)
                {
                    a.BeginBattle(); b.BeginBattle(); a.ResolveBattle(true); b.ResolveBattle(true);
                    a.Continue(); b.Continue();
                }
            }
        }
    }

    static void PreparationUndo()
    {
        var run = ExpeditionRun.New(catalog, 24);
        Check(run.Move("ally-01", -3f, 1f), "Preparation move succeeds.");
        string summoned = run.Summon();
        Check(run.Transfer("ally-02", "ally-01", true) && run.Undo(), "Transfer can be undone.");
        Check(run.Roster.Find("ally-02") != null && run.Roster.Find(summoned) != null &&
            run.Roster.SupplyUsed == 4 && run.Gold == 20,
            "Undo restores donor but retains later summon and spent gold.");
        Check(run.Undo() && run.Roster.Find(summoned) != null &&
            run.Roster.Find("ally-01").X == -1.3f,
            "Older undo retains summon while restoring position.");
        Check(!run.Undo(), "A summon itself is never an undo entry.");
        Check(run.SetDeployed(summoned, true) && run.DeployedIds.Count == 4 && run.Undo() &&
            run.DeployedIds.Count == 3, "Deployment changes can be undone.");
        Check(run.SwapDeployment("ally-01", summoned) && run.DeployedIds.Contains(summoned) &&
            run.Undo() && run.DeployedIds.Contains("ally-01"), "Active/bench swap can be undone.");
        Check(run.BeginBattle() != null && !run.Undo() && run.Gold == 20,
            "Battle commits preparation history and carries unspent gold.");
    }

    static void GrowthAndCombination()
    {
        var run = ExpeditionRun.New(catalog, 3);
        var save = run.Capture();
        // A full supply is available in later preparation; construct an owned-roster save
        // to exercise the real run API without bypassing its transfer/evolution rules.
        var roster = CatalogRoster.Restore(catalog, save.Roster);
        string[] forms = { "B-FI", "B-PR", "B-AR", "B-IC", "B-BA", "B-PR" };
        for (int i = 0; i < forms.Length; i++) roster.Recruit("ally-" + (i + 4).ToString("00"), forms[i], -2f, 0f);
        save.Roster = roster.Capture();
        save.NextMemberNumber = 9;
        FundSaveForWaveTen(save);
        run = ExpeditionRun.Restore(catalog, save);
        Check(run.Transfer("ally-04", "ally-01", true) && run.Transfer("ally-05", "ally-01", false) &&
            run.Evolve("ally-01", "E-KN-C"), "Two donors grow and branch a knight.");
        Check(run.Transfer("ally-06", "ally-02", false) && run.Transfer("ally-07", "ally-02", false) &&
            run.Evolve("ally-02", "E-AR-P"), "Two donors grow and branch an archer.");
        Check(!run.Combine("ally-01", "ally-02", "H-01"), "Invalid family recipe is rejected.");
        Check(run.Transfer("ally-08", "ally-04", false) == false, "Consumed donor cannot receive growth.");
        Check(run.Undo() && run.Roster.Find("ally-02").FormId == "B-AR", "Branch choice is reversible.");

        var fusion = ExpeditionRun.New(catalog, 4);
        var fs = fusion.Capture();
        var fr = CatalogRoster.Restore(catalog, fs.Roster);
        fr.Recruit("ally-04", "B-FI", -2f, 0f);
        for (int i = 5; i <= 8; i++) fr.Recruit("ally-" + i.ToString("00"), "B-IC", -2f, 0f);
        fs.Roster = fr.Capture(); fs.NextMemberNumber = 8;
        FundSaveForWaveTen(fs);
        fusion = ExpeditionRun.Restore(catalog, fs);
        Check(fusion.Transfer("ally-05", "ally-01", false) && fusion.Transfer("ally-06", "ally-01", false) &&
            fusion.Evolve("ally-01", "E-KN-C") && fusion.Transfer("ally-07", "ally-04", false) &&
            fusion.Transfer("ally-08", "ally-04", false) && fusion.Evolve("ally-04", "E-FI-E") &&
            fusion.Combine("ally-01", "ally-04", "H-01"), "Valid branch pair combines through run API.");
        Check(fusion.Roster.Find("ally-01").FormId == "H-01" && fusion.Roster.Find("ally-04") == null &&
            fusion.Undo() && fusion.Roster.Find("ally-04") != null,
            "Combination consumes other branch and Undo restores it.");
    }

    static void BattleResumeAndProgression()
    {
        var run = ExpeditionRun.New(catalog, 11);
        var first = run.BeginBattle();
        float hp = first.Get("ally-01").Hp;
        first.Step(8f);
        Check(first.Get("ally-01").Hp <= hp, "Combat state progresses after starting.");
        var resumedRun = ExpeditionRun.Restore(catalog, run.Capture());
        var resumed = resumedRun.ResumeBattle();
        Check(resumed != first && resumed.Time == 0f && resumed.Get("ally-01").Hp == hp &&
            resumed.Get("ally-01").MaxHp == hp, "Battle restore restarts from full-HP deployment.");
        resumed.Step(2f);
        var replayed = resumedRun.ResumeBattle();
        Check(!object.ReferenceEquals(resumed, replayed) && replayed.Time == 0f &&
            replayed.Get("ally-01").Hp == hp,
            "Continuing from the hub restarts even a cached battle at full health.");
        Check(resumedRun.ResolveBattle(true) && !resumedRun.ResolveBattle(true) && resumedRun.Continue() &&
            resumedRun.Wave == 2 && resumedRun.Gold == 60 && resumedRun.LastReward == 20,
            "Result is idempotent and reward is credited once.");
    }

    static void EnemyWavesAndRoles()
    {
        var run = ExpeditionRun.New(catalog, 31);
        int[] expected = { 2, 3, 4, 4, 3, 3, 4, 5, 6, 3 };
        for (int wave = 1; wave <= ExpeditionRun.TotalWaves; wave++)
        {
            Check(!string.IsNullOrEmpty(run.EncounterDescription), "Preparation describes the upcoming enemy group.");
            CatalogBattle battle = run.BeginBattle();
            var enemies = battle.Units.Where(u => u.Team == CatalogTeam.Enemy).ToArray();
            Check(enemies.Length == expected[wave - 1], "Wave has its requested number of enemies.");
            Check(enemies.All(u => u.EnemyRole != CatalogEnemyRole.None), "Run enemies expose their roles.");
            Check(enemies.Select(u => u.X.ToString("0.00") + "," + u.Y.ToString("0.00")).Distinct().Count() == enemies.Length,
                "Enemies start at distinct positions.");
            CatalogBattle replay = ExpeditionRun.Restore(catalog, run.Capture()).ResumeBattle();
            var replayEnemies = replay.Units.Where(u => u.Team == CatalogTeam.Enemy).ToArray();
            Check(enemies.Zip(replayEnemies, (a, b) => a.Id == b.Id && a.EnemyRole == b.EnemyRole &&
                a.X == b.X && a.Y == b.Y && a.Hp == b.Hp).All(x => x),
                "Saved battle replays the same enemy composition and positions.");
            run.ResolveBattle(true);
            run.Continue();
        }
    }

    static void BeastChargeAndRangedAttack()
    {
        var battle = new CatalogBattle(catalog);
        battle.AddUnit("knight", CatalogTeam.Ally, "B-KN", null, null, -1.3f, 0f);
        CatalogBattleUnit beast = battle.AddEnemy("beast", 1000f, 5f, 0f, 2f, 1f, .6f,
            false, 2.1f, 0f, CatalogEnemyRole.Beast);
        int telegraphs = 0, hits = 0;
        battle.Event += e => { if (e.Kind == CatalogEventKind.ChargeTelegraph) telegraphs++;
            if (e.Kind == CatalogEventKind.ChargeHit) hits++; };
        battle.Step(1.7f);
        Check(telegraphs == 1 && beast.ChargeWarningRemaining > 0f &&
            beast.AttackTargetId == "knight" && beast.AttackWindupRemaining > 0f,
            "Beast exposes a targeted charge warning before release: " + telegraphs + ", " +
            beast.ChargeWarningRemaining + ", " + beast.AttackTargetId + ", " + beast.AttackWindupRemaining);
        float fixedAimX = beast.ChargeTargetX, fixedAimY = beast.ChargeTargetY;
        for (int i = 0; i < 50 && !beast.IsCharging; i++) battle.Step(.02f);
        Check(beast.IsCharging && beast.ChargeWarningRemaining == 0f && hits == 0 &&
            beast.ChargeTargetX == fixedAimX && beast.ChargeTargetY == fixedAimY,
            "Charge movement starts only after warning at the fixed announced point.");
        float windupEndX = beast.X;
        battle.Step(.08f);
        Check(beast.IsCharging && beast.X < windupEndX && hits == 0,
            "Charge visibly moves over ticks without early impact.");
        battle.Step(.2f);
        Check(hits == 1 && beast.ChargeWarningRemaining == 0f && beast.X < 2.1f,
            "Charge hits only after its moving phase finishes.");
        battle.Step(3f);
        Check(telegraphs == 1, "Charge respects its cooldown.");

        var slowedBattle = new CatalogBattle(catalog);
        slowedBattle.AddUnit("ice", CatalogTeam.Ally, "B-IC", null, null, -1.3f, 0f);
        CatalogBattleUnit slowed = slowedBattle.AddEnemy("beast", 1000f, 5f, 0f, 2f, 1f, .6f,
            false, 2.1f, 0f, CatalogEnemyRole.Beast);
        slowedBattle.Step(1.7f);
        Check(slowed.SlowPercent > 0f && slowed.ChargeWarningRemaining > 0f,
            "Ice slow applies during beast charge warning.");
        for (int i = 0; i < 50 && !slowed.IsCharging; i++) slowedBattle.Step(.02f);
        Check(slowed.IsCharging, "Slowed beast still enters charge movement.");
        float before = slowed.X;
        float slow = slowed.SlowPercent;
        slowedBattle.Step(.25f);
        Check(before - slowed.X <= 2.65f * (1f - slow) + .1f,
            "Slow limits charge travel.");

        var ranged = new CatalogBattle(catalog);
        ranged.AddUnit("knight", CatalogTeam.Ally, "B-KN", null, null, -1f, 0f);
        CatalogBattleUnit raider = ranged.AddEnemy("raider", 500f, 7f, 0f, 1.2f, 3.6f, .5f,
            false, 1.8f, 0f, CatalogEnemyRole.Raider);
        int rangedHits = 0;
        ranged.Event += e => { if (e.Kind == CatalogEventKind.BasicHit && e.SourceId == "raider") rangedHits++; };
        ranged.Step(1f);
        Check(rangedHits > 0 && raider.X == 1.8f, "Raider attacks from range instead of closing for melee.");
    }

    static void OpeningWavesPlayable()
    {
        var run = ExpeditionRun.New(catalog, 1);
        for (int wave = 1; wave <= 2; wave++)
        {
            CatalogBattle battle = run.BeginBattle();
            for (int second = 0; second < 75 && !battle.Ended; second++) battle.Step(1f);
            Check(battle.Ended && battle.Winner == CatalogTeam.Ally,
                "Starter trio can clear opening wave " + wave + " within the time limit.");
            run.ResolveBattle(true);
            run.Continue();
        }
    }

    static void DefeatRules()
    {
        var run = ExpeditionRun.New(catalog, 17);
        for (int i = 1; i <= 3; i++)
        {
            run.BeginBattle();
            Check(run.ResolveBattle(false) && run.Lantern == 3 - i && run.Phase == ExpeditionPhase.Result,
                "Normal defeat costs one lantern.");
            run.Continue();
        }
        Check(run.Phase == ExpeditionPhase.Defeated && run.Wave == 1 && run.Gold == 40,
            "Three failures on same wave end run without reward.");
        run = ExpeditionRun.New(catalog, 19);
        for (int i = 1; i < 5; i++) { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
        run.BeginBattle(); run.ResolveBattle(false); run.Continue();
        Check(run.Phase == ExpeditionPhase.Preparation && run.Wave == 5 && run.Lantern == 2 &&
            run.Victories == 4 && run.Gold == 130,
            "First boss loss costs one lantern and retries wave five without reward.");
        run.BeginBattle(); run.ResolveBattle(true); run.Continue();
        Check(run.Wave == 6 && run.Gold == 180 && run.Victories == 5,
            "Boss reward is credited once on retry victory.");
        for (int i = 6; i < 10; i++) { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
        run.BeginBattle(); run.ResolveBattle(false); run.Continue();
        Check(run.Phase == ExpeditionPhase.Preparation && run.Wave == 10 && run.Lantern == 1 &&
            run.Gold == 290, "Final boss loss also retries same wave without reward.");
    }

    static void RejectBadSaves()
    {
        var save = ExpeditionRun.New(catalog, 1).Capture();
        save.Version = 99;
        bool rejected = false;
        try { ExpeditionRun.Restore(catalog, save); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Unknown save version is rejected.");
        save.Version = 1;
        rejected = false;
        try { ExpeditionRun.Restore(catalog, save); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Version-one six-wave save requires explicit new-run transition.");
        save = ExpeditionRun.New(catalog, 1).Capture();
        save.DeployedIds[0] = "absent";
        rejected = false;
        try { ExpeditionRun.Restore(catalog, save); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Corrupt deployment is rejected.");

        save = ExpeditionRun.New(catalog, 1).Capture();
        save.Gold++;
        rejected = false;
        try { ExpeditionRun.Restore(catalog, save); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Gold inconsistent with rewards and purchases is rejected.");

        save = ExpeditionRun.New(catalog, 1).Capture();
        save.LastSummonedFamily = "";
        foreach (CatalogMemberSave member in save.Roster.Members)
        {
            member.BranchId = "";
            member.InheritedTraitId = "";
        }
        var restored = ExpeditionRun.Restore(catalog, save);
        Check(restored.Roster.Find("ally-01").BranchId == null &&
            restored.Roster.Find("ally-01").InheritedTraitId == null &&
            restored.Capture().LastSummonedFamily == null,
            "Unity empty-string null fields normalize during restore.");

        save = ExpeditionRun.New(catalog, 1).Capture();
        save.Roster.Members[0].BranchId = "E-AR-P";
        rejected = false;
        try { ExpeditionRun.Restore(catalog, save); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Base form cannot carry an unrelated branch in the save.");
    }

    static void CompareDraftFights()
    {
        foreach (int wave in new[] { 5, 10 })
            foreach (bool grown in new[] { false, true })
            {
                var save = ExpeditionRun.New(catalog, 80).Capture();
                save.Wave = wave;
                save.RewardedWave = save.Victories = wave - 1;
                save.LastReward = wave == 5 ? 30 : 35;
                save.Gold = wave == 5 ? 130 : 290;
                if (grown)
                {
                    var roster = CatalogRoster.Restore(catalog, save.Roster);
                    string[] forms = { "B-FI", "B-IC", "B-BA", "B-PR", "B-FI", "B-IC" };
                    for (int i = 0; i < forms.Length; i++)
                        roster.Recruit("ally-" + (i + 4).ToString("00"), forms[i], -2.9f, i % 2 == 0 ? 1.8f : -1.8f);
                    save.Roster = roster.Capture();
                    save.NextMemberNumber = 9;
                    save.Gold -= 120;
                    save.DeployedIds = new[] { "ally-01", "ally-02", "ally-03", "ally-08", "ally-09" };
                }
                var run = ExpeditionRun.Restore(catalog, save);
                if (grown)
                    Check(run.Transfer("ally-04", "ally-01", false) &&
                        run.Transfer("ally-05", "ally-01", false) && run.Evolve("ally-01", "E-KN-C") &&
                        run.Transfer("ally-06", "ally-02", false) &&
                        run.Transfer("ally-07", "ally-02", false) && run.Evolve("ally-02", "E-AR-P"),
                        "Draft grown formation includes two legal branch evolutions.");
                CatalogBattle battle = run.BeginBattle();
                for (int second = 0; second < 75 && !battle.Ended; second++) battle.Step(1f);
                int survivors = battle.Units.Count(unit => unit.Team == CatalogTeam.Ally && unit.IsAlive);
                Console.WriteLine("Draft wave " + wave + " " + (grown ? "grown five" : "basic three") +
                    ": " + (battle.Winner == CatalogTeam.Ally ? "win" : "no win") +
                    " at " + battle.Time.ToString("0.0") + "s, survivors " + survivors + ".");
            }
    }
}
