using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Expedition.Core
{
    public enum Team { Ally, Enemy }
    public enum AttackKind { Melee, Ranged }
    public enum UnitRole { Ordinary, ShieldKnight, LanternPriest, FireMage, GateGolem, OutpostArcher }
    public enum NativeTrait { None, Resolve, LanternBlessing, Ember }
    public enum KnightEvolution { None, CounterKnight, GuardianKnight }
    public enum CombatEventType { AttackStarted, ProjectileLaunched, Hit, Defeated, BattleEnded, ShieldApplied, ShieldBroken, Counterattack, BossTelegraph, BossSlam, BurnApplied }

    [Serializable]
    public sealed class UnitDefinition
    {
        public string Id;
        public Team Team;
        public AttackKind AttackKind;
        public float MaxHp;
        public float Damage;
        public float Range;
        public float MoveSpeed;
        public float Cooldown;
        public float Windup;
        public float ProjectileSpeed;
        public float Radius;
        public UnitRole Role;
        public NativeTrait NativeTrait;
        public NativeTrait InheritedTrait;
        public KnightEvolution Evolution;
    }

    public sealed class CombatUnit
    {
        public UnitDefinition Definition { get; private set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Hp { get; internal set; }
        public float Shield { get; internal set; }
        public bool IsAlive { get { return Hp > 0f; } }
        public string TargetId { get; internal set; }

        // Normalized windup progress; zero while moving, recovering, or dead.
        public float AttackProgress { get; internal set; }

        internal bool IsWindingUp;
        internal float WindupElapsed;
        internal float CooldownRemaining;
        internal float ShieldRemaining;
        internal float AbilityRemaining;
        internal float CounterRemaining;
        internal float BurnRemaining;
        internal float BurnTickRemaining;
        internal float BurnDamage;
        internal int BurnStacks;
        internal float EmberTargetRemaining;
        public float SlowRemaining { get; internal set; }
        public float SlowFactor { get; internal set; }
        internal bool ResolveUsed;

        public CombatUnit(UnitDefinition definition, float x, float y)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            Validate(definition, x, y);
            Definition = definition;
            X = x;
            Y = y;
            Hp = definition.MaxHp;
        }

        private static void Validate(UnitDefinition definition, float x, float y)
        {
            if (string.IsNullOrWhiteSpace(definition.Id))
                throw new ArgumentException("Every unit needs a non-empty Id.", "definition");
            if (!Finite(x) || !Finite(y) || !Finite(definition.MaxHp) || definition.MaxHp <= 0f ||
                !NonNegative(definition.Damage) || !NonNegative(definition.Range) ||
                !NonNegative(definition.MoveSpeed) || !Finite(definition.Cooldown) || definition.Cooldown <= 0f ||
                !NonNegative(definition.Windup) || !Finite(definition.Radius) || definition.Radius <= 0f ||
                !NonNegative(definition.ProjectileSpeed) ||
                (definition.AttackKind == AttackKind.Ranged && definition.ProjectileSpeed <= 0f))
                throw new ArgumentException("Unit stats and position must be finite and valid.", "definition");
        }

        private static bool NonNegative(float value) { return Finite(value) && value >= 0f; }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    public struct CombatEvent
    {
        public readonly CombatEventType Type;
        public readonly string SourceId;
        public readonly string TargetId;
        public readonly float X;
        public readonly float Y;
        public readonly float Damage;

        public CombatEvent(CombatEventType type, string sourceId, string targetId, float x, float y, float damage)
        {
            Type = type;
            SourceId = sourceId;
            TargetId = targetId;
            X = x;
            Y = y;
            Damage = damage;
        }
    }

    // Call Step with a fixed simulation interval. Playback speed changes the number
    // of steps, never the interval. Definitions must remain unchanged during battle.
    public sealed class CombatSimulation
    {
        private const float MinX = -7f;
        private const float MaxX = 7f;
        private const float MinY = -2.8f;
        private const float MaxY = 1.3f;
        private const float Epsilon = 0.0001f;
        private readonly ReadOnlyCollection<CombatUnit> units;
        private readonly Dictionary<string, CombatUnit> byId = new Dictionary<string, CombatUnit>(StringComparer.Ordinal);
        private readonly List<Projectile> projectiles = new List<Projectile>();
        private float nextBossSlam = 4f;
        private bool bossWinding;
        private float bossReleaseAt;
        private float bossDirectionX, bossDirectionY;
        private string bossId;

        public IReadOnlyList<CombatUnit> Units { get { return units; } }
        public event Action<CombatEvent> Event;
        public bool IsFinished { get; private set; }
        public Team? Winner { get; private set; }
        public float Elapsed { get; private set; }

        public void EndByTimeout()
        {
            if (IsFinished) return;
            IsFinished = true;
            Winner = Team.Enemy;
            projectiles.Clear();
            foreach (var unit in units) CancelWindup(unit);
            Emit(CombatEventType.BattleEnded, null, null, 0f, 0f, 0f);
        }

        public CombatSimulation(IEnumerable<CombatUnit> initialUnits)
        {
            if (initialUnits == null) throw new ArgumentNullException("initialUnits");
            var copy = new List<CombatUnit>();
            foreach (var unit in initialUnits)
            {
                if (unit == null) throw new ArgumentException("Units cannot contain null.", "initialUnits");
                if (byId.ContainsKey(unit.Definition.Id))
                    throw new ArgumentException("Unit Ids must be unique.", "initialUnits");
                byId.Add(unit.Definition.Id, unit);
                copy.Add(unit);
                ClampPosition(unit);
                if (unit.Definition.Role == UnitRole.ShieldKnight)
                {
                    unit.Shield = unit.Definition.MaxHp * .25f;
                    unit.ShieldRemaining = 6f;
                    if (unit.Definition.Evolution == KnightEvolution.GuardianKnight) unit.AbilityRemaining = 5f;
                }
                else if (unit.Definition.Role == UnitRole.LanternPriest) unit.AbilityRemaining = 5f;
                else if (unit.Definition.Role == UnitRole.FireMage) unit.AbilityRemaining = 4f;
                else if (unit.Definition.Role == UnitRole.GateGolem) bossId = unit.Definition.Id;
            }
            units = copy.AsReadOnly();
        }

        public void Step(float dt)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f)
                throw new ArgumentOutOfRangeException("dt", "Step duration must be finite and non-negative.");
            if (IsFinished || dt == 0f) return;
            if (FinishIfNecessary()) return;
            Elapsed += dt;
            UpdateStatusesAndAbilities(dt);
            UpdateBossAttack();
            if (FinishIfNecessary()) return;

            foreach (var unit in units)
            {
                if (!unit.IsAlive) continue;
                unit.CooldownRemaining = Math.Max(0f, unit.CooldownRemaining - dt);
                EnsureTarget(unit);
                var target = GetLivingTarget(unit);
                if (target == null) continue;
                if (unit.Definition.Role == UnitRole.LanternPriest)
                    FollowFrontliner(unit, dt);
                else if (!InRange(unit, target))
                {
                    CancelWindup(unit);
                    if (!(bossWinding && unit.Definition.Id == bossId)) MoveTowards(unit, target, dt);
                }
            }

            SeparateUnits();
            UpdateProjectiles(dt);
            if (FinishIfNecessary()) return;

            foreach (var unit in units)
            {
                if (!unit.IsAlive) continue;
                EnsureTarget(unit);
                var target = GetLivingTarget(unit);
                if (target == null || !InRange(unit, target))
                {
                    CancelWindup(unit);
                    continue;
                }

                if (unit.IsWindingUp)
                {
                    unit.WindupElapsed += dt;
                    unit.AttackProgress = Math.Min(1f, unit.WindupElapsed / Math.Max(Epsilon, unit.Definition.Windup));
                    if (unit.WindupElapsed + Epsilon >= unit.Definition.Windup)
                        ReleaseAttack(unit, target);
                }
                else if (unit.CooldownRemaining <= 0f)
                {
                    unit.IsWindingUp = true;
                    unit.WindupElapsed = 0f;
                    unit.AttackProgress = 0f;
                    Emit(CombatEventType.AttackStarted, unit.Definition.Id, target.Definition.Id, unit.X, unit.Y, 0f);
                    if (unit.Definition.Windup <= 0f) ReleaseAttack(unit, target);
                }
            }

            // A death cancels every preparation and projectile aimed at that unit,
            // including those whose actors already took their turn this step.
            foreach (var unit in units)
                if (unit.IsAlive) EnsureTarget(unit);
            RemoveOrphanedProjectiles();
            FinishIfNecessary();
        }

        private void UpdateStatusesAndAbilities(float dt)
        {
            foreach (var unit in units)
            {
                if (!unit.IsAlive) continue;
                unit.CounterRemaining = Math.Max(0f, unit.CounterRemaining - dt);
                unit.EmberTargetRemaining = Math.Max(0f, unit.EmberTargetRemaining - dt);
                unit.SlowRemaining = Math.Max(0f, unit.SlowRemaining - dt);
                if (unit.SlowRemaining <= 0f) unit.SlowFactor = 0f;
                if (unit.ShieldRemaining > 0f)
                {
                    unit.ShieldRemaining -= dt;
                    if (unit.ShieldRemaining <= 0f) unit.Shield = 0f;
                }
                if (unit.BurnRemaining > 0f)
                {
                    unit.BurnRemaining -= dt;
                    unit.BurnTickRemaining -= dt;
                    if (unit.BurnTickRemaining <= 0f)
                    {
                        unit.BurnTickRemaining += 1f;
                        ApplyHit(null, unit, unit.BurnDamage, false);
                    }
                    if (unit.BurnRemaining <= 0f) { unit.BurnDamage = 0f; unit.BurnStacks = 0; }
                }
                if (!unit.IsAlive) continue;
                if (unit.Definition.Role != UnitRole.LanternPriest && unit.Definition.Role != UnitRole.FireMage &&
                    unit.Definition.Evolution != KnightEvolution.GuardianKnight) continue;
                unit.AbilityRemaining -= dt;
                if (unit.AbilityRemaining > 0f) continue;
                if (unit.Definition.Role == UnitRole.LanternPriest)
                {
                    unit.AbilityRemaining += 5f;
                    CombatUnit chosen = null;
                    float lowest = float.PositiveInfinity;
                    foreach (var ally in units)
                    {
                        if (!ally.IsAlive || ally.Definition.Team != unit.Definition.Team ||
                            DistanceSquared(unit.X, unit.Y, ally.X, ally.Y) > 4f * 4f) continue;
                        float ratio = ally.Hp / ally.Definition.MaxHp;
                        if (ratio < lowest) { lowest = ratio; chosen = ally; }
                    }
                    if (chosen != null) ApplyShield(unit, chosen, unit.Definition.MaxHp * .15f, 4f);
                }
                else if (unit.Definition.Role == UnitRole.FireMage)
                {
                    unit.AbilityRemaining += 4f;
                    EnsureTarget(unit);
                    var target = GetLivingTarget(unit);
                    if (target == null || !InRange(unit, target)) continue;
                    foreach (var enemy in units)
                    {
                        if (!enemy.IsAlive || enemy.Definition.Team == unit.Definition.Team ||
                            DistanceSquared(target.X, target.Y, enemy.X, enemy.Y) > 1.25f * 1.25f) continue;
                        ApplyHit(unit.Definition.Id, enemy, unit.Definition.Damage * .8f, true);
                        ApplyBurn(unit, enemy);
                    }
                }
                else
                {
                    unit.AbilityRemaining += 5f;
                    CombatUnit chosen = null;
                    float closest = float.PositiveInfinity;
                    foreach (var ally in units)
                    {
                        if (!ally.IsAlive || ally == unit || ally.Definition.Team != unit.Definition.Team) continue;
                        float distance = DistanceSquared(unit.X, unit.Y, ally.X, ally.Y);
                        if (distance < closest && distance <= 4f * 4f) { closest = distance; chosen = ally; }
                    }
                    if (chosen != null) ApplyShield(unit, chosen, unit.Definition.MaxHp * .15f, 4f);
                }
            }
        }

        private void UpdateBossAttack()
        {
            if (bossId == null) return;
            CombatUnit boss;
            if (!byId.TryGetValue(bossId, out boss) || !boss.IsAlive) return;
            if (bossWinding && Elapsed + Epsilon >= bossReleaseAt)
            {
                bossWinding = false;
                Emit(CombatEventType.BossSlam, bossId, null, boss.X, boss.Y, 0f);
                foreach (var ally in units)
                {
                    if (!ally.IsAlive || ally.Definition.Team == boss.Definition.Team) continue;
                    float dx = ally.X - boss.X, dy = ally.Y - boss.Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (distance <= 3.2f && distance > Epsilon &&
                        (dx * bossDirectionX + dy * bossDirectionY) / distance >= .819f)
                        ApplyHit(bossId, ally, boss.Definition.Damage * 2f, true);
                }
            }
            if (bossWinding || Elapsed + Epsilon < nextBossSlam) return;
            CombatUnit closest = null;
            float best = float.PositiveInfinity;
            foreach (var ally in units)
            {
                if (!ally.IsAlive || ally.Definition.Team == boss.Definition.Team) continue;
                float distance = DistanceSquared(boss.X, boss.Y, ally.X, ally.Y);
                if (distance < best) { best = distance; closest = ally; }
            }
            if (closest == null) return;
            float vx = closest.X - boss.X, vy = closest.Y - boss.Y;
            float length = (float)Math.Sqrt(vx * vx + vy * vy);
            bossDirectionX = length <= Epsilon ? -1f : vx / length;
            bossDirectionY = length <= Epsilon ? 0f : vy / length;
            bossWinding = true;
            bossReleaseAt = Elapsed + .8f;
            nextBossSlam += 7f;
            Emit(CombatEventType.BossTelegraph, bossId, closest.Definition.Id, bossDirectionX, bossDirectionY, .8f);
        }

        private void FollowFrontliner(CombatUnit priest, float dt)
        {
            CombatUnit front = null;
            foreach (var unit in units)
                if (unit.IsAlive && unit != priest && unit.Definition.Team == priest.Definition.Team &&
                    unit.Definition.Role == UnitRole.ShieldKnight) { front = unit; break; }
            if (front == null) return;
            float dx = front.X - priest.X, dy = front.Y - priest.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distance <= 3.2f) return;
            float travel = Math.Min(priest.Definition.MoveSpeed * (1f - priest.SlowFactor) * dt, distance - 3.2f);
            priest.X += dx / distance * travel;
            priest.Y += dy / distance * travel;
            ClampPosition(priest);
        }

        private void EnsureTarget(CombatUnit unit)
        {
            if (GetLivingTarget(unit) != null) return;
            CancelWindup(unit);
            unit.TargetId = null;
            float closestDistance = float.PositiveInfinity;
            foreach (var candidate in units)
            {
                if (!candidate.IsAlive || candidate.Definition.Team == unit.Definition.Team) continue;
                // Outpost archers select a ranged ally before looking at the front line.
                if (unit.Definition.Role == UnitRole.OutpostArcher &&
                    candidate.Definition.AttackKind != AttackKind.Ranged) continue;
                float distance = DistanceSquared(unit.X, unit.Y, candidate.X, candidate.Y);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    unit.TargetId = candidate.Definition.Id;
                }
            }
            if (unit.TargetId != null || unit.Definition.Role != UnitRole.OutpostArcher) return;
            foreach (var candidate in units)
            {
                if (!candidate.IsAlive || candidate.Definition.Team == unit.Definition.Team) continue;
                float distance = DistanceSquared(unit.X, unit.Y, candidate.X, candidate.Y);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    unit.TargetId = candidate.Definition.Id;
                }
            }
        }

        private CombatUnit GetLivingTarget(CombatUnit unit)
        {
            CombatUnit target;
            return unit.TargetId != null && byId.TryGetValue(unit.TargetId, out target) && target.IsAlive
                ? target : null;
        }

        private static bool InRange(CombatUnit unit, CombatUnit target)
        {
            float range = unit.Definition.Range + unit.Definition.Radius + target.Definition.Radius;
            return DistanceSquared(unit.X, unit.Y, target.X, target.Y) <= range * range + Epsilon;
        }

        private static void MoveTowards(CombatUnit unit, CombatUnit target, float dt)
        {
            float dx = target.X - unit.X;
            float dy = target.Y - unit.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distance <= Epsilon) return;
            float stopDistance = unit.Definition.Range + unit.Definition.Radius + target.Definition.Radius;
            float travel = Math.Min(unit.Definition.MoveSpeed * (1f - unit.SlowFactor) * dt,
                Math.Max(0f, distance - stopDistance));
            unit.X += dx / distance * travel;
            unit.Y += dy / distance * travel;
            ClampPosition(unit);
        }

        private void SeparateUnits()
        {
            // Two deterministic passes resolve ordinary spawn/movement overlaps.
            // This is a small-arena approximation, not pathfinding or a physics solver.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    var a = units[i];
                    if (!a.IsAlive) continue;
                    for (int j = i + 1; j < units.Count; j++)
                    {
                        var b = units[j];
                        if (!b.IsAlive) continue;
                        float dx = b.X - a.X;
                        float dy = b.Y - a.Y;
                        float minimum = a.Definition.Radius + b.Definition.Radius;
                        float square = dx * dx + dy * dy;
                        if (square >= minimum * minimum) continue;
                        float distance = (float)Math.Sqrt(square);
                        if (distance < Epsilon) { dx = 1f; dy = 0f; distance = 0f; }
                        else { dx /= distance; dy /= distance; }
                        float correction = (minimum - distance) * 0.5f;
                        a.X -= dx * correction;
                        a.Y -= dy * correction;
                        b.X += dx * correction;
                        b.Y += dy * correction;
                        ClampPosition(a);
                        ClampPosition(b);
                    }
                }
            }
        }

        private void ReleaseAttack(CombatUnit unit, CombatUnit target)
        {
            CancelWindup(unit);
            // Cooldown begins at release, independently of render speed or hit FX.
            bool blessed = HasTrait(unit, NativeTrait.LanternBlessing) && unit.Shield > 0f;
            unit.CooldownRemaining = unit.Definition.Cooldown * (blessed ? .85f : 1f);
            if (unit.Definition.AttackKind == AttackKind.Melee)
            {
                ApplyHit(unit.Definition.Id, target, unit.Definition.Damage, true);
                if (HasTrait(unit, NativeTrait.Ember) && unit.EmberTargetRemaining <= 0f)
                { ApplyBurn(unit, target); unit.EmberTargetRemaining = 2f; }
                return;
            }

            projectiles.Add(new Projectile
            {
                SourceId = unit.Definition.Id,
                TargetId = target.Definition.Id,
                X = unit.X,
                Y = unit.Y,
                Speed = unit.Definition.ProjectileSpeed,
                Damage = unit.Definition.Damage
            });
            Emit(CombatEventType.ProjectileLaunched, unit.Definition.Id, target.Definition.Id, unit.X, unit.Y, 0f);
        }

        private void UpdateProjectiles(float dt)
        {
            // Iterate in launch order so simultaneous impacts have a stable order.
            for (int i = 0; i < projectiles.Count;)
            {
                var projectile = projectiles[i];
                CombatUnit target;
                if (!byId.TryGetValue(projectile.TargetId, out target) || !target.IsAlive)
                {
                    projectiles.RemoveAt(i);
                    continue;
                }
                float dx = target.X - projectile.X;
                float dy = target.Y - projectile.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                float travel = projectile.Speed * dt;
                if (distance <= travel + target.Definition.Radius)
                {
                    projectiles.RemoveAt(i);
                    ApplyHit(projectile.SourceId, target, projectile.Damage, true);
                    CombatUnit attacker;
                    if (byId.TryGetValue(projectile.SourceId, out attacker) &&
                        HasTrait(attacker, NativeTrait.Ember) && attacker.EmberTargetRemaining <= 0f)
                    { ApplyBurn(attacker, target); attacker.EmberTargetRemaining = 2f; }
                    continue;
                }
                projectile.X += dx / distance * travel;
                projectile.Y += dy / distance * travel;
                i++;
            }
        }

        private static bool HasTrait(CombatUnit unit, NativeTrait trait)
        { return unit.Definition.NativeTrait == trait || unit.Definition.InheritedTrait == trait; }

        private void ApplyShield(CombatUnit source, CombatUnit target, float value, float duration)
        {
            if (!target.IsAlive || value <= target.Shield) return;
            target.Shield = value;
            target.ShieldRemaining = duration;
            Emit(CombatEventType.ShieldApplied, source.Definition.Id, target.Definition.Id, target.X, target.Y, value);
        }

        private void ApplyBurn(CombatUnit source, CombatUnit target)
        {
            if (!target.IsAlive) return;
            if (target.BurnStacks == 0) target.BurnTickRemaining = 1f;
            target.BurnStacks = Math.Min(3, target.BurnStacks + 1);
            target.BurnDamage = source.Definition.Damage * .1f * target.BurnStacks;
            target.BurnRemaining = 3f;
            Emit(CombatEventType.BurnApplied, source.Definition.Id, target.Definition.Id, target.X, target.Y, target.BurnDamage);
        }

        private void ApplyHit(string sourceId, CombatUnit target, float damage, bool direct)
        {
            if (!target.IsAlive) return;
            float absorbed = 0f;
            bool shieldBroken = false;
            if (target.Shield > 0f)
            {
                absorbed = Math.Min(target.Shield, damage);
                target.Shield -= absorbed;
                damage -= absorbed;
                shieldBroken = direct && target.Shield <= Epsilon;
                if (target.Shield <= Epsilon) { target.Shield = 0f; target.ShieldRemaining = 0f; }
            }
            float actualDamage = Math.Min(target.Hp, damage);
            target.Hp -= actualDamage;
            Emit(CombatEventType.Hit, sourceId, target.Definition.Id, target.X, target.Y, actualDamage + absorbed);
            if (!target.IsAlive)
            {
                CancelWindup(target);
                target.TargetId = null;
                Emit(CombatEventType.Defeated, sourceId, target.Definition.Id, target.X, target.Y, 0f);
            }
            else
            {
                if (shieldBroken)
                {
                    Emit(CombatEventType.ShieldBroken, sourceId, target.Definition.Id, target.X, target.Y, 0f);
                    if (target.Definition.Evolution == KnightEvolution.CounterKnight && target.CounterRemaining <= 0f)
                    {
                        target.CounterRemaining = 3f;
                        Emit(CombatEventType.Counterattack, target.Definition.Id, null, target.X, target.Y, 0f);
                        CombatUnit attacker;
                        float facingX = target.Definition.Team == Team.Ally ? 1f : -1f, facingY = 0f;
                        if (sourceId != null && byId.TryGetValue(sourceId, out attacker))
                        {
                            float dx = attacker.X - target.X, dy = attacker.Y - target.Y;
                            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                            if (distance > Epsilon) { facingX = dx / distance; facingY = dy / distance; }
                        }
                        foreach (var enemy in units)
                        {
                            if (!enemy.IsAlive || enemy.Definition.Team == target.Definition.Team) continue;
                            float dx = enemy.X - target.X, dy = enemy.Y - target.Y;
                            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                            if (distance > 2.4f || distance <= Epsilon ||
                                (dx * facingX + dy * facingY) / distance < .5f) continue;
                            ApplyHit(target.Definition.Id, enemy, target.Definition.Damage * 1.2f, true);
                            if (enemy.IsAlive)
                            {
                                enemy.SlowFactor = enemy.Definition.Role == UnitRole.GateGolem ? .15f : .3f;
                                enemy.SlowRemaining = 2f;
                            }
                        }
                    }
                }
                if (HasTrait(target, NativeTrait.Resolve) && !target.ResolveUsed &&
                    target.Hp <= target.Definition.MaxHp * .4f)
                {
                    target.ResolveUsed = true;
                    ApplyShield(target, target, target.Definition.MaxHp * .2f, 4f);
                }
            }
        }

        private void RemoveOrphanedProjectiles()
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
                if (!byId[projectiles[i].TargetId].IsAlive) projectiles.RemoveAt(i);
        }

        private bool FinishIfNecessary()
        {
            if (IsFinished) return true;
            bool allies = false;
            bool enemies = false;
            foreach (var unit in units)
            {
                if (!unit.IsAlive) continue;
                if (unit.Definition.Team == Team.Ally) allies = true;
                else enemies = true;
            }
            if (allies && enemies) return false;
            IsFinished = true;
            Winner = allies ? (Team?)Team.Ally : enemies ? (Team?)Team.Enemy : null;
            projectiles.Clear();
            foreach (var unit in units)
            {
                CancelWindup(unit);
                unit.TargetId = null;
            }
            Emit(CombatEventType.BattleEnded, null, null, 0f, 0f, 0f);
            return true;
        }

        private static void CancelWindup(CombatUnit unit)
        {
            unit.IsWindingUp = false;
            unit.WindupElapsed = 0f;
            unit.AttackProgress = 0f;
        }

        private void Emit(CombatEventType type, string sourceId, string targetId, float x, float y, float damage)
        {
            var handler = Event;
            if (handler != null) handler(new CombatEvent(type, sourceId, targetId, x, y, damage));
        }

        private static float DistanceSquared(float ax, float ay, float bx, float by)
        {
            float dx = bx - ax;
            float dy = by - ay;
            return dx * dx + dy * dy;
        }

        private static void ClampPosition(CombatUnit unit)
        {
            unit.X = Math.Max(MinX, Math.Min(MaxX, unit.X));
            unit.Y = Math.Max(MinY, Math.Min(MaxY, unit.Y));
        }

        private sealed class Projectile
        {
            public string SourceId;
            public string TargetId;
            public float X;
            public float Y;
            public float Speed;
            public float Damage;
        }
    }

    // Small preparation loop for the first encounter. Every operation keeps a full
    // snapshot so a material choice can be undone until combat begins.
    public sealed class ExpeditionPreparation
    {
        private struct Snapshot
        {
            public int Progress;
            public int RemainingMask;
            public NativeTrait KnightTrait, PriestTrait, MageTrait;
            public KnightEvolution Evolution;
        }
        private readonly Stack<Snapshot> history = new Stack<Snapshot>();
        private int remainingMask = 7;
        public int Progress { get; private set; }
        public NativeTrait InheritedTrait { get; private set; }
        public NativeTrait PriestInheritedTrait { get; private set; }
        public NativeTrait MageInheritedTrait { get; private set; }
        public KnightEvolution Evolution { get; private set; }
        public bool CanUndo { get { return history.Count > 0; } }
        public bool IsReserveAvailable(int index) { return index >= 0 && index < 3 && (remainingMask & (1 << index)) != 0; }
        public NativeTrait ReserveTrait(int index)
        {
            if (index == 0) return NativeTrait.Resolve;
            if (index == 1) return NativeTrait.LanternBlessing;
            if (index == 2) return NativeTrait.Ember;
            throw new ArgumentOutOfRangeException("index");
        }
        public bool Train(int index)
        {
            if (!IsReserveAvailable(index) || Progress >= 2 || Evolution != KnightEvolution.None) return false;
            Save();
            remainingMask &= ~(1 << index);
            Progress++;
            return true;
        }
        public NativeTrait InheritedTraitFor(int recipient)
        {
            if (recipient == 0) return InheritedTrait;
            if (recipient == 1) return PriestInheritedTrait;
            if (recipient == 2) return MageInheritedTrait;
            throw new ArgumentOutOfRangeException("recipient");
        }
        public bool Inherit(int index, int recipient)
        {
            if (!IsReserveAvailable(index) || recipient < 0 || recipient > 2) return false;
            NativeTrait trait = ReserveTrait(index);
            if (trait == ReserveTrait(recipient) || trait == InheritedTraitFor(recipient)) return false;
            Save();
            remainingMask &= ~(1 << index);
            if (recipient == 0) InheritedTrait = trait;
            else if (recipient == 1) PriestInheritedTrait = trait;
            else MageInheritedTrait = trait;
            return true;
        }
        public bool Evolve(KnightEvolution branch)
        {
            if (Progress != 2 || Evolution != KnightEvolution.None || branch == KnightEvolution.None) return false;
            Save();
            Evolution = branch;
            return true;
        }
        public bool Undo()
        {
            if (history.Count == 0) return false;
            var old = history.Pop();
            Progress = old.Progress;
            remainingMask = old.RemainingMask;
            InheritedTrait = old.KnightTrait;
            PriestInheritedTrait = old.PriestTrait;
            MageInheritedTrait = old.MageTrait;
            Evolution = old.Evolution;
            return true;
        }
        public void Commit() { history.Clear(); }
        private void Save()
        {
            history.Push(new Snapshot { Progress = Progress, RemainingMask = remainingMask,
                KnightTrait = InheritedTrait, PriestTrait = PriestInheritedTrait,
                MageTrait = MageInheritedTrait, Evolution = Evolution });
        }
    }
}
