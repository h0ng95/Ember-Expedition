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
            Console.WriteLine("PASS: 6 combat scenarios; " + assertions + " assertions.");
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
