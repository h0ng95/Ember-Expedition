using System;
using System.Collections.Generic;
using System.Globalization;
using Expedition.Core;

internal static class Program
{
    private const float Tick = 1f / 60f;
    private static int assertions;

    private static int Main()
    {
        try
        {
            DamageRequiresImpact();
            DeadTargetsCancelPendingAttacks();
            EscapingTargetCancelsWindup();
            SpeedSchedulingPreservesBattle();
            BoundariesAndOverlapsStayFinite();
            FinishedBattleEmitsOnce();
            PreparationChoicesCanBeUndone();
            BrokenShieldTriggersOneCounter();
            GuardianProtectsNearbyAlly();
            GolemTelegraphLocksDirection();
            PriestFollowsWithinShieldRange();
            BurnUsesLatestApplierAttack();
            FrequentBurnRefreshKeepsTickClock();
            GolemHoldsPositionDuringTelegraph();
            StageTwoBattleFlowAndScore();
            StageLossAndDraftVariation();
            StageCatalogAndRecords();
            Console.WriteLine("PASS: 17 combat, preparation, and stage scenarios; " + assertions + " assertions.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error);
            return 1;
        }
    }

    private static UnitDefinition Definition(string id, Team team, AttackKind kind)
    {
        return new UnitDefinition
        {
            Id = id, Team = team, AttackKind = kind,
            MaxHp = 100f, Damage = 12f, Range = kind == AttackKind.Ranged ? 8f : 0.4f,
            MoveSpeed = 1.4f, Cooldown = 0.7f, Windup = 0.2f,
            ProjectileSpeed = 5f, Radius = 0.25f
        };
    }

    private static void DamageRequiresImpact()
    {
        var archerDefinition = Definition("archer", Team.Ally, AttackKind.Ranged);
        archerDefinition.Windup = 0.3f;
        archerDefinition.ProjectileSpeed = 2f;
        var enemyDefinition = Definition("target", Team.Enemy, AttackKind.Melee);
        enemyDefinition.MoveSpeed = 0f;
        var archer = new CombatUnit(archerDefinition, -3f, 0f);
        var enemy = new CombatUnit(enemyDefinition, 3f, 0f);
        var sim = new CombatSimulation(new[] { archer, enemy });
        bool launched = false;
        int hits = 0;
        float damage = 0f;
        float launchTime = 0f;
        sim.Event += delegate(CombatEvent ev)
        {
            if (ev.Type == CombatEventType.ProjectileLaunched)
            {
                Assert(enemy.Hp == enemyDefinition.MaxHp || hits > 0, "Launching cannot remove HP.");
                if (!launched) launchTime = sim.Elapsed;
                launched = true;
                Assert(ev.X == archer.X && ev.Y == archer.Y, "Launch position is source position.");
            }
            if (ev.Type == CombatEventType.Hit && ev.TargetId == "target")
            {
                hits++;
                damage += ev.Damage;
                Assert(sim.Elapsed > launchTime + 2f, "Projectile must travel before its first impact.");
                Assert(ev.X == enemy.X && ev.Y == enemy.Y, "Hit position is current target position.");
            }
        };
        for (int tick = 0; tick < 220; tick++)
        {
            sim.Step(Tick);
            Near(enemy.Hp, enemyDefinition.MaxHp - damage, "HP only changes through Hit events.");
            if (tick < 15) Assert(!launched, "Windup must complete before launch.");
        }
        Assert(launched && hits > 0, "Ranged attack eventually lands.");
    }

    private static void DeadTargetsCancelPendingAttacks()
    {
        var archerDefinition = Definition("slow-arrow", Team.Ally, AttackKind.Ranged);
        archerDefinition.Windup = 0f;
        archerDefinition.ProjectileSpeed = 1f;
        var knightDefinition = Definition("knight", Team.Ally, AttackKind.Melee);
        knightDefinition.Damage = 200f;
        var closeDefinition = Definition("close", Team.Enemy, AttackKind.Melee);
        closeDefinition.MoveSpeed = 0f;
        closeDefinition.Damage = 0f;
        var farDefinition = Definition("far", Team.Enemy, AttackKind.Melee);
        farDefinition.MoveSpeed = 0f;
        farDefinition.Damage = 0f;
        farDefinition.MaxHp = 1000f;
        var archer = new CombatUnit(archerDefinition, -3f, 0f);
        var knight = new CombatUnit(knightDefinition, 0f, 0f);
        var close = new CombatUnit(closeDefinition, 0.8f, 0f);
        var far = new CombatUnit(farDefinition, 3f, 0f);
        var sim = new CombatSimulation(new[] { archer, knight, close, far });
        bool closeDead = false;
        int closeDefeats = 0;
        int orphanHits = 0;
        int retargetedLaunches = 0;
        sim.Event += delegate(CombatEvent ev)
        {
            if (ev.Type == CombatEventType.Defeated && ev.TargetId == "close") { closeDead = true; closeDefeats++; }
            if (ev.Type == CombatEventType.Hit && ev.TargetId == "close" && closeDead) orphanHits++;
            if (ev.Type == CombatEventType.ProjectileLaunched && ev.SourceId == "slow-arrow" && ev.TargetId == "far")
                retargetedLaunches++;
        };
        for (int tick = 0; tick < 150; tick++) sim.Step(Tick);
        Assert(closeDefeats == 1, "A defeated target emits Defeated once.");
        Assert(orphanHits == 0, "In-flight attacks cannot damage a dead target.");
        Assert(retargetedLaunches > 0 && archer.TargetId == "far", "Survivors retarget living opponents.");
        Assert(close.AttackProgress == 0f && close.TargetId == null, "Dead unit has no pending attack.");
    }

    private static void EscapingTargetCancelsWindup()
    {
        var strikerDefinition = Definition("striker", Team.Ally, AttackKind.Melee);
        strikerDefinition.MoveSpeed = 0f;
        strikerDefinition.Windup = 0.8f;
        var enemyDefinition = Definition("runner", Team.Enemy, AttackKind.Melee);
        enemyDefinition.Damage = 0f;
        enemyDefinition.MoveSpeed = 0f;
        var striker = new CombatUnit(strikerDefinition, 0f, 0f);
        var enemy = new CombatUnit(enemyDefinition, 0.8f, 0f);
        var sim = new CombatSimulation(new[] { striker, enemy });
        for (int tick = 0; tick < 10; tick++) sim.Step(Tick);
        Assert(striker.AttackProgress > 0f, "Attack should be winding up.");
        enemy.X = 4f;
        for (int tick = 0; tick < 90; tick++) sim.Step(Tick);
        Near(enemy.Hp, enemyDefinition.MaxHp, "An out-of-range target cannot receive a stale melee hit.");
        Assert(striker.AttackProgress == 0f, "Leaving range cancels windup.");
    }

    private static void SpeedSchedulingPreservesBattle()
    {
        var normal = RunScheduledBattle(1);
        var fast = RunScheduledBattle(2);
        Assert(normal == fast, "1x and 2x render schedules must produce identical simulation events and state.");
    }

    private static string RunScheduledBattle(int ticksPerFrame)
    {
        var knight = Definition("knight", Team.Ally, AttackKind.Melee);
        knight.MaxHp = 260f;
        knight.Damage = 26f;
        var archer = Definition("archer", Team.Ally, AttackKind.Ranged);
        archer.Damage = 18f;
        var boss = Definition("boss", Team.Enemy, AttackKind.Melee);
        boss.MaxHp = 750f;
        boss.Damage = 8f;
        boss.Radius = 0.45f;
        var sim = new CombatSimulation(new[]
        {
            new CombatUnit(knight, -3f, -0.3f),
            new CombatUnit(archer, -4f, 0.8f),
            new CombatUnit(boss, 3f, -0.1f)
        });
        var log = new System.Text.StringBuilder();
        int endEvents = 0;
        sim.Event += delegate(CombatEvent ev)
        {
            log.Append(ev.Type).Append('|').Append(ev.SourceId).Append('|').Append(ev.TargetId)
                .Append('|').Append(ev.Damage.ToString("R", CultureInfo.InvariantCulture))
                .Append('|').Append(sim.Elapsed.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            if (ev.Type == CombatEventType.BattleEnded) endEvents++;
        };
        for (int frame = 0; frame < 3600 && !sim.IsFinished; frame++)
            for (int tick = 0; tick < ticksPerFrame; tick++) sim.Step(Tick);
        Assert(sim.IsFinished && sim.Winner == Team.Ally && endEvents == 1, "The test encounter ends in one ally victory.");
        foreach (var unit in sim.Units)
            log.Append(unit.Definition.Id).Append(':').Append(unit.Hp.ToString("R", CultureInfo.InvariantCulture))
                .Append(':').Append(unit.X.ToString("R", CultureInfo.InvariantCulture))
                .Append(':').Append(unit.Y.ToString("R", CultureInfo.InvariantCulture));
        Console.WriteLine("  " + ticksPerFrame + "x schedule: victory at " + sim.Elapsed.ToString("F2", CultureInfo.InvariantCulture) + " simulation seconds.");
        return log.ToString();
    }

    private static void BoundariesAndOverlapsStayFinite()
    {
        var a = new CombatUnit(Definition("a", Team.Ally, AttackKind.Melee), -20f, 20f);
        var b = new CombatUnit(Definition("b", Team.Enemy, AttackKind.Melee), -20f, 20f);
        var sim = new CombatSimulation(new[] { a, b });
        for (int tick = 0; tick < 120; tick++) sim.Step(Tick);
        foreach (var unit in sim.Units)
            Assert(!float.IsNaN(unit.X) && !float.IsNaN(unit.Y) && unit.X >= -7f && unit.X <= 7f && unit.Y >= -2.8f && unit.Y <= 1.3f,
                "Overlapping actors must stay finite and within battlefield bounds.");
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        Assert(dx * dx + dy * dy >= 0.249f, "Actors with identical spawn points separate.");
    }

    private static void FinishedBattleEmitsOnce()
    {
        var sim = new CombatSimulation(new[] { new CombatUnit(Definition("solo", Team.Ally, AttackKind.Melee), 0f, 0f) });
        int events = 0;
        sim.Event += delegate(CombatEvent ev) { if (ev.Type == CombatEventType.BattleEnded) events++; };
        for (int tick = 0; tick < 60; tick++) sim.Step(Tick);
        Assert(events == 1 && sim.Winner == Team.Ally, "A finished battle must emit BattleEnded only once.");
        Assert(sim.Elapsed == 0f, "Already-ended combat does not advance time.");
    }

    private static void PreparationChoicesCanBeUndone()
    {
        var prep = new ExpeditionPreparation();
        Assert(prep.Train(0) && prep.Progress == 1, "First reserve raises training progress.");
        Assert(prep.Inherit(2, 0) && prep.InheritedTrait == NativeTrait.Ember, "Mage passes only its native trait.");
        Assert(!prep.Train(2), "Consumed donor cannot be used twice.");
        Assert(prep.Train(1) && prep.Progress == 2, "Second donor enables evolution.");
        Assert(prep.Evolve(KnightEvolution.CounterKnight), "Evolution branch is chosen in preparation.");
        Assert(prep.Undo() && prep.Evolution == KnightEvolution.None, "Undo restores branch selection.");
        Assert(prep.Undo() && prep.Progress == 1 && prep.IsReserveAvailable(1), "Undo restores training material.");
        Assert(prep.Undo() && prep.InheritedTrait == NativeTrait.None && prep.IsReserveAvailable(2), "Undo restores passed trait and donor.");
        Assert(prep.Undo() && prep.Progress == 0 && prep.IsReserveAvailable(0), "Undo restores first training material.");
        Assert(prep.Inherit(0, 1) && prep.PriestInheritedTrait == NativeTrait.Resolve,
            "Reserve knight can pass its native trait to priest.");
        Assert(prep.Undo() && prep.PriestInheritedTrait == NativeTrait.None && prep.IsReserveAvailable(0),
            "Undo restores recipient trait and knight donor.");
        prep.Commit();
        Assert(!prep.CanUndo, "Combat commits preparation choices.");
    }

    private static void BrokenShieldTriggersOneCounter()
    {
        var knight = Definition("counter", Team.Ally, AttackKind.Melee);
        knight.Role = UnitRole.ShieldKnight;
        knight.Evolution = KnightEvolution.CounterKnight;
        knight.MaxHp = 200f;
        knight.Damage = 20f;
        knight.MoveSpeed = 0f;
        var enemy = Definition("foe", Team.Enemy, AttackKind.Melee);
        enemy.MaxHp = 500f;
        enemy.Damage = 60f;
        enemy.MoveSpeed = 0f;
        var secondEnemy = Definition("nearby", Team.Enemy, AttackKind.Melee);
        secondEnemy.MaxHp = 500f;
        secondEnemy.Damage = 0f;
        secondEnemy.MoveSpeed = 0f;
        var shieldBearer = new CombatUnit(knight, -.8f, 0f);
        var foe = new CombatUnit(enemy, 0f, 0f);
        var nearby = new CombatUnit(secondEnemy, 0f, -.8f);
        var sim = new CombatSimulation(new[] { shieldBearer, foe, nearby });
        int breaks = 0, counters = 0;
        sim.Event += delegate(CombatEvent ev)
        {
            if (ev.Type == CombatEventType.ShieldBroken) breaks++;
            if (ev.Type == CombatEventType.Counterattack) counters++;
        };
        for (int i = 0; i < 90; i++) sim.Step(Tick);
        Assert(breaks == 1 && counters == 1, "Direct shield break triggers one counter.");
        Assert(shieldBearer.Hp < knight.MaxHp && foe.Hp < enemy.MaxHp, "Overflow reaches HP and counter damages attacker.");
        Assert(nearby.Hp < secondEnemy.MaxHp && nearby.SlowRemaining > 0f,
            "Frontal counter wave damages and slows a second enemy.");
    }

    private static void GuardianProtectsNearbyAlly()
    {
        var knight = Definition("guardian", Team.Ally, AttackKind.Melee);
        knight.Role = UnitRole.ShieldKnight;
        knight.Evolution = KnightEvolution.GuardianKnight;
        knight.MoveSpeed = 0f;
        var priest = Definition("priest", Team.Ally, AttackKind.Ranged);
        priest.MoveSpeed = 0f;
        var enemy = Definition("dummy", Team.Enemy, AttackKind.Melee);
        enemy.MaxHp = 10000f;
        enemy.Damage = 0f;
        enemy.MoveSpeed = 0f;
        var ally = new CombatUnit(priest, -2f, 0f);
        var sim = new CombatSimulation(new[] { new CombatUnit(knight, -1f, 0f), ally,
            new CombatUnit(enemy, 1f, 0f) });
        for (int i = 0; i < 310; i++) sim.Step(Tick);
        Assert(ally.Shield > 0f, "Guardian's timed protection reaches a nearby ally.");
    }

    private static void GolemTelegraphLocksDirection()
    {
        var boss = Definition("golem", Team.Enemy, AttackKind.Melee);
        boss.Role = UnitRole.GateGolem;
        boss.MaxHp = 10000f;
        boss.Damage = 20f;
        boss.MoveSpeed = 0f;
        boss.Range = 0f;
        var ally = Definition("bait", Team.Ally, AttackKind.Melee);
        ally.Damage = 0f;
        ally.MoveSpeed = 0f;
        var second = Definition("second", Team.Ally, AttackKind.Melee);
        second.Damage = 0f;
        second.MoveSpeed = 0f;
        var bait = new CombatUnit(ally, -2f, 0f);
        var other = new CombatUnit(second, -2f, 2f);
        var sim = new CombatSimulation(new[] { bait, other, new CombatUnit(boss, 0f, 0f) });
        int telegraphs = 0, slams = 0;
        sim.Event += delegate(CombatEvent ev)
        {
            if (ev.Type == CombatEventType.BossTelegraph)
            {
                telegraphs++;
                Assert(ev.TargetId == "bait", "Boss targets nearest ally at windup start.");
                bait.Y = -2.5f;
                other.Y = 0f;
            }
            if (ev.Type == CombatEventType.BossSlam) slams++;
        };
        for (int i = 0; i < 295; i++) sim.Step(Tick);
        Assert(telegraphs == 1 && slams == 1, "Boss warns for 0.8 seconds, then releases once.");
        Assert(bait.Hp == ally.MaxHp && other.Hp < second.MaxHp, "Slam keeps locked direction despite target movement.");
    }

    private static void PriestFollowsWithinShieldRange()
    {
        var knight = Definition("knight", Team.Ally, AttackKind.Melee);
        knight.Role = UnitRole.ShieldKnight;
        knight.MoveSpeed = 0f;
        knight.Damage = 0f;
        var priest = Definition("priest", Team.Ally, AttackKind.Ranged);
        priest.Role = UnitRole.LanternPriest;
        priest.MaxHp = 200f;
        priest.Damage = 0f;
        var enemy = Definition("dummy", Team.Enemy, AttackKind.Melee);
        enemy.MaxHp = 10000f;
        enemy.Damage = 0f;
        enemy.MoveSpeed = 0f;
        var front = new CombatUnit(knight, -1f, 0f);
        var support = new CombatUnit(priest, -5.5f, 0f);
        var sim = new CombatSimulation(new[] { front, support, new CombatUnit(enemy, 2f, 0f) });
        int shields = 0;
        sim.Event += delegate(CombatEvent ev)
        { if (ev.Type == CombatEventType.ShieldApplied && ev.SourceId == "priest" && ev.TargetId == "knight") shields++; };
        for (int i = 0; i < 310; i++) sim.Step(Tick);
        Assert(Math.Abs(support.X - front.X) <= 4f && shields > 0,
            "Priest reaches the frontliner and applies its shield at first cooldown.");
    }

    private static void BurnUsesLatestApplierAttack()
    {
        var low = Definition("low", Team.Ally, AttackKind.Ranged);
        low.Damage = 10f;
        low.Windup = 0f;
        low.Cooldown = 100f;
        low.ProjectileSpeed = 100f;
        low.NativeTrait = NativeTrait.Ember;
        var high = Definition("high", Team.Ally, AttackKind.Ranged);
        high.Damage = 100f;
        high.Windup = 1f;
        high.Cooldown = 100f;
        high.ProjectileSpeed = 100f;
        high.NativeTrait = NativeTrait.Ember;
        var enemy = Definition("dummy", Team.Enemy, AttackKind.Melee);
        enemy.MaxHp = 10000f;
        enemy.Damage = 0f;
        enemy.MoveSpeed = 0f;
        var target = new CombatUnit(enemy, 1f, 0f);
        var sim = new CombatSimulation(new[] { new CombatUnit(low, -2f, 0f),
            new CombatUnit(high, -2f, -.7f), target });
        for (int i = 0; i < 90; i++) sim.Step(Tick);
        Assert(target.BurnStacks == 2 && Math.Abs(target.BurnDamage - 20f) < .01f,
            "Latest applier's attack sets burn tick damage for all current stacks.");
    }

    private static void GolemHoldsPositionDuringTelegraph()
    {
        var boss = Definition("golem", Team.Enemy, AttackKind.Melee);
        boss.Role = UnitRole.GateGolem;
        boss.MaxHp = 10000f;
        boss.Damage = 0f;
        boss.MoveSpeed = .7f;
        boss.Range = 0f;
        var ally = Definition("bait", Team.Ally, AttackKind.Melee);
        ally.Damage = 0f;
        ally.MoveSpeed = 0f;
        var sim = new CombatSimulation(new[] { new CombatUnit(ally, -4f, 0f),
            new CombatUnit(boss, 3f, 0f) });
        float warnedX = float.NaN;
        int slams = 0;
        sim.Event += delegate(CombatEvent ev)
        {
            if (ev.Type == CombatEventType.BossTelegraph) warnedX = sim.Units[1].X;
            if (ev.Type == CombatEventType.BossSlam)
            {
                slams++;
                Assert(Math.Abs(sim.Units[1].X - warnedX) < .001f,
                    "Golem does not move away from the visible cone origin during windup.");
            }
        };
        for (int i = 0; i < 295; i++) sim.Step(Tick);
        Assert(slams == 1, "Moving golem releases one warned slam.");
    }

    private static void FrequentBurnRefreshKeepsTickClock()
    {
        var members = new List<CombatUnit>();
        for (int i = 0; i < 4; i++)
        {
            var caster = Definition("ember" + i, Team.Ally, AttackKind.Ranged);
            caster.NativeTrait = NativeTrait.Ember;
            caster.Damage = 10f;
            caster.Windup = i * .25f;
            caster.Cooldown = 100f;
            caster.ProjectileSpeed = 100f;
            members.Add(new CombatUnit(caster, -2f, -.8f + i * .4f));
        }
        var dummy = Definition("dummy", Team.Enemy, AttackKind.Melee);
        dummy.MaxHp = 10000f;
        dummy.Damage = 0f;
        dummy.MoveSpeed = 0f;
        members.Add(new CombatUnit(dummy, 2f, 0f));
        var sim = new CombatSimulation(members);
        int burnTicks = 0;
        sim.Event += delegate(CombatEvent ev)
        { if (ev.Type == CombatEventType.Hit && ev.SourceId == null && ev.TargetId == "dummy") burnTicks++; };
        for (int i = 0; i < 80; i++) sim.Step(Tick);
        Assert(burnTicks > 0, "Frequent burn refreshes cannot postpone the one-second damage tick indefinitely.");
    }

    private static void StageTwoBattleFlowAndScore()
    {
        var stage = new StageFlow(new Random(14));
        Assert(stage.Phase == StagePhase.Map && stage.OpenBriefing() && stage.Begin(),
            "Map node opens briefing and starts wave one preparation.");
        Assert(stage.BattleNumber == 1 && stage.StartBattle(), "First wave can start.");
        Assert(stage.CompleteBattle(true, 3, 3) && stage.Phase == StagePhase.Recruitment,
            "First victory offers recruitment before wave two.");
        Assert(stage.Candidates.Count == 3 && stage.Candidates[0] != stage.Candidates[1] &&
            stage.Candidates[0] != stage.Candidates[2] && stage.Candidates[1] != stage.Candidates[2],
            "Draft contains three distinct archetypes.");
        var recruited = stage.Candidates[1];
        Assert(stage.ChooseRecruit(1) && stage.Recruited == recruited && stage.BattleNumber == 2,
            "Selected candidate joins the second wave.");
        Assert(stage.StartBattle() && stage.CompleteBattle(true, 3, 4, true), "Second victory finishes stage.");
        Assert(stage.Phase == StagePhase.Result && stage.Cleared && stage.Score == 96 && stage.Stars == 2,
            "One final casualty still clears with two stars and a surviving recruit.");
        Assert(stage.ReturnMap() && stage.Phase == StagePhase.Map, "Result returns to map without auto-start.");
        Assert(StageFlow.CalculateScore(3, 3, 4, 4, true) == 100 &&
            StageFlow.CalculateScore(2, 3, 4, 4, true) == 97 &&
            StageFlow.CalculateScore(3, 3, 3, 4, false) == 86,
            "Both wave survival and recruited ally survival change score without timing pressure.");
    }

    private static void StageLossAndDraftVariation()
    {
        var stage = new StageFlow(new Random(30));
        stage.OpenBriefing(); stage.Begin(); stage.StartBattle();
        Assert(stage.CompleteBattle(false, 0, 3) && stage.Phase == StagePhase.Result &&
            stage.Score == 0 && stage.Stars == 0, "A loss awards no score or stars.");
        Assert(stage.ReturnMap(), "A failed run returns to map.");
        var signatures = new HashSet<string>();
        for (int seed = 0; seed < 20; seed++)
        {
            var attempt = new StageFlow(new Random(seed));
            attempt.OpenBriefing(); attempt.Begin(); attempt.StartBattle(); attempt.CompleteBattle(true, 3, 3);
            signatures.Add(attempt.Candidates[0] + "," + attempt.Candidates[1] + "," + attempt.Candidates[2]);
        }
        Assert(signatures.Count > 1, "Recruit candidates vary between random runs.");
    }

    private static void StageCatalogAndRecords()
    {
        Assert(StageCatalog.All.Length == 3 && StageCatalog.ForestEntrance.Waves.Length == 2 &&
            StageCatalog.ForestEntrance.Waves[0].RecruitAfter && StageCatalog.ForestEntrance.Playable &&
            !StageCatalog.RuinedOutpost.Playable && !StageCatalog.CastleGate.Playable,
            "Stage data defines the playable two-wave slice and later placeholders.");
        var progress = new StageProgress();
        Assert(progress.IsUnlocked(StageCatalog.ForestEntrance) &&
            !progress.IsUnlocked(StageCatalog.RuinedOutpost) && !progress.IsUnlocked(StageCatalog.CastleGate),
            "Prerequisite clears gate the route.");
        progress.RecordFor(StageCatalog.ForestEntrance).RecordClear(86, 2);
        Assert(progress.IsUnlocked(StageCatalog.RuinedOutpost) && !progress.IsUnlocked(StageCatalog.CastleGate),
            "First clear opens the next node only.");
        progress.RecordFor(StageCatalog.ForestEntrance).RecordClear(79, 1);
        Assert(progress.RecordFor(StageCatalog.ForestEntrance).BestScore == 86 &&
            progress.RecordFor(StageCatalog.ForestEntrance).BestStars == 2,
            "Best score and stars never regress on a lower clear.");
        var stage = new StageFlow(new Random(8));
        Assert(!stage.OpenBriefing(StageCatalog.RuinedOutpost) && stage.Phase == StagePhase.Map,
            "Unimplemented stage data cannot launch as a battle.");
        var future = new StageDefinition("test", "THREE WAVES", null, StageObjective.RecruitSurvives, true,
            new WaveDefinition("FIRST", 100, 10, 1, 1, false),
            new WaveDefinition("SECOND", 100, 10, 1, 1, true),
            new WaveDefinition("THIRD", 100, 10, 1, 1, false));
        Assert(stage.OpenBriefing(future) && stage.Begin() && stage.StartBattle() &&
            stage.CompleteBattle(true, 3, 3) && stage.BattleNumber == 2 && stage.StartBattle() &&
            stage.CompleteBattle(true, 3, 3) && stage.Phase == StagePhase.Recruitment &&
            stage.ChooseRecruit(0) && stage.BattleNumber == 3,
            "A draft after wave two advances to wave three without returning to wave two.");
    }

    private static void Near(float actual, float expected, string message)
    {
        Assert(Math.Abs(actual - expected) < 0.0001f, message);
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
}
