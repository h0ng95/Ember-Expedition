using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public enum CatalogTeam { Ally, Enemy }
    public enum CatalogAttackPhase { Ready, WindingBasic, WindingSkill }
    public enum CatalogEventKind { SkillUsed, BasicHit, Damage, Shield, ShieldBlocked, Heal, Burn, Slow, Mark,
        Buff, TraitTriggered, Defeated, Ended }

    public sealed class CatalogBattleEvent
    {
        public CatalogEventKind Kind { get; internal set; }
        public string SourceId { get; internal set; }
        public string TargetId { get; internal set; }
        public string AbilityId { get; internal set; }
        public float Amount { get; internal set; }
        public float Time { get; internal set; }
    }

    internal enum CatalogBuffKind { Power, Haste, DamageReduction }
    internal sealed class CatalogBuff
    {
        public CatalogBuffKind Kind;
        public float Amount, Until;
    }

    public sealed class CatalogBattleUnit
    {
        public string Id { get; internal set; }
        public CatalogTeam Team { get; internal set; }
        public string FormId { get; internal set; }
        public string BranchId { get; internal set; }
        public string InheritedTraitId { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Hp { get; internal set; }
        public float MaxHp { get; internal set; }
        public float Shield { get; internal set; }
        public float Power { get; internal set; }
        public float Defense { get; internal set; }
        public float AttackInterval { get; internal set; }
        public float Range { get; internal set; }
        public float MoveSpeed { get; internal set; }
        public bool IsBoss { get; internal set; }
        public bool IsAlive { get { return Hp > 0f; } }
        public CatalogAttackPhase AttackPhase { get; internal set; }
        public float BurnRemaining { get { return BurnTicksLeft > 0 ? Math.Max(0f, BurnUntil - Clock.Time) : 0f; } }
        public float SlowPercent { get { return SlowUntil > Clock.Time ? Slow : 0f; } }
        public float MarkRemaining { get { return Math.Max(0f, MarkUntil - Clock.Time); } }

        internal CatalogBattle Clock;
        internal string InnateTraitId, PendingSkillId, PendingTargetId, VolleyTargetId, BurnSourceId, ShieldSourceId;
        internal float ShieldUntil, BurnUntil, BurnNextAt, BurnDamage, Slow, SlowUntil, MarkUntil;
        internal int BurnTicksLeft, VolleyShotsLeft;
        internal float NextBasicAt, ReleaseAt, NextPrimaryAt = 2f, NextFinalAt = 2f, NextCounterAt,
            NextHarmonyAt, NextSpoilsAt, NextVolleyAt, ExtraBasicUntil, ExtraBasicMultiplier;
        internal bool FirstHitUsed, CrisisUsed;
        internal readonly List<CatalogBuff> Buffs = new List<CatalogBuff>();
        internal float ZoneX, ZoneY, ZoneNextAt;
        internal int ZoneTicksLeft;
    }

    public sealed partial class CatalogBattle
    {
        const float Tick = 1f / 60f;
        readonly CatalogDatabase database;
        readonly List<CatalogBattleUnit> units = new List<CatalogBattleUnit>();
        readonly Dictionary<string, CatalogBattleUnit> byId = new Dictionary<string, CatalogBattleUnit>(StringComparer.Ordinal);
        float accumulator;
        public event Action<CatalogBattleEvent> Event;
        public IReadOnlyList<CatalogBattleUnit> Units { get { return units.AsReadOnly(); } }
        public float Time { get; private set; }
        public bool Ended { get; private set; }
        public CatalogTeam? Winner { get; private set; }

        public CatalogBattle(CatalogDatabase database)
        {
            if (database == null) throw new ArgumentNullException("database");
            this.database = database;
        }

        public CatalogBattleUnit AddUnit(string id, CatalogTeam team, string formId, string branchId,
            string inheritedTraitId, float x, float y)
        {
            CatalogForm form = database.Get(formId);
            if (formId.StartsWith("F-", StringComparison.Ordinal))
            {
                if (branchId == null || !branchId.StartsWith("E-" + CatalogRoster.FamilyCode(formId) + "-", StringComparison.Ordinal))
                    throw new ArgumentException("Final form must retain its chosen branch skill.");
                database.Get(branchId);
            }
            else if (branchId != null && branchId != formId)
                throw new ArgumentException("Only final forms accept a separate branch ID.");
            return Add(id, team, formId, branchId, inheritedTraitId, x, y,
                form.Hp, form.Power, form.Defense, form.AttackInterval, form.Range, form.MoveSpeed,
                form.InnateTrait, false);
        }

        public CatalogBattleUnit AddEnemy(string id, float hp, float power, float defense,
            float attackInterval, float range, float moveSpeed, bool boss, float x, float y)
        {
            if (hp <= 0 || power <= 0 || defense < 0 || attackInterval <= .25f || range <= 0 || moveSpeed <= 0)
                throw new ArgumentException("Invalid enemy stats.");
            return Add(id, CatalogTeam.Enemy, boss ? "EN-BOSS" : "EN-MOB", null, null, x, y,
                hp, power, defense, attackInterval, range, moveSpeed, null, boss);
        }

        CatalogBattleUnit Add(string id, CatalogTeam team, string formId, string branchId, string inheritedTraitId,
            float x, float y, float hp, float power, float defense, float interval, float range, float moveSpeed,
            string innateTraitId, bool boss)
        {
            if (Ended || string.IsNullOrEmpty(id) || byId.ContainsKey(id)) throw new ArgumentException("Duplicate or invalid actor ID.");
            var unit = new CatalogBattleUnit
            {
                Id = id, Team = team, FormId = formId, BranchId = branchId,
                InheritedTraitId = inheritedTraitId, InnateTraitId = innateTraitId,
                X = x, Y = y, Hp = hp, MaxHp = hp, Power = power, Defense = defense,
                AttackInterval = interval, Range = range, MoveSpeed = moveSpeed,
                IsBoss = boss, Clock = this, NextBasicAt = .25f
            };
            units.Add(unit); byId.Add(id, unit);
            if (HasTrait(unit, "T-PR")) GiveShield(unit, unit, unit.MaxHp * .1f, 4f, "T-PR");
            return unit;
        }

        public CatalogBattleUnit Get(string id)
        {
            CatalogBattleUnit unit;
            return id != null && byId.TryGetValue(id, out unit) ? unit : null;
        }

        public void Step(float dt)
        {
            if (dt < 0 || float.IsNaN(dt) || float.IsInfinity(dt)) throw new ArgumentOutOfRangeException("dt");
            if (Ended) return;
            accumulator += dt;
            int steps = 0;
            while (accumulator + .000001f >= Tick && steps++ < 36000 && !Ended)
            {
                accumulator -= Tick;
                Time += Tick;
                TickOnce();
            }
        }

        void TickOnce()
        {
            foreach (CatalogBattleUnit unit in units)
            {
                if (!unit.IsAlive) continue;
                if (unit.Shield > 0 && unit.ShieldUntil <= Time) unit.Shield = 0;
                if (unit.SlowUntil <= Time) unit.Slow = 0;
                for (int b = unit.Buffs.Count - 1; b >= 0; b--)
                    if (unit.Buffs[b].Until <= Time) unit.Buffs.RemoveAt(b);
                if (unit.BurnTicksLeft > 0 && unit.BurnNextAt <= Time)
                {
                    CatalogBattleUnit source = Get(unit.BurnSourceId);
                    Damage(source, unit, unit.BurnDamage, "BURN", false, true);
                    unit.BurnTicksLeft--;
                    unit.BurnNextAt += 1f;
                    if (unit.BurnTicksLeft == 0) unit.BurnUntil = Time;
                }
            }
            foreach (CatalogBattleUnit unit in units)
            {
                if (!unit.IsAlive) continue;
                TickZone(unit);
                TickVolley(unit);
                if (unit.AttackPhase != CatalogAttackPhase.Ready && Time >= unit.ReleaseAt)
                {
                    if (unit.AttackPhase == CatalogAttackPhase.WindingBasic) ReleaseBasic(unit);
                    else ReleaseSkill(unit);
                    unit.AttackPhase = CatalogAttackPhase.Ready;
                }
                if (unit.VolleyShotsLeft > 0) continue; // One tracking cast finishes before basic/final actions resume.
                if (!unit.IsAlive || unit.AttackPhase != CatalogAttackPhase.Ready) continue;
                if (TryStartSkill(unit)) continue;
                CatalogBattleUnit target = ChooseEnemy(unit, false, false);
                if (target == null) continue;
                if (Distance(unit, target) > unit.Range)
                {
                    MoveToward(unit, target);
                    continue;
                }
                if (Time + .25f + .0001f >= unit.NextBasicAt)
                {
                    unit.PendingTargetId = target.Id;
                    unit.AttackPhase = CatalogAttackPhase.WindingBasic;
                    unit.ReleaseAt = Math.Max(Time + .25f, unit.NextBasicAt);
                }
            }
            CheckEnded();
        }

        void ReleaseBasic(CatalogBattleUnit actor)
        {
            CatalogBattleUnit target = Get(actor.PendingTargetId);
            if (target == null || !target.IsAlive || target.Team == actor.Team) target = ChooseEnemy(actor, false, false);
            actor.NextBasicAt = Time + actor.AttackInterval * (1f - Math.Min(.4f, BuffTotal(actor, CatalogBuffKind.Haste)));
            if (target == null || Distance(actor, target) > actor.Range + .001f) return;
            float coefficient = 1f;
            if (actor.ExtraBasicUntil > Time)
            {
                coefficient += actor.ExtraBasicMultiplier;
                actor.ExtraBasicUntil = actor.ExtraBasicMultiplier = 0;
            }
            float bonus = 0f;
            if (HasTrait(actor, "T-AR") && target.MarkRemaining > 0) bonus += .15f;
            if (HasTrait(actor, "T-H01") && target.BurnRemaining > 0) bonus += .2f;
            coefficient *= 1f + Math.Min(1f, bonus);
            Emit(CatalogEventKind.BasicHit, actor, target, "BASIC", coefficient);
            bool alreadySlowed = target.SlowPercent > 0;
            Damage(actor, target, EffectivePower(actor) * coefficient, "BASIC", true, false);
            if (target.IsAlive)
            {
                if (HasTrait(actor, "T-FI")) Burn(actor, target);
                if (HasTrait(actor, "T-IC")) Slow(actor, target, .2f, 2f);
                if (HasTrait(actor, "T-H03") && alreadySlowed) Mark(actor, target, 3f);
            }
        }

        void MoveToward(CatalogBattleUnit actor, CatalogBattleUnit target)
        {
            float distance = Distance(actor, target);
            if (distance < .0001f) return;
            float travel = Math.Min(distance - actor.Range, actor.MoveSpeed * (1f - actor.SlowPercent) * Tick);
            actor.X += (target.X - actor.X) / distance * travel;
            actor.Y += (target.Y - actor.Y) / distance * travel;
        }

        CatalogBattleUnit ChooseEnemy(CatalogBattleUnit actor, bool lowestHp, bool farthest)
        {
            CatalogBattleUnit best = null;
            float bestValue = 0f, bestDistance = 0f;
            bool markedInRange = false;
            if (actor.Range > 2.1f)
                foreach (CatalogBattleUnit candidate in units)
                    if (candidate.IsAlive && candidate.Team != actor.Team && candidate.MarkRemaining > 0 &&
                        Distance(actor, candidate) <= actor.Range) markedInRange = true;
            foreach (CatalogBattleUnit candidate in units)
            {
                if (!candidate.IsAlive || candidate.Team == actor.Team) continue;
                float distance = Distance(actor, candidate);
                if ((lowestHp || farthest) && distance > actor.Range) continue;
                if (!lowestHp && !farthest && markedInRange &&
                    (candidate.MarkRemaining <= 0 || distance > actor.Range)) continue;
                float value = lowestHp ? candidate.Hp / candidate.MaxHp : farthest ? -distance : distance;
                if (best == null || value < bestValue - .00001f ||
                    (lowestHp && Math.Abs(value - bestValue) <= .00001f && distance < bestDistance - .00001f))
                { best = candidate; bestValue = value; bestDistance = distance; }
            }
            return best;
        }

        float EffectivePower(CatalogBattleUnit actor)
        { return actor.Power * (1f + Math.Min(1f, BuffTotal(actor, CatalogBuffKind.Power))); }

        float BuffTotal(CatalogBattleUnit actor, CatalogBuffKind kind)
        {
            float total = 0;
            foreach (CatalogBuff buff in actor.Buffs)
                if (buff.Kind == kind && buff.Until > Time) total += buff.Amount;
            return total;
        }

        void GiveBuff(CatalogBattleUnit source, CatalogBattleUnit target, CatalogBuffKind kind,
            float value, float duration, string ability)
        {
            target.Buffs.Add(new CatalogBuff { Kind = kind, Amount = value, Until = Time + duration });
            Emit(CatalogEventKind.Buff, source, target, ability, value);
            TryHarmony(source, target, ability);
        }

        void Damage(CatalogBattleUnit source, CatalogBattleUnit target, float raw, string ability,
            bool basic, bool periodic)
        {
            if (!target.IsAlive || raw <= 0) return;
            if (!target.FirstHitUsed && HasTrait(target, "T-KN"))
            {
                target.FirstHitUsed = true;
                GiveBuff(target, target, CatalogBuffKind.DamageReduction, .2f, 3f, "T-KN");
                Emit(CatalogEventKind.TraitTriggered, target, target, "T-KN", .2f);
            }
            float remaining = 1f;
            foreach (CatalogBuff buff in target.Buffs)
                if (buff.Kind == CatalogBuffKind.DamageReduction && buff.Until > Time)
                    remaining *= 1f - buff.Amount;
            float reduction = Math.Min(.5f, 1f - remaining);
            float damage = raw * 100f / (100f + target.Defense) * (1f - reduction);
            float blocked = Math.Min(target.Shield, damage);
            bool broke = blocked > 0 && blocked >= target.Shield - .00001f;
            target.Shield -= blocked;
            if (blocked > 0) Emit(CatalogEventKind.ShieldBlocked, Get(target.ShieldSourceId), target, ability, blocked);
            float hpLoss = Math.Min(target.Hp, damage - blocked);
            target.Hp -= hpLoss;
            Emit(CatalogEventKind.Damage, source, target, ability, hpLoss + blocked);
            if (!target.IsAlive)
            {
                target.Hp = 0;
                target.ZoneTicksLeft = 0;
                Emit(CatalogEventKind.Defeated, source, target, ability, 0);
                if (source != null && source.IsAlive && HasTrait(source, "T-H06") && Time >= source.NextSpoilsAt)
                {
                    CatalogBattleUnit ally = LowestAlly(source, 3f, false, true);
                    if (ally != null && Heal(source, ally, EffectivePower(source), "T-H06"))
                    { source.NextSpoilsAt = Time + 4f; Emit(CatalogEventKind.TraitTriggered, source, ally, "T-H06", 1); }
                }
                return;
            }
            if (broke && HasCounterSkill(target) && Time >= target.NextCounterAt)
            {
                target.NextCounterAt = Time + 2.5f;
                Counter(target, source);
            }
            if (!target.CrisisUsed && target.Hp <= target.MaxHp * .5f && HasTrait(target, "T-BA"))
            {
                target.CrisisUsed = true;
                GiveShield(target, target, target.MaxHp * .12f, 3f, "T-BA");
                Emit(CatalogEventKind.TraitTriggered, target, target, "T-BA", 1);
            }
        }

        bool GiveShield(CatalogBattleUnit source, CatalogBattleUnit target, float value, float duration, string ability)
        {
            if (!target.IsAlive || value <= target.Shield + .00001f) return false;
            target.Shield = value; target.ShieldUntil = Time + duration; target.ShieldSourceId = source.Id;
            Emit(CatalogEventKind.Shield, source, target, ability, value);
            TryHarmony(source, target, ability);
            return true;
        }

        bool Heal(CatalogBattleUnit source, CatalogBattleUnit target, float amount, string ability)
        {
            if (target == null || !target.IsAlive) return false;
            if (HasTrait(target, "T-H02")) amount *= 1.15f;
            float actual = Math.Min(target.MaxHp - target.Hp, amount);
            if (actual <= .00001f) return false;
            target.Hp += actual;
            Emit(CatalogEventKind.Heal, source, target, ability, actual);
            TryHarmony(source, target, ability);
            return true;
        }

        void Burn(CatalogBattleUnit source, CatalogBattleUnit target)
        {
            if (!target.IsAlive) return;
            float strength = EffectivePower(source) * .15f;
            if (target.BurnTicksLeft > 0 && strength + .00001f < target.BurnDamage) return;
            if (target.BurnTicksLeft == 0) target.BurnNextAt = Time + 1f;
            target.BurnDamage = strength; target.BurnTicksLeft = 3; target.BurnUntil = Time + 3f;
            target.BurnSourceId = source.Id;
            Emit(CatalogEventKind.Burn, source, target, "BURN", strength);
        }

        void Slow(CatalogBattleUnit source, CatalogBattleUnit target, float value, float duration)
        {
            if (!target.IsAlive) return;
            value = Math.Min(.4f, value) * (target.IsBoss ? .5f : 1f);
            if (target.SlowUntil > Time && target.Slow > value + .00001f) return;
            target.Slow = value; target.SlowUntil = Time + duration;
            Emit(CatalogEventKind.Slow, source, target, "SLOW", value);
        }

        void Mark(CatalogBattleUnit source, CatalogBattleUnit target, float duration)
        {
            if (!target.IsAlive) return;
            target.MarkUntil = Math.Max(target.MarkUntil, Time + duration);
            Emit(CatalogEventKind.Mark, source, target, "MARK", duration);
        }

        bool HasTrait(CatalogBattleUnit unit, string trait)
        { return unit != null && (unit.InnateTraitId == trait || unit.InheritedTraitId == trait); }

        bool HasCounterSkill(CatalogBattleUnit unit)
        { return unit.FormId == "E-KN-C" || (unit.FormId == "F-KN" && unit.BranchId == "E-KN-C"); }

        CatalogBattleUnit LowestAlly(CatalogBattleUnit source, float range, bool otherOnly, bool needsHealing)
        {
            CatalogBattleUnit best = null;
            float ratio = float.PositiveInfinity, bestDistance = float.PositiveInfinity;
            foreach (CatalogBattleUnit candidate in units)
            {
                if (!candidate.IsAlive || candidate.Team != source.Team || (otherOnly && candidate == source) ||
                    Distance(source, candidate) > range || (needsHealing && candidate.Hp >= candidate.MaxHp)) continue;
                float current = candidate.Hp / candidate.MaxHp;
                float distance = Distance(source, candidate);
                if (current < ratio - .00001f ||
                    (Math.Abs(current - ratio) <= .00001f && distance < bestDistance - .00001f))
                { best = candidate; ratio = current; bestDistance = distance; }
            }
            return best;
        }

        static float Distance(CatalogBattleUnit a, CatalogBattleUnit b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        void CheckEnded()
        {
            bool allies = false, enemies = false;
            foreach (CatalogBattleUnit unit in units)
            {
                if (!unit.IsAlive) continue;
                if (unit.Team == CatalogTeam.Ally) allies = true; else enemies = true;
            }
            if (units.Count < 2 || allies && enemies) return;
            Ended = true;
            Winner = allies ? CatalogTeam.Ally : enemies ? CatalogTeam.Enemy : (CatalogTeam?)null;
            Emit(CatalogEventKind.Ended, null, null, "END", 0);
        }

        void Emit(CatalogEventKind kind, CatalogBattleUnit source, CatalogBattleUnit target, string ability, float amount)
        {
            Action<CatalogBattleEvent> handler = Event;
            if (handler != null) handler(new CatalogBattleEvent
            {
                Kind = kind, SourceId = source == null ? null : source.Id,
                TargetId = target == null ? null : target.Id,
                AbilityId = ability, Amount = amount, Time = Time
            });
        }
    }
}
