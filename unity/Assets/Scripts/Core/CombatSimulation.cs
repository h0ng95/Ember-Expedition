using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Expedition.Core
{
    public enum Team { Ally, Enemy }
    public enum AttackKind { Melee, Ranged }
    public enum CombatEventType { AttackStarted, ProjectileLaunched, Hit, Defeated, BattleEnded }

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
    }

    public sealed class CombatUnit
    {
        public UnitDefinition Definition { get; private set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Hp { get; internal set; }
        public bool IsAlive { get { return Hp > 0f; } }
        public string TargetId { get; internal set; }

        // Normalized windup progress; zero while moving, recovering, or dead.
        public float AttackProgress { get; internal set; }

        internal bool IsWindingUp;
        internal float WindupElapsed;
        internal float CooldownRemaining;

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

        public IReadOnlyList<CombatUnit> Units { get { return units; } }
        public event Action<CombatEvent> Event;
        public bool IsFinished { get; private set; }
        public Team? Winner { get; private set; }
        public float Elapsed { get; private set; }

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

            foreach (var unit in units)
            {
                if (!unit.IsAlive) continue;
                unit.CooldownRemaining = Math.Max(0f, unit.CooldownRemaining - dt);
                EnsureTarget(unit);
                var target = GetLivingTarget(unit);
                if (target == null) continue;
                if (!InRange(unit, target))
                {
                    CancelWindup(unit);
                    MoveTowards(unit, target, dt);
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

        private void EnsureTarget(CombatUnit unit)
        {
            if (GetLivingTarget(unit) != null) return;
            CancelWindup(unit);
            unit.TargetId = null;
            float closestDistance = float.PositiveInfinity;
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
            float travel = Math.Min(unit.Definition.MoveSpeed * dt, Math.Max(0f, distance - stopDistance));
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
            unit.CooldownRemaining = unit.Definition.Cooldown;
            if (unit.Definition.AttackKind == AttackKind.Melee)
            {
                ApplyHit(unit.Definition.Id, target, unit.Definition.Damage);
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
                    ApplyHit(projectile.SourceId, target, projectile.Damage);
                    continue;
                }
                projectile.X += dx / distance * travel;
                projectile.Y += dy / distance * travel;
                i++;
            }
        }

        private void ApplyHit(string sourceId, CombatUnit target, float damage)
        {
            if (!target.IsAlive) return;
            float actualDamage = Math.Min(target.Hp, damage);
            target.Hp -= actualDamage;
            Emit(CombatEventType.Hit, sourceId, target.Definition.Id, target.X, target.Y, actualDamage);
            if (!target.IsAlive)
            {
                CancelWindup(target);
                target.TargetId = null;
                Emit(CombatEventType.Defeated, sourceId, target.Definition.Id, target.X, target.Y, 0f);
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
}
