using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public sealed partial class CatalogBattle
    {
        static float SkillCooldown(string id)
        {
            switch (id)
            {
                case "B-PR": case "E-AR-P": case "E-AR-T": case "E-PR-G": case "E-PR-H":
                case "H-03": return 5f;
                case "E-IC-S": return 4f;
                case "B-BA": case "H-05": return 8f;
                case "E-FI-B": case "E-FI-E": case "E-IC-F": case "E-BA-G": case "E-BA-A":
                case "H-02": return 7f;
                case "F-AR": return 10f;
                case "F-IC": return 11f;
                case "F-KN": case "F-FI": case "F-PR": case "F-BA": return 12f;
                default: return 6f;
            }
        }

        bool TryStartSkill(CatalogBattleUnit actor)
        {
            if (actor.FormId.StartsWith("EN-", StringComparison.Ordinal)) return false;
            if (actor.FormId.StartsWith("F-", StringComparison.Ordinal) &&
                Time >= actor.NextFinalAt && StartSkill(actor, actor.FormId)) return true;
            string primary = actor.FormId.StartsWith("F-", StringComparison.Ordinal) ? actor.BranchId : actor.FormId;
            if (primary == "E-KN-C") return false; // Shield break is an immediate condition, not a timer.
            return Time >= actor.NextPrimaryAt && StartSkill(actor, primary);
        }

        bool StartSkill(CatalogBattleUnit actor, string skill)
        {
            CatalogBattleUnit target = null;
            bool support = skill == "B-PR" || skill == "B-BA" || skill == "E-KN-G" ||
                skill == "E-PR-G" || skill == "E-PR-H" || skill == "E-BA-G" || skill == "E-BA-A" ||
                skill == "F-KN" || skill == "F-PR" || skill == "F-BA" || skill == "H-05";
            if (support)
            {
                if (skill == "E-PR-H" && LowestAlly(actor, 4.8f, false, true) == null) return false;
                if (skill == "B-PR" && LowestAlly(actor, 4.5f, false, false) == null) return false;
            }
            else
            {
                bool low = skill == "B-AR" || skill == "E-AR-T";
                bool far = skill == "F-AR";
                target = ChooseEnemy(actor, low, far);
                if (target == null || !InBasicContact(actor, target)) return false;
            }
            actor.PendingSkillId = skill;
            actor.PendingTargetId = target == null ? null : target.Id;
            actor.AttackPhase = CatalogAttackPhase.WindingSkill;
            actor.ReleaseAt = Time + .25f;
            return true;
        }

        void ReleaseSkill(CatalogBattleUnit actor)
        {
            string skill = actor.PendingSkillId;
            CatalogBattleUnit target = actor.PendingTargetId == null ? null : Get(actor.PendingTargetId);
            if (skill == "E-PR-H" && LowestAlly(actor, 4.8f, false, true) == null)
                return; // Recovery waits if another healer restored the target during windup.
            if (target != null && (!target.IsAlive || target.Team == actor.Team)) target = null;
            if (target == null && !IsSupportSkill(skill)) target = ChooseEnemy(actor, false, false);
            if (!IsSupportSkill(skill) && (target == null || !InBasicContact(actor, target)))
                return; // A dead or escaped target does not consume the skill cooldown.
            if (skill == actor.FormId && actor.FormId.StartsWith("F-", StringComparison.Ordinal))
                actor.NextFinalAt = Time + SkillCooldown(skill);
            else actor.NextPrimaryAt = Time + SkillCooldown(skill);
            Emit(CatalogEventKind.SkillUsed, actor, target, skill, 1);
            ExecuteSkill(actor, target, skill);
        }

        static bool IsSupportSkill(string skill)
        {
            return skill == "B-PR" || skill == "B-BA" || skill == "E-KN-G" ||
                skill == "E-PR-G" || skill == "E-PR-H" || skill == "E-BA-G" || skill == "E-BA-A" ||
                skill == "F-KN" || skill == "F-PR" || skill == "F-BA" || skill == "H-05";
        }

        void ExecuteSkill(CatalogBattleUnit a, CatalogBattleUnit target, string id)
        {
            switch (id)
            {
                case "B-KN": Direct(a, target, 1.8f, id); Slow(a, target, .2f, 2f); break;
                case "B-AR": Direct(a, target, 1.2f, id); Mark(a, target, 5f); break;
                case "B-FI": CircleHit(a, target, 1f, 3, 1.6f, id, true, 0, 0); break;
                case "B-PR": ShieldLowest(a, 4.5f, 1, EffectivePower(a) * 1.8f, 4f, id); break;
                case "B-IC": CircleHit(a, target, 1.2f, 3, 1.3f, id, false, .25f, 3f); break;
                case "B-BA": BuffNear(a, 2.5f, 2, CatalogBuffKind.Power, .2f, 4f, id, true); break;
                case "E-KN-G":
                    GiveShield(a, a, EffectivePower(a) * 2.5f, 4f, id);
                    CatalogBattleUnit nearest = NearestOtherAlly(a, 2f);
                    if (nearest != null) GiveShield(a, nearest, EffectivePower(a) * 2.5f, 4f, id);
                    break;
                case "E-AR-P": LineHit(a, target, .5f, 3, 2.1f, id, 0, 0); break;
                case "E-AR-T":
                    Mark(a, target, 5f);
                    a.VolleyShotsLeft = 3; a.VolleyTargetId = target.Id; a.NextVolleyAt = Time;
                    TickVolley(a);
                    break;
                case "E-FI-B": CircleHit(a, target, 1.4f, 4, 2.8f, id, true, 0, 0); break;
                case "E-FI-E":
                    a.ZoneX = target.X; a.ZoneY = target.Y;
                    a.ZoneNextAt = Time + 1f; a.ZoneTicksLeft = 4;
                    break;
                case "E-PR-G": ShieldLowest(a, 4.8f, 2, EffectivePower(a) * 2.3f, 4f, id); break;
                case "E-PR-H": Heal(a, LowestAlly(a, 4.8f, false, true), EffectivePower(a) * 2.6f, id); break;
                case "E-IC-F": CircleHit(a, target, 1.6f, 4, 1.7f, id, false, .4f, 4f); break;
                case "E-IC-S": Direct(a, target, 1.9f, id); Slow(a, target, .3f, 2f); break;
                case "E-BA-G": BuffNear(a, 2.8f, 2, CatalogBuffKind.DamageReduction, .2f, 4f, id, true); break;
                case "E-BA-A": BuffNear(a, 2.8f, 2, CatalogBuffKind.Haste, .2f, 4f, id, true); break;
                case "F-KN":
                    GiveShield(a, a, a.MaxHp * .18f, 5f, id);
                    foreach (CatalogBattleUnit ally in LowestAllies(a, 2.5f, 2, true))
                        GiveShield(a, ally, a.MaxHp * .18f, 5f, id);
                    break;
                case "F-AR": Direct(a, target, 4.2f, id); Mark(a, target, 5f); break;
                case "F-FI":
                    CatalogBattleUnit dense = DensestEnemy(a);
                    if (dense != null) CircleHit(a, dense, 1.8f, 5, 3.3f, id, true, 0, 0);
                    break;
                case "F-PR":
                    foreach (CatalogBattleUnit ally in units)
                        if (ally.IsAlive && ally.Team == a.Team)
                        {
                            Heal(a, ally, EffectivePower(a) * 1.6f, id);
                            GiveShield(a, ally, EffectivePower(a) * 1.6f, 5f, id);
                        }
                    break;
                case "F-IC": CircleHit(a, target, 2f, 5, 2.6f, id, false, .4f, 4f); break;
                case "F-BA":
                    foreach (CatalogBattleUnit ally in units)
                        if (ally.IsAlive && ally.Team == a.Team)
                        {
                            GiveBuff(a, ally, CatalogBuffKind.Power, .2f, 5f, id);
                            GiveBuff(a, ally, CatalogBuffKind.DamageReduction, .15f, 5f, id);
                        }
                    break;
                case "H-01": ConeHit(a, target, 2f, 3, 2.4f, id, true, 0, 0); break;
                case "H-02":
                    Direct(a, target, 1.6f, id);
                    foreach (CatalogBattleUnit ally in LowestAllies(a, 2.5f, 2, false))
                        Heal(a, ally, EffectivePower(a) * 1.8f, id);
                    break;
                case "H-03": LineHit(a, target, .5f, 3, 2f, id, .3f, 3f); break;
                case "H-04":
                    // Both states land before direct damage; the native mastery applies to this cast.
                    foreach (CatalogBattleUnit enemy in CircleTargets(a, target.X, target.Y, 1.5f, 4))
                    {
                        Burn(a, enemy); Slow(a, enemy, .3f, 3f); Direct(a, enemy, 2f, id);
                    }
                    break;
                case "H-05":
                    Heal(a, a, EffectivePower(a) * 1.8f, id);
                    GiveBuff(a, a, CatalogBuffKind.Power, .15f, 4f, id);
                    foreach (CatalogBattleUnit ally in NearbyAllies(a, 3.2f, true, 2))
                    {
                        Heal(a, ally, EffectivePower(a) * 1.8f, id);
                        GiveBuff(a, ally, CatalogBuffKind.Power, .15f, 4f, id);
                    }
                    break;
                case "H-06":
                    Mark(a, target, 5f);
                    a.ExtraBasicMultiplier = .6f; a.ExtraBasicUntil = Time + 5f;
                    Emit(CatalogEventKind.Buff, a, a, id, .6f);
                    foreach (CatalogBattleUnit ally in NearbyAllies(a, 3f, true, 2))
                    {
                        ally.ExtraBasicMultiplier = .6f; ally.ExtraBasicUntil = Time + 5f;
                        Emit(CatalogEventKind.Buff, a, ally, id, .6f);
                        TryHarmony(a, ally, id);
                    }
                    break;
                default: throw new InvalidOperationException("No skill execution for form " + id);
            }
        }

        void Direct(CatalogBattleUnit a, CatalogBattleUnit target, float coefficient, string ability)
        {
            if (target == null || !target.IsAlive) return;
            float raw = EffectivePower(a) * coefficient;
            if (HasTrait(a, "T-H04") && target.BurnRemaining > 0 && target.SlowPercent > 0)
                raw *= 1.15f;
            Damage(a, target, raw, ability, false, false);
        }

        void CircleHit(CatalogBattleUnit a, CatalogBattleUnit center, float radius, int limit,
            float coefficient, string ability, bool burn, float slow, float slowSeconds)
        {
            foreach (CatalogBattleUnit target in CircleTargets(a, center.X, center.Y, radius, limit))
            {
                Direct(a, target, coefficient, ability);
                if (target.IsAlive && burn) Burn(a, target);
                if (target.IsAlive && slow > 0) Slow(a, target, slow, slowSeconds);
            }
        }

        List<CatalogBattleUnit> CircleTargets(CatalogBattleUnit a, float x, float y, float radius, int limit)
        {
            var result = new List<CatalogBattleUnit>();
            foreach (CatalogBattleUnit target in units)
            {
                float dx = target.X - x, dy = target.Y - y;
                if (target.IsAlive && target.Team != a.Team && dx * dx + dy * dy <= radius * radius + .00001f)
                    result.Add(target);
            }
            result.Sort((left, right) =>
            {
                float ld = (left.X - x) * (left.X - x) + (left.Y - y) * (left.Y - y);
                float rd = (right.X - x) * (right.X - x) + (right.Y - y) * (right.Y - y);
                int comparison = ld.CompareTo(rd);
                return comparison != 0 ? comparison : units.IndexOf(left).CompareTo(units.IndexOf(right));
            });
            if (result.Count > limit) result.RemoveRange(limit, result.Count - limit);
            return result;
        }

        void LineHit(CatalogBattleUnit a, CatalogBattleUnit aim, float width, int limit,
            float coefficient, string ability, float slow, float duration)
        {
            float dx = aim.X - a.X, dy = aim.Y - a.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < .0001f) return;
            dx /= length; dy /= length;
            var targets = new List<CatalogBattleUnit>();
            foreach (CatalogBattleUnit target in units)
            {
                if (!target.IsAlive || target.Team == a.Team) continue;
                float tx = target.X - a.X, ty = target.Y - a.Y;
                float forward = tx * dx + ty * dy;
                float perpendicular = Math.Abs(tx * dy - ty * dx);
                if (forward >= 0 && forward <= a.Range && perpendicular <= width * .5f + .00001f)
                    targets.Add(target);
            }
            targets.Sort((left, right) => CompareDistance(a, left, right));
            for (int i = 0; i < Math.Min(limit, targets.Count); i++)
            {
                Direct(a, targets[i], coefficient, ability);
                if (slow > 0 && targets[i].IsAlive) Slow(a, targets[i], slow, duration);
            }
        }

        void ConeHit(CatalogBattleUnit a, CatalogBattleUnit aim, float distance, int limit,
            float coefficient, string ability, bool burn, float slow, float duration)
        {
            float dx = aim.X - a.X, dy = aim.Y - a.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < .0001f) return;
            dx /= length; dy /= length;
            var targets = new List<CatalogBattleUnit>();
            foreach (CatalogBattleUnit target in units)
            {
                if (!target.IsAlive || target.Team == a.Team) continue;
                float radius = Distance(a, target);
                if (radius > .0001f && radius <= distance &&
                    ((target.X - a.X) * dx + (target.Y - a.Y) * dy) / radius >= .5f)
                    targets.Add(target);
            }
            targets.Sort((left, right) => CompareDistance(a, left, right));
            for (int i = 0; i < Math.Min(limit, targets.Count); i++)
            {
                Direct(a, targets[i], coefficient, ability);
                if (targets[i].IsAlive && burn) Burn(a, targets[i]);
                if (targets[i].IsAlive && slow > 0) Slow(a, targets[i], slow, duration);
            }
        }

        void Counter(CatalogBattleUnit a, CatalogBattleUnit attacker)
        {
            if (attacker == null) return;
            Emit(CatalogEventKind.SkillUsed, a, attacker, "E-KN-C", 1);
            ConeHit(a, attacker, 1.8f, 3, 2.2f, "E-KN-C", false, .2f, 2f);
        }

        void TickZone(CatalogBattleUnit a)
        {
            if (a.ZoneTicksLeft <= 0 || Time + .00001f < a.ZoneNextAt) return;
            foreach (CatalogBattleUnit enemy in CircleTargets(a, a.ZoneX, a.ZoneY, 1.6f, 4))
            {
                Direct(a, enemy, .65f, "E-FI-E");
                if (enemy.IsAlive) Burn(a, enemy);
            }
            a.ZoneTicksLeft--;
            a.ZoneNextAt += 1f;
        }

        void TickVolley(CatalogBattleUnit a)
        {
            if (a.VolleyShotsLeft <= 0 || Time + .00001f < a.NextVolleyAt) return;
            CatalogBattleUnit target = Get(a.VolleyTargetId);
            if (target == null || !target.IsAlive || target.Team == a.Team || Distance(a, target) > a.Range)
                target = ChooseEnemy(a, false, false);
            if (target != null && Distance(a, target) <= a.Range)
            {
                Direct(a, target, .9f, "E-AR-T");
                a.VolleyTargetId = target.Id;
            }
            a.VolleyShotsLeft--;
            a.NextVolleyAt += .15f;
        }

        CatalogBattleUnit DensestEnemy(CatalogBattleUnit a)
        {
            CatalogBattleUnit best = null;
            int bestCount = -1;
            foreach (CatalogBattleUnit candidate in units)
            {
                if (!candidate.IsAlive || candidate.Team == a.Team || Distance(a, candidate) > a.Range) continue;
                int count = CircleTargets(a, candidate.X, candidate.Y, 1.8f, 5).Count;
                if (count > bestCount || count == bestCount && best != null &&
                    Distance(a, candidate) < Distance(a, best) - .00001f)
                { best = candidate; bestCount = count; }
            }
            return best;
        }

        CatalogBattleUnit NearestOtherAlly(CatalogBattleUnit a, float range)
        {
            CatalogBattleUnit best = null;
            float distance = float.PositiveInfinity;
            foreach (CatalogBattleUnit ally in units)
            {
                if (!ally.IsAlive || ally.Team != a.Team || ally == a) continue;
                float current = Distance(a, ally);
                if (current <= range && current < distance) { best = ally; distance = current; }
            }
            return best;
        }

        List<CatalogBattleUnit> NearbyAllies(CatalogBattleUnit a, float range, bool otherOnly, int limit)
        {
            var result = new List<CatalogBattleUnit>();
            foreach (CatalogBattleUnit ally in units)
                if (ally.IsAlive && ally.Team == a.Team && (!otherOnly || ally != a) && Distance(a, ally) <= range)
                    result.Add(ally);
            result.Sort((left, right) => CompareDistance(a, left, right));
            if (result.Count > limit) result.RemoveRange(limit, result.Count - limit);
            return result;
        }

        List<CatalogBattleUnit> LowestAllies(CatalogBattleUnit a, float range, int limit, bool otherOnly)
        {
            var result = new List<CatalogBattleUnit>();
            foreach (CatalogBattleUnit ally in units)
                if (ally.IsAlive && ally.Team == a.Team && (!otherOnly || ally != a) && Distance(a, ally) <= range)
                    result.Add(ally);
            result.Sort((left, right) =>
            {
                int health = (left.Hp / left.MaxHp).CompareTo(right.Hp / right.MaxHp);
                return health != 0 ? health : CompareDistance(a, left, right);
            });
            if (result.Count > limit) result.RemoveRange(limit, result.Count - limit);
            return result;
        }

        void ShieldLowest(CatalogBattleUnit a, float range, int limit, float value, float duration, string ability)
        {
            var eligible = LowestAllies(a, range, 30, false);
            // Existing shields above 20% HP rank after exposed allies.
            eligible.Sort((left, right) =>
            {
                bool leftCovered = left.Shield >= left.MaxHp * .2f;
                bool rightCovered = right.Shield >= right.MaxHp * .2f;
                int coverage = leftCovered.CompareTo(rightCovered);
                if (coverage != 0) return coverage;
                int health = (left.Hp / left.MaxHp).CompareTo(right.Hp / right.MaxHp);
                return health != 0 ? health : CompareDistance(a, left, right);
            });
            for (int i = 0; i < Math.Min(limit, eligible.Count); i++) GiveShield(a, eligible[i], value, duration, ability);
        }

        void BuffNear(CatalogBattleUnit a, float range, int others, CatalogBuffKind kind,
            float amount, float duration, string ability, bool includeSelf)
        {
            if (includeSelf) GiveBuff(a, a, kind, amount, duration, ability);
            foreach (CatalogBattleUnit ally in NearbyAllies(a, range, true, others))
                GiveBuff(a, ally, kind, amount, duration, ability);
        }

        void TryHarmony(CatalogBattleUnit a, CatalogBattleUnit helped, string ability)
        {
            if (helped == a || ability == null || ability.StartsWith("T-", StringComparison.Ordinal) ||
                !HasTrait(a, "T-H05") || Time < a.NextHarmonyAt) return;
            a.NextHarmonyAt = Time + 6f;
            GiveShield(a, a, EffectivePower(a) * .8f, 2f, "T-H05");
            Emit(CatalogEventKind.TraitTriggered, a, a, "T-H05", 1);
        }

        int CompareDistance(CatalogBattleUnit origin, CatalogBattleUnit left, CatalogBattleUnit right)
        {
            int distance = Distance(origin, left).CompareTo(Distance(origin, right));
            return distance != 0 ? distance : units.IndexOf(left).CompareTo(units.IndexOf(right));
        }
    }
}
