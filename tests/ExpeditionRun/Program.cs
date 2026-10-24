using System;
using System.Collections.Generic;
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
            GrowthPreviewAndProtection();
            BattleResumeAndProgression();
            EnemyWavesAndRoles();
            BeastChargeAndRangedAttack();
            BossBeastPattern();
            GolemPattern();
            MineGuardAndFurnacePattern();
            FrostWardenPattern();
            FrozenCounterBoundary();
            LegacyTenWaveMigration();
            BossStepPartition();
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
            Check(run.Wave == wave && run.IsBossWave == (wave % 5 == 0),
                "Each fifth wave is a boss.");
            Check(!string.IsNullOrEmpty(run.EncounterName) && !run.EncounterName.Contains("FOREST"),
                "Encounter name is ready for Korean UI.");
            Check(run.BeginBattle() != null && run.ResolveBattle(true) && run.Continue(), "Wave advances once.");
        }
        Check(run.Gold == 680 && run.Roster.SupplyUsed == 3 && run.Phase == ExpeditionPhase.Completed &&
            run.Victories == 20 && run.LastReward == 0,
            "Twenty-wave unspent economy earns 640 gold and final clear grants zero.");
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
        int[] supplied = { 5, 6, 7, 8, 9, 12, 13, 14, 15, 17, 20, 21, 23, 24, 26,
            29, 31, 32, 34, 37 };
        int[] remaining = { 0, 0, 0, 0, 10, 0, 5, 10, 15, 10, 0, 10, 0, 15, 15,
            5, 0, 15, 15, 0 };
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
                "Gold budget and cumulative supply match the twenty-wave plan at wave " + wave + ".");
            var restored = ExpeditionRun.Restore(catalog, run.Capture());
            Check(restored.Gold == run.Gold && restored.Roster.SupplyUsed == run.Roster.SupplyUsed,
                "Saved economy retains gold and cumulative summons.");
            run.BeginBattle(); run.ResolveBattle(true); run.Continue();
        }
        Check(run.Phase == ExpeditionPhase.Completed && run.Roster.SupplyUsed == 37 && run.Gold == 0,
            "Thirty-four paid summons and three starters survive the full budget path.");
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

    static void GrowthPreviewAndProtection()
    {
        var run = ExpeditionRun.New(catalog, 49);
        string donor = run.Summon();
        uint rng = run.Capture().RandomState;
        int gold = run.Gold;
        Check(run.CanTransfer(donor, "ally-01") && run.PreviewCombination(donor, "ally-01") == null,
            "Basic donor offers growth rather than a hybrid.");
        Check(!run.CanUndo && run.Gold == gold && run.Capture().RandomState == rng,
            "Repeated preview does not mutate history, money or RNG.");
        Check(!run.CanTransfer(donor, donor) && !run.CanTransfer("missing", "ally-01"), "Invalid targets rejected.");
        Check(run.SetMaterialLocked(donor, true) && !run.CanTransfer(donor, "ally-01") &&
            !run.Transfer(donor, "ally-01", true), "Locked donor cannot be consumed even by trait transfer.");
        Check(!run.CanUndo, "Protection toggle does not create a preparation action.");
        run = ExpeditionRun.Restore(catalog, run.Capture());
        Check(run.Roster.Find(donor).MaterialLocked, "Protection survives save restore.");
        Check(run.SetMaterialLocked(donor, false) && run.SetMaterialLocked("ally-01", true) &&
            run.Transfer(donor, "ally-01", false), "Protected recipient can still receive growth.");
        string second = run.Summon();
        Check(run.SetMaterialLocked(second, true) && run.Undo() && run.Roster.Find(donor) != null &&
            run.Roster.Find(second).MaterialLocked && run.Roster.Find("ally-01").MaterialLocked && run.Gold == 0,
            "Undo restores donor, keeps later summon and protection preferences, and never refunds summons.");
        Check(run.SetMaterialLocked(donor, true) && run.Move("ally-02", -4f, 1f) &&
            run.SetMaterialLocked(donor, false) && run.Undo() && !run.Roster.Find(donor).MaterialLocked,
            "Undo preserves latest unlock preference.");

        var fs = run.Capture();
        var roster = CatalogRoster.Restore(catalog, fs.Roster);
        for (int i = 6; i <= 9; i++) roster.Recruit("ally-" + i.ToString("00"), "B-IC", -2f, 0f);
        fs.Roster = roster.Capture(); fs.NextMemberNumber = 9; fs.UndoHistory = new ExpeditionPreparationSave[0];
        FundSaveForWaveTen(fs);
        run = ExpeditionRun.Restore(catalog, fs);
        Check(run.Transfer("ally-06", "ally-01", false) && run.Transfer("ally-07", "ally-01", false) &&
            run.Evolve("ally-01", "E-KN-G") && run.Transfer("ally-08", "ally-03", false) &&
            run.Transfer("ally-09", "ally-03", false) && run.Evolve("ally-03", "E-PR-H"), "Prepare two legal branches.");
        Check(run.PreviewCombination("ally-03", "ally-01") == "H-02" &&
            run.CanCombine("ally-01", "ally-03", "H-02"), "Preview resolves the same recipe as execution.");
        var optionsBefore = run.Capture();
        var options = run.GetCombinationOptions();
        Check(options.Count == 1 && options[0].RecipientId == "ally-01" &&
            options[0].DonorId == "ally-03" && options[0].ResultId == "H-02",
            "Available combinations identify exact owned members and preserve protected recipient.");
        Check(run.Roster.Find("ally-03") != null && run.Gold == optionsBefore.Gold &&
            run.Capture().UndoHistory.Length == optionsBefore.UndoHistory.Length,
            "Displaying a combination opportunity consumes nothing and does not create history.");
        Check(run.PreviewCombination("ally-01", "ally-03") == null, "Reversing to use the protected unit as donor is blocked.");
        Check(run.SetMaterialLocked("ally-03", true) && run.PreviewCombination("ally-03", "ally-01") == null &&
            !run.Combine("ally-01", "ally-03", "H-02"), "Protection blocks combination too.");
        Check(run.GetCombinationOptions().Count == 0, "Two protected partners do not produce an executable offer.");
        Check(run.SetMaterialLocked("ally-01", false) && run.GetCombinationOptions()[0].RecipientId == "ally-03",
            "When only second partner is protected, reverse the offered consumption direction.");
        run.SetMaterialLocked("ally-03", false);
        Check(run.SetDeployed("ally-01", false) && run.GetCombinationOptions()[0].RecipientId == "ally-03",
            "Unprotected partner on field retains its location over a bench recipient.");
        Check(run.Undo() && run.SetMaterialLocked("ally-01", true) && run.SetMaterialLocked("ally-03", true),
            "Restore preparation and protection for explicit execution checks.");
        Check(run.SetMaterialLocked("ally-03", false) && run.Combine("ally-01", "ally-03", "H-02") &&
            run.Roster.Find("ally-01").MaterialLocked && !run.CanTransfer(second, "ally-01"),
            "Hybrid keeps anchor protection and cannot absorb growth-only fodder.");
        run = ExpeditionRun.Restore(catalog, run.Capture());
        Check(run.Undo() && run.Roster.Find("ally-03") != null && run.DeployedIds.Contains("ally-03"),
            "Saved combination Undo restores consumed deployed partner.");
        Check(run.GetCombinationOptions().Count == 1, "Undo makes the explicit combination opportunity available again.");
        run.BeginBattle();
        Check(!run.CanTransfer(donor, "ally-01") && run.PreviewCombination("ally-03", "ally-01") == null &&
            !run.SetMaterialLocked(donor, true), "Preparation actions are unavailable during combat.");
        Check(run.GetCombinationOptions().Count == 0, "Combat never offers preparation combinations.");
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
        int[] expected = { 2, 3, 4, 4, 3, 3, 4, 5, 6, 3, 3, 4, 5, 5, 4, 4, 5, 6, 5, 3 };
        for (int wave = 1; wave <= ExpeditionRun.TotalWaves; wave++)
        {
            Check(!string.IsNullOrEmpty(run.EncounterDescription), "Preparation describes the upcoming enemy group.");
            CatalogBattle battle = run.BeginBattle();
            var enemies = battle.Units.Where(u => u.Team == CatalogTeam.Enemy).ToArray();
            Check(enemies.Length == expected[wave - 1], "Wave has its requested number of enemies.");
            Check(enemies.All(u => u.EnemyRole != CatalogEnemyRole.None), "Run enemies expose their roles.");
            Check(enemies.Select(u => u.X.ToString("0.00") + "," + u.Y.ToString("0.00")).Distinct().Count() == enemies.Length,
                "Enemies start at distinct positions.");
            if (wave == 3)
                Check(enemies.Count(u => u.EnemyRole == CatalogEnemyRole.Raider) == 3 &&
                    enemies.Any(u => u.Id == "spine-raider-3" && u.X == -.5f && u.Y == 2.5f),
                    "Third encounter previews three ranged beasts, including a fixed side flanker.");
            if (wave == 4)
                Check(enemies.Any(u => u.Id == "elite-beast" && u.EnemyRole == CatalogEnemyRole.Beast &&
                    u.NextChargeAt == 1.35f),
                    "Fourth encounter elite uses the existing readable beast charge.");
            if (wave >= 16)
            {
                Check(enemies.All(u => u.Id.StartsWith("frost-", StringComparison.Ordinal)),
                    "Frost region uses its distinct enemy IDs.");
                Check(enemies.Count(u => u.EnemyRole == CatalogEnemyRole.MineWorker) ==
                    (wave == 20 ? 0 : wave == 17 || wave == 18 ? 3 : 2),
                    "Frost guards use the nondashing melee role.");
                Check(enemies.Count(u => u.EnemyRole == CatalogEnemyRole.Raider) ==
                    (wave == 18 ? 3 : 2), "Frost acolytes use ranged behavior.");
                if (wave == 19) Check(enemies.Single(u => u.Id == "frost-cantor").EnemyRole ==
                    CatalogEnemyRole.Supervisor, "Cantor grants supervisor guard.");
                if (wave == 20) Check(enemies.Single(u => u.Id == "frost-boss").EnemyRole ==
                    CatalogEnemyRole.FrostWarden, "Final boss has its own frost role.");
            }
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

    static void StepUntil(CatalogBattle battle, Func<bool> condition, int maximumTicks, string reason)
    {
        for (int i = 0; i < maximumTicks && !condition() && !battle.Ended; i++) battle.Step(1f / 60f);
        Check(condition(), reason);
    }

    static void BossBeastPattern()
    {
        var battle = new CatalogBattle(catalog);
        CatalogBattleUnit first = battle.AddUnit("first", CatalogTeam.Ally, "B-KN", null, null, -1f, 0f);
        CatalogBattleUnit second = battle.AddUnit("second", CatalogTeam.Ally, "B-PR", null, null, -1.15f, .35f);
        CatalogBattleUnit outside = battle.AddUnit("outside", CatalogTeam.Ally, "B-AR", null, null, -1.2f, 1.8f);
        first.MoveSpeed = second.MoveSpeed = outside.MoveSpeed = 0f;
        CatalogBattleUnit boss = battle.AddEnemy("beast-boss", 2000f, 10f, 8f, 2f, 1f, .6f,
            true, 2.5f, 0f, CatalogEnemyRole.Beast, true);
        var events = new List<CatalogBattleEvent>();
        battle.Event += e => events.Add(e);
        StepUntil(battle, () => boss.ChargeWarningRemaining > 0f, 600,
            "Boss beast enters a charge warning.");
        Check(boss.BossPatternsEnabled && boss.ChargeWarningRemaining > .7f &&
            boss.ChargeWarningRemaining <= .901f && boss.ChargeWidth == .7f,
            "Boss charge has a .9-second warning and .7 half-width: " +
            boss.BossPatternsEnabled + "," + boss.ChargeWarningRemaining + "," + boss.ChargeWidth);
        Check(battle.Time >= 7f, "Boss beast waits seven seconds before its first charge.");
        float originX = boss.ChargeOriginX, aimX = boss.ChargeTargetX;
        float previewX = boss.ChargeImpactX, previewY = boss.ChargeImpactY;
        float paused = battle.Time;
        battle.Step(0f);
        Check(battle.Time == paused && boss.ChargeOriginX == originX,
            "Paused battle does not consume boss warning.");
        StepUntil(battle, () => boss.IsCharging, 120, "Boss begins visible charge movement.");
        Check(boss.ChargeWarningRemaining == 0f && boss.ChargeTargetX == aimX &&
            Math.Abs(boss.ChargeImpactX - previewX) < .001f &&
            Math.Abs(boss.ChargeImpactY - previewY) < .001f,
            "Charge keeps its fixed aim and predicted impact during movement.");
        StepUntil(battle, () => events.Exists(e => e.Kind == CatalogEventKind.BossImpact &&
            e.AbilityId == "BOSS_CHARGE"), 40, "Boss charge resolves after movement.");
        Check(events.Count(e => e.Kind == CatalogEventKind.BossImpact && e.AbilityId == "BOSS_CHARGE") == 1 &&
            events.Count(e => e.Kind == CatalogEventKind.Damage && e.AbilityId == "BOSS_CHARGE" &&
                e.TargetId == first.Id) == 1 &&
            events.Count(e => e.Kind == CatalogEventKind.Damage && e.AbilityId == "BOSS_CHARGE" &&
                e.TargetId == second.Id) == 1 &&
            events.Count(e => e.Kind == CatalogEventKind.Damage && e.AbilityId == "BOSS_CHARGE" &&
                e.TargetId == outside.Id) == 0,
            "Boss charge damages each unit inside corridor once and spares units outside: " +
            string.Join(",", events.Where(e => e.Kind == CatalogEventKind.Damage &&
                e.AbilityId == "BOSS_CHARGE").Select(e => e.TargetId).ToArray()) +
            " origin=" + originX + " impact=" + boss.ChargeEndX + "," + boss.ChargeEndY);

        var slowBattle = new CatalogBattle(catalog);
        slowBattle.AddUnit("target", CatalogTeam.Ally, "B-KN", null, null, -1f, 0f).MoveSpeed = 0f;
        CatalogBattleUnit slowBoss = slowBattle.AddEnemy("boss", 2000f, 8f, 8f, 2f, 1f, .01f,
            true, 3.5f, 0f, CatalogEnemyRole.Beast, true);
        StepUntil(slowBattle, () => slowBoss.ChargeWarningRemaining > 0f, 600,
            "Slow test reaches long-distance charge warning.");
        float originalImpact = slowBoss.ChargeImpactX, fixedAim = slowBoss.ChargeTargetX;
        slowBoss.Slow = .4f; slowBoss.SlowUntil = slowBattle.Time + 2f;
        Check(slowBoss.ChargeImpactX > originalImpact && slowBoss.ChargeTargetX == fixedAim,
            "Slow shortens the exposed charge corridor without retargeting.");
        float slowedImpact = slowBoss.ChargeImpactX;
        StepUntil(slowBattle, () => slowBoss.IsCharging, 120, "Slowed boss enters charge movement.");
        slowBoss.Slow = 0f;
        Check(Math.Abs(slowBoss.ChargeImpactX - slowedImpact) < .001f,
            "Charge impact remains locked after movement starts even if slow changes.");
        StepUntil(slowBattle, () => !slowBoss.IsCharging, 40, "Slowed boss finishes charge.");
        Check(Math.Abs(slowBoss.ChargeImpactX - slowedImpact) < .001f,
            "Completed charge retains its actual impact point for effects.");

        var dodge = new CatalogBattle(catalog);
        CatalogBattleUnit movingTarget = dodge.AddUnit("target", CatalogTeam.Ally, "B-KN", null, null, -1f, 0f);
        movingTarget.MoveSpeed = 0f;
        dodge.AddUnit("reserve", CatalogTeam.Ally, "B-AR", null, null, -4f, 3f).MoveSpeed = 0f;
        CatalogBattleUnit chargingBoss = dodge.AddEnemy("boss", 2000f, 8f, 8f, 2f, 1f, .6f,
            true, 2.5f, 0f, CatalogEnemyRole.Beast, true);
        int impacts = 0, chargeDamage = 0;
        dodge.Event += e => { if (e.Kind == CatalogEventKind.BossImpact) impacts++;
            if (e.Kind == CatalogEventKind.Damage && e.AbilityId == "BOSS_CHARGE") chargeDamage++; };
        StepUntil(dodge, () => chargingBoss.ChargeWarningRemaining > 0f, 600, "Dodge test reaches charge warning.");
        float fixedTargetX = chargingBoss.ChargeTargetX;
        movingTarget.X = -4f; movingTarget.Y = 3f;
        StepUntil(dodge, () => impacts > 0, 120, "Charge still finishes when target moves.");
        Check(chargingBoss.ChargeTargetX == fixedTargetX && chargeDamage == 0,
            "Moving out of fixed corridor avoids damage.");

        var lostTarget = new CatalogBattle(catalog);
        CatalogBattleUnit doomed = lostTarget.AddUnit("target", CatalogTeam.Ally, "B-KN", null, null, -1f, 0f);
        doomed.MoveSpeed = 0f;
        lostTarget.AddUnit("reserve", CatalogTeam.Ally, "B-AR", null, null, -4f, 3f).MoveSpeed = 0f;
        CatalogBattleUnit fixedBoss = lostTarget.AddEnemy("boss", 2000f, 8f, 8f, 2f, 1f, .6f,
            true, 2.5f, 0f, CatalogEnemyRole.Beast, true);
        int fixedImpacts = 0, fixedDamage = 0;
        lostTarget.Event += e => { if (e.Kind == CatalogEventKind.BossImpact) fixedImpacts++;
            if (e.Kind == CatalogEventKind.Damage && e.AbilityId == "BOSS_CHARGE") fixedDamage++; };
        StepUntil(lostTarget, () => fixedBoss.ChargeWarningRemaining > 0f, 600,
            "Lost-target test reaches charge warning.");
        float lockedAim = fixedBoss.ChargeTargetX;
        doomed.Hp = 0f;
        StepUntil(lostTarget, () => fixedImpacts > 0, 120,
            "Charge completes on its fixed line after its target dies.");
        Check(fixedBoss.ChargeTargetX == lockedAim && fixedDamage == 0,
            "Dead original target does not redirect or receive charge damage.");

        var cancelled = new CatalogBattle(catalog);
        cancelled.AddUnit("target", CatalogTeam.Ally, "B-KN", null, null, -1f, 0f).MoveSpeed = 0f;
        CatalogBattleUnit deadBoss = cancelled.AddEnemy("boss", 2000f, 8f, 8f, 2f, 1f, .6f,
            true, 2.5f, 0f, CatalogEnemyRole.Beast, true);
        int cancelledImpacts = 0;
        cancelled.Event += e => { if (e.Kind == CatalogEventKind.BossImpact) cancelledImpacts++; };
        StepUntil(cancelled, () => deadBoss.ChargeWarningRemaining > 0f, 600, "Cancellation test reaches warning.");
        deadBoss.Hp = 0f;
        cancelled.Step(2f);
        Check(cancelledImpacts == 0 && !deadBoss.IsCharging,
            "A defeated boss cannot finish its pending charge.");

        var counterBattle = new CatalogBattle(catalog);
        CatalogBattleUnit counterKnight = counterBattle.AddUnit("counter", CatalogTeam.Ally,
            "E-KN-C", null, null, -1f, 0f);
        counterKnight.MoveSpeed = 0f;
        CatalogBattleUnit counterBoss = counterBattle.AddEnemy("boss", 2000f, 8f, 8f,
            2f, 1f, .6f, true, 2.5f, 0f, CatalogEnemyRole.Beast, true);
        int counterImpacts = 0, counterSkills = 0;
        counterBattle.Event += e => { if (e.Kind == CatalogEventKind.BossImpact) counterImpacts++;
            if (e.Kind == CatalogEventKind.SkillUsed && e.AbilityId == "E-KN-C") counterSkills++; };
        StepUntil(counterBattle, () => counterBoss.IsCharging, 700,
            "Counter test reaches boss charge movement.");
        counterBoss.Hp = 1f;
        counterKnight.Shield = 1f;
        counterKnight.ShieldUntil = counterBattle.Time + 2f;
        counterKnight.ShieldSourceId = counterKnight.Id;
        StepUntil(counterBattle, () => counterImpacts > 0, 40,
            "Counter test charge reaches impact.");
        Check(counterSkills == 1 && !counterBoss.IsAlive &&
            counterBoss.AttackPhase == CatalogAttackPhase.Ready &&
            counterBoss.PendingSkillId == null && !counterBoss.IsCharging,
            "A counterattack killing the boss leaves no active boss pattern state.");
    }

    static void GolemPattern()
    {
        var battle = new CatalogBattle(catalog);
        CatalogBattleUnit target = battle.AddUnit("target", CatalogTeam.Ally, "B-IC", null, null, -1f, 0f);
        CatalogBattleUnit near = battle.AddUnit("near", CatalogTeam.Ally, "B-KN", null, null, -1.2f, .45f);
        CatalogBattleUnit far = battle.AddUnit("far", CatalogTeam.Ally, "B-AR", null, null, -1.4f, 2.2f);
        target.MoveSpeed = near.MoveSpeed = far.MoveSpeed = 0f;
        CatalogBattleUnit boss = battle.AddEnemy("golem", 1000f, 8f, 15f, 2f, 1f, .5f,
            true, 2.5f, 0f, CatalogEnemyRole.Golem, true);
        boss.Hp = 390f;
        var events = new List<CatalogBattleEvent>();
        battle.Event += e => events.Add(e);
        StepUntil(battle, () => boss.BossEnraged, 150, "Golem enters rage below forty percent.");
        Check(events.Count(e => e.Kind == CatalogEventKind.BossEnraged) == 1,
            "Golem rage is announced exactly once.");
        StepUntil(battle, () => boss.SlamWarningRemaining > 0f, 400,
            "Golem warns of its first slam.");
        Check(boss.SlamWarningRemaining > .9f && boss.SlamWarningRemaining <= 1.1f &&
            boss.SlamRadius == 1.35f && !boss.SlamIsAftershock,
            "First slam exposes a fixed center, radius, and 1.1-second warning.");
        float fixedX = boss.SlamTargetX, fixedY = boss.SlamTargetY;
        target.X = -4f; target.Y = 3f;
        float paused = battle.Time;
        battle.Step(0f);
        Check(battle.Time == paused && boss.SlamTargetX == fixedX,
            "Pause preserves the fixed slam warning.");
        StepUntil(battle, () => events.Exists(e => e.Kind == CatalogEventKind.BossImpact &&
            e.AbilityId == "GOLEM_SLAM"), 150, "Golem slam resolves.");
        Check(events.Count(e => e.Kind == CatalogEventKind.BossImpact && e.AbilityId == "GOLEM_SLAM") == 1 &&
            events.Count(e => e.Kind == CatalogEventKind.Damage && e.AbilityId == "GOLEM_SLAM" &&
                e.TargetId == target.Id) == 0 &&
            events.Count(e => e.Kind == CatalogEventKind.Damage && e.AbilityId == "GOLEM_SLAM" &&
                e.TargetId == near.Id) == 1 &&
            events.Count(e => e.Kind == CatalogEventKind.Damage && e.AbilityId == "GOLEM_SLAM" &&
                e.TargetId == far.Id) == 0,
            "Slam hits only units inside its fixed circle, once each.");
        Check(boss.SlamIsAftershock && boss.SlamWarningRemaining > 0f &&
            boss.SlamRadius == .75f && boss.SlamTargetX == fixedX && boss.SlamTargetY == fixedY,
            "Raging golem follows with a narrower .6-second warning at the same center.");
        StepUntil(battle, () => events.Exists(e => e.Kind == CatalogEventKind.BossImpact &&
            e.AbilityId == "GOLEM_AFTERSHOCK"), 100, "Aftershock resolves.");
        Check(events.Count(e => e.Kind == CatalogEventKind.BossImpact &&
            e.AbilityId == "GOLEM_AFTERSHOCK") == 1 &&
            events.Count(e => e.Kind == CatalogEventKind.Damage &&
            e.AbilityId == "GOLEM_AFTERSHOCK" && e.TargetId == near.Id) <= 1,
            "Aftershock emits one impact and does not duplicate per-target damage.");

        var legacy = new CatalogBattle(catalog);
        legacy.AddUnit("ally", CatalogTeam.Ally, "B-KN", null, null, -1f, 0f);
        CatalogBattleUnit labBoss = legacy.AddEnemy("lab-boss", 1000f, 5f, 15f, 2f, 1f, .5f,
            true, 2.5f, 0f, CatalogEnemyRole.Golem);
        int legacyPatternEvents = 0;
        legacy.Event += e => { if (e.Kind == CatalogEventKind.BossTelegraph) legacyPatternEvents++; };
        legacy.Step(6f);
        Check(!labBoss.BossPatternsEnabled && legacyPatternEvents == 0,
            "Existing lab boss call stays on its old behavior by default.");
    }

    static CatalogBattle PartitionBattle(List<CatalogBattleEvent> events)
    {
        var battle = new CatalogBattle(catalog);
        battle.AddUnit("target", CatalogTeam.Ally, "B-KN", null, null, -1f, 0f).MoveSpeed = 0f;
        battle.AddUnit("near", CatalogTeam.Ally, "B-PR", null, null, -1.2f, .3f).MoveSpeed = 0f;
        battle.AddEnemy("golem", 3000f, 8f, 15f, 2f, 1f, .5f,
            true, 2.5f, 0f, CatalogEnemyRole.Golem, true);
        battle.Event += e => events.Add(e);
        return battle;
    }

    static void BossStepPartition()
    {
        var wholeEvents = new List<CatalogBattleEvent>();
        var partsEvents = new List<CatalogBattleEvent>();
        CatalogBattle whole = PartitionBattle(wholeEvents), parts = PartitionBattle(partsEvents);
        whole.Step(4f);
        parts.Step(1f);
        parts.Step(1f);
        parts.Step(2f);
        Check(Math.Abs(whole.Time - parts.Time) < .001f && wholeEvents.Count == partsEvents.Count,
            "Boss pattern timing is invariant to Step partitioning.");
        for (int i = 0; i < wholeEvents.Count; i++)
            Check(wholeEvents[i].Kind == partsEvents[i].Kind &&
                wholeEvents[i].AbilityId == partsEvents[i].AbilityId &&
                wholeEvents[i].SourceId == partsEvents[i].SourceId &&
                wholeEvents[i].TargetId == partsEvents[i].TargetId &&
                Math.Abs(wholeEvents[i].Time - partsEvents[i].Time) < .001f,
                "Boss pattern event order remains identical across Step partitioning.");
        foreach (CatalogBattleUnit unit in whole.Units)
        {
            CatalogBattleUnit counterpart = parts.Get(unit.Id);
            Check(Math.Abs(unit.Hp - counterpart.Hp) < .001f &&
                Math.Abs(unit.X - counterpart.X) < .001f &&
                Math.Abs(unit.Y - counterpart.Y) < .001f,
                "Boss pattern combat state remains identical across Step partitioning.");
        }
    }

    static void MineGuardAndFurnacePattern()
    {
        var battle = new CatalogBattle(catalog);
        CatalogBattleUnit knight = battle.AddUnit("knight", CatalogTeam.Ally, "B-KN", null, null, -1.3f, 0f);
        knight.Hp = knight.MaxHp = 5000f;
        CatalogBattleUnit boss = battle.AddEnemy("furnace-boss", 1000f, 5f, 10f, 1.8f, 1.1f, .48f,
            true, 3f, 0f, CatalogEnemyRole.Furnace, true);
        CatalogBattleUnit guard = battle.AddEnemy("mine-elite", 1000f, 5f, 10f, 1.8f, 1f, .57f,
            false, 2.5f, -1f, CatalogEnemyRole.Supervisor);
        CatalogBattleUnit slinger = battle.AddEnemy("mine-slinger-1", 500f, 5f, 5f, 2f, 3.6f, .55f,
            false, 3f, 1f, CatalogEnemyRole.Raider);
        int guardShields = 0, blasts = 0, embers = 0, blastHits = 0, emberHits = 0, burns = 0;
        int burnTicks = 0; float burnDamage = 0f;
        battle.Event += e =>
        {
            if (e.Kind == CatalogEventKind.Shield && e.AbilityId == "MINE_GUARD") guardShields++;
            if (e.Kind == CatalogEventKind.BossTelegraph && e.AbilityId == "FURNACE_BLAST") blasts++;
            if (e.Kind == CatalogEventKind.BossTelegraph && e.AbilityId == "FURNACE_EMBER") embers++;
            if (e.Kind == CatalogEventKind.BossImpact && e.AbilityId == "FURNACE_BLAST") blastHits++;
            if (e.Kind == CatalogEventKind.BossImpact && e.AbilityId == "FURNACE_EMBER") emberHits++;
            if (e.Kind == CatalogEventKind.Burn && e.SourceId == boss.Id && e.TargetId == knight.Id &&
                e.AbilityId == "BURN" && Math.Abs(e.Amount - 10f) < .001f) burns++;
            if (e.Kind == CatalogEventKind.Damage && e.AbilityId == "BURN" && e.TargetId == knight.Id)
            { burnTicks++; burnDamage += e.Amount; }
        };
        battle.Step(1.6f);
        Check(guardShields == 1 && boss.Shield > 0f && slinger.Shield == 0f &&
            boss.Shield <= 120f && guard.Shield == 0f,
            "Supervisor shields a nearby boss without covering a distant escort.");
        battle.Step(.6f);
        Check(blasts == 1 && boss.SlamWarningRemaining > 0f && boss.SlamRadius == 1.25f,
            "Furnace uses a distinct fixed-point blast warning.");
        boss.BossEnraged = true;
        battle.Step(1f);
        Check(embers == 1 && blastHits == 1 && boss.SlamWarningRemaining > 0f && boss.SlamIsAftershock &&
            boss.SlamRadius == 1f && knight.BurnRemaining > 0f && burns == 1,
            "Furnace blast burns surviving targets with three raw ten-damage ticks.");
        battle.Step(.7f);
        Check(emberHits == 1 && boss.SlamWarningRemaining == 0f && burns == 1,
            "The ember pulse lands once without refreshing the burn.");
        battle.Step(4.8f);
        Check(guardShields >= 2 && boss.Shield <= 120f && slinger.Shield <= 60f,
            "Guard refresh never adds repeated shield amounts.");
        Check(knight.BurnRemaining == 0f && burnTicks == 3 && burnDamage > 0f && burnDamage < 30f,
            "Three raw ten-damage burn ticks use normal defense and expire without stacking.");
    }

    static void FrostWardenPattern()
    {
        var battle = new CatalogBattle(catalog);
        CatalogBattleUnit front = battle.AddUnit("front", CatalogTeam.Ally, "B-KN", null, null, -1f, 0f);
        CatalogBattleUnit rear = battle.AddUnit("rear", CatalogTeam.Ally, "B-AR", null, null, -4f, 0f);
        CatalogBattleUnit witness = battle.AddUnit("witness", CatalogTeam.Ally, "B-PR", null, null, -3f, 0f);
        front.MoveSpeed = rear.MoveSpeed = witness.MoveSpeed = 0f;
        front.Hp = front.MaxHp = rear.Hp = rear.MaxHp = witness.Hp = witness.MaxHp = 3000f;
        CatalogBattleUnit boss = battle.AddEnemy("frost-boss", 10000f, 10f, 25f, 1.8f, 1.1f, .01f,
            true, 3f, 0f, CatalogEnemyRole.FrostWarden, true);
        var events = new List<CatalogBattleEvent>();
        battle.Event += events.Add;
        StepUntil(battle, () => boss.SlamWarningRemaining > 0f, 180,
            "Frost Warden announces its distant-target nova.");
        Check(boss.AttackTargetId == rear.Id && Math.Abs(boss.SlamTargetX - rear.X) < .01f &&
            boss.SlamRadius == 1.1f && boss.SlamWarningRemaining > 1f &&
            events.Count(e => e.Kind == CatalogEventKind.BossTelegraph && e.AbilityId == "FROST_NOVA") == 1,
            "Frost nova locks the farthest living ally without the melee range cap.");
        float lockedX = boss.SlamTargetX;
        rear.X = -5.8f;
        StepUntil(battle, () => events.Any(e => e.Kind == CatalogEventKind.BossImpact &&
            e.AbilityId == "FROST_NOVA"), 100, "Frost nova lands after its warning.");
        Check(boss.SlamTargetX == lockedX &&
            events.Any(e => e.Kind == CatalogEventKind.Damage && e.AbilityId == "FROST_NOVA" &&
                e.TargetId == witness.Id &&
                Math.Abs(e.Amount - 15f * 100f / (100f + witness.Defense)) < .01f) &&
            !events.Any(e => e.Kind == CatalogEventKind.Damage && e.AbilityId == "FROST_NOVA" &&
                e.TargetId == rear.Id) && Math.Abs(boss.NextSlamAt - battle.Time - 8f) < .05f,
            "Locked circle hits only occupants at the announced point and waits eight seconds.");
        Check(witness.FrozenRemaining > 1f && witness.FrozenRemaining <= 1.101f &&
            rear.FrozenRemaining == 0f &&
            events.Count(e => e.Kind == CatalogEventKind.Freeze && e.AbilityId == "FROST_NOVA" &&
                e.TargetId == witness.Id) == 1,
            "Only living occupants of the nova circle receive a bounded freeze: witness=" +
            witness.FrozenRemaining + ", rear=" + rear.FrozenRemaining + ", events=" +
            events.Count(e => e.Kind == CatalogEventKind.Freeze && e.AbilityId == "FROST_NOVA" &&
                e.TargetId == witness.Id));
        boss.BossEnraged = true;
        boss.NextSlamAt = battle.Time;
        StepUntil(battle, () => boss.SlamWarningRemaining > 0f, 30,
            "Enraged Frost Warden announces another nova.");
        Check(boss.SlamWarningRemaining > .7f && boss.SlamWarningRemaining <= .8f &&
            boss.AttackTargetId == rear.Id, "Enraged warning shortens to .8 seconds.");
        StepUntil(battle, () => events.Count(e => e.Kind == CatalogEventKind.BossImpact &&
            e.AbilityId == "FROST_NOVA") == 2, 70, "Enraged nova lands.");
        Check(Math.Abs(boss.NextSlamAt - battle.Time - 6f) < .05f,
            "Enraged nova repeats after six seconds.");
        boss.Hp = 0f;
        battle.Step(1f / 60f);
        Check(battle.Ended && rear.FrozenRemaining == 0f && witness.FrozenRemaining == 0f,
            "Final battle settlement hides any remaining freeze state.");
        bool rejected = false;
        try { battle.AddEnemy("false-boss", 100f, 5f, 0f, 2f, 1f, .5f, false, 2f, 0f,
            CatalogEnemyRole.FrostWarden, true); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Frost Warden role cannot be assigned to a nonboss.");

        var threshold = new CatalogBattle(catalog);
        threshold.AddUnit("archer", CatalogTeam.Ally, "B-AR", null, null, 1f, 0f);
        CatalogBattleUnit thresholdBoss = threshold.AddEnemy("frost-boss", 10000f, 5f, 0f,
            1.8f, 1.1f, .01f, true, 3f, 0f, CatalogEnemyRole.FrostWarden, true);
        thresholdBoss.Hp = 4001f;
        int enrages = 0;
        threshold.Event += e => { if (e.Kind == CatalogEventKind.BossEnraged) enrages++; };
        StepUntil(threshold, () => thresholdBoss.BossEnraged, 150,
            "Frost Warden enrages when actual damage crosses forty percent HP.");
        Check(enrages == 1 && thresholdBoss.Hp <= 4000f,
            "Frost enrage emits once at the forty-percent threshold.");

        var pause = new CatalogBattle(catalog);
        CatalogBattleUnit archer = pause.AddUnit("archer", CatalogTeam.Ally, "B-AR", null, null, -1f, 0f);
        archer.Hp = archer.MaxHp = 3000f;
        CatalogBattleUnit ice = pause.AddEnemy("frost-boss", 10000f, 10f, 0f,
            1.8f, 1.1f, .01f, true, 3f, 0f, CatalogEnemyRole.FrostWarden, true);
        int archerHits = 0, archerSkills = 0;
        pause.Event += e =>
        {
            if (e.SourceId == archer.Id && e.Kind == CatalogEventKind.BasicHit) archerHits++;
            if (e.SourceId == archer.Id && e.Kind == CatalogEventKind.SkillUsed) archerSkills++;
        };
        StepUntil(pause, () => ice.SlamWarningRemaining > 0f, 180, "Pause test sees frost warning.");
        StepUntil(pause, () => ice.SlamWarningRemaining < .08f, 90,
            "Pause test approaches frost impact.");
        archer.AttackPhase = CatalogAttackPhase.WindingBasic;
        archer.PendingTargetId = ice.Id;
        archer.ReleaseAt = pause.Time + .15f;
        StepUntil(pause, () => archer.FrozenRemaining > 0f, 20,
            "Impact freezes the archer during an active windup.");
        archer.X = -4f; // Out of attack range, so a nonfrozen actor would move.
        float frozenX = archer.X, frozenY = archer.Y;
        int hitsBeforeThaw = archerHits, skillsBeforeThaw = archerSkills;
        pause.Step(.95f);
        Check(archer.FrozenRemaining > 0f && archerHits == hitsBeforeThaw &&
            archerSkills == skillsBeforeThaw && archer.AttackPhase == CatalogAttackPhase.WindingBasic &&
            archer.X == frozenX && archer.Y == frozenY,
            "Freeze stops movement, attacks and skills while preserving the pending windup.");
        StepUntil(pause, () => archer.FrozenRemaining == 0f, 30, "Archer thaws.");
        Check(archerHits == hitsBeforeThaw && archer.AttackWindupRemaining > .03f,
            "The pending hit does not burst on the thaw tick: hits=" + archerHits +
            ", before=" + hitsBeforeThaw + ", windup=" + archer.AttackWindupRemaining);
        archer.X = -1f;
        StepUntil(pause, () => archerHits > hitsBeforeThaw, 30,
            "The saved windup finishes normally after thaw.");
    }

    static void FrozenCounterBoundary()
    {
        var battle = new CatalogBattle(catalog);
        CatalogBattleUnit knight = battle.AddUnit("knight", CatalogTeam.Ally, "E-KN-C", null, null, -1f, 0f);
        CatalogBattleUnit attacker = battle.AddEnemy("attacker", 1000f, 5f, 0f,
            2f, 1f, .5f, false, 1f, 0f, CatalogEnemyRole.Raider);
        var damage = typeof(CatalogBattle).GetMethod("Damage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Check(damage != null, "Counter boundary can invoke the actual damage pipeline.");
        int counters = 0;
        battle.Event += e => { if (e.Kind == CatalogEventKind.SkillUsed && e.AbilityId == "E-KN-C") counters++; };
        knight.Shield = 10f; knight.ShieldSourceId = knight.Id; knight.ShieldUntil = 10f;
        damage.Invoke(battle, new object[] { attacker, knight, 100f, "FROST_NOVA", false, false });
        Check(counters == 0, "The nova impact cannot trigger a counter before freeze is applied.");
        knight.Shield = 10f; knight.FrozenUntil = battle.Time + 1.1f;
        damage.Invoke(battle, new object[] { attacker, knight, 100f, "BASIC", true, false });
        Check(counters == 0, "A frozen defender cannot counter a later shield break.");
        knight.Shield = 10f; knight.FrozenUntil = 0f;
        damage.Invoke(battle, new object[] { attacker, knight, 100f, "BASIC", true, false });
        Check(counters == 1, "The same counter works again after thaw.");
    }

    static void LegacyTenWaveMigration()
    {
        var run = ExpeditionRun.New(catalog, 73);
        for (int wave = 1; wave < 10; wave++)
        {
            run.BeginBattle(); run.ResolveBattle(true); run.Continue();
            if (wave == 4)
            {
                ExpeditionRunSave prior = run.Capture(); prior.Version = 2;
                var unchanged = ExpeditionRun.Restore(catalog, prior);
                Check(unchanged.Wave == 5 && unchanged.Gold == 130 &&
                    unchanged.Capture().Version == 5 && prior.Version == 2,
                    "Earlier valid v2 preparation migrates without reward or mutating its input.");
                unchanged.BeginBattle();
                ExpeditionRunSave earlyBattle = unchanged.Capture(); earlyBattle.Version = 2;
                var resumedEarly = ExpeditionRun.Restore(catalog, earlyBattle);
                Check(resumedEarly.Phase == ExpeditionPhase.Battle && resumedEarly.Wave == 5 &&
                    resumedEarly.Gold == 130 && resumedEarly.ResumeBattle().Get("beast-boss") != null,
                    "Earlier v2 battle state keeps its original encounter and budget.");
            }
        }
        ExpeditionRunSave preparation = run.Capture(); preparation.Version = 2;
        var resumed = ExpeditionRun.Restore(catalog, preparation);
        Check(resumed.Wave == 10 && resumed.Phase == ExpeditionPhase.Preparation &&
            resumed.Gold == 290 && resumed.LastReward == 35,
            "Old v2 wave-ten preparation remains the same fight and budget.");
        resumed.BeginBattle();
        ExpeditionRunSave battleSave = resumed.Capture(); battleSave.Version = 2;
        var inBattle = ExpeditionRun.Restore(catalog, battleSave);
        Check(inBattle.Phase == ExpeditionPhase.Battle && inBattle.Gold == 290 &&
            inBattle.ResumeBattle().Get("golem-boss") != null,
            "Old v2 wave-ten battle resumes without early reward or encounter changes.");
        resumed.ResolveBattle(true);
        Check(resumed.Gold == 340 && resumed.LastReward == 50,
            "New wave-ten victory grants the passage reward exactly once.");
        ExpeditionRunSave oldResult = resumed.Capture(); oldResult.Version = 2;
        oldResult.Gold -= 50; oldResult.LastReward = 0;
        var migratedResult = ExpeditionRun.Restore(catalog, oldResult);
        Check(migratedResult.Phase == ExpeditionPhase.Result && migratedResult.Wave == 10 &&
            migratedResult.Gold == 340 && migratedResult.LastReward == 50 &&
            oldResult.Gold == 290 && oldResult.LastReward == 0,
            "Valid old victory result gains fifty without mutating the source save.");
        var reloaded = ExpeditionRun.Restore(catalog, migratedResult.Capture());
        Check(reloaded.Gold == 340 && reloaded.Continue() &&
            reloaded.Phase == ExpeditionPhase.Preparation && reloaded.Wave == 11 &&
            ExpeditionRun.Restore(catalog, reloaded.Capture()).Gold == 340,
            "Current capture and reload cannot duplicate the migration grant.");
        oldResult.Phase = ExpeditionPhase.Completed;
        var completed = ExpeditionRun.Restore(catalog, oldResult);
        Check(completed.Phase == ExpeditionPhase.Preparation && completed.Wave == 11 &&
            completed.Gold == 340 && completed.LastReward == 50 &&
            ExpeditionRun.Restore(catalog, completed.Capture()).Gold == 340,
            "Old completed ten-wave runs enter wave eleven with one grant.");
        var lost = ExpeditionRun.Restore(catalog, preparation);
        lost.BeginBattle(); lost.ResolveBattle(false);
        ExpeditionRunSave oldLoss = lost.Capture(); oldLoss.Version = 2;
        var migratedLoss = ExpeditionRun.Restore(catalog, oldLoss);
        Check(migratedLoss.Phase == ExpeditionPhase.Result && !migratedLoss.LastVictory &&
            migratedLoss.Gold == 290 && migratedLoss.LastReward == 0,
            "Old wave-ten defeat receives no passage reward.");
        var defeated = ExpeditionRun.Restore(catalog, oldLoss);
        defeated.Continue();
        for (int i = 0; i < 2; i++)
        { defeated.BeginBattle(); defeated.ResolveBattle(false); defeated.Continue(); }
        ExpeditionRunSave oldDefeated = defeated.Capture(); oldDefeated.Version = 2;
        var migratedDefeat = ExpeditionRun.Restore(catalog, oldDefeated);
        Check(migratedDefeat.Phase == ExpeditionPhase.Defeated && migratedDefeat.Wave == 10 &&
            migratedDefeat.Gold == 290 && migratedDefeat.LastReward == 0,
            "Old exhausted-lantern defeat remains terminal without migration reward.");
        oldResult.Phase = ExpeditionPhase.Result; oldResult.Gold++;
        bool rejected = false;
        try { ExpeditionRun.Restore(catalog, oldResult); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Invalid old budget is rejected before migration.");
        oldResult.Gold--; oldResult.LastReward = 50; rejected = false;
        try { ExpeditionRun.Restore(catalog, oldResult); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Invalid old result reward cannot bypass v2 validation.");
        preparation.Wave = 11; rejected = false;
        try { ExpeditionRun.Restore(catalog, preparation); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "A v2 save cannot claim the new region directly.");
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
