using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public sealed partial class CatalogBattle
    {
        const float ScreenXPerWorld = 83f, ScreenYPerWorld = 48f;
        const float LeftLimit = -6.4f, RightLimit = 6.4f;
        const float BottomLimit = -2.35f, TopLimit = 2.35f;
        readonly List<CatalogBattleUnit> spacingOrder = new List<CatalogBattleUnit>();
        bool spacingOrderDirty = true;

        static float BodyWidth(CatalogBattleUnit unit)
        {
            if (unit.IsBoss) return unit.EnemyRole == CatalogEnemyRole.Beast ? 280f : 210f;
            if (unit.Team == CatalogTeam.Ally) return 70f;
            if (unit.Id.StartsWith("spine-", StringComparison.Ordinal)) return 110f;
            if (unit.Id.IndexOf("elite", StringComparison.Ordinal) >= 0)
                return unit.Id.StartsWith("stone-", StringComparison.Ordinal) ? 130f : 200f;
            if (unit.Id.StartsWith("crossbow-", StringComparison.Ordinal)) return 100f;
            return 100f;
        }

        static float BodyHeight(CatalogBattleUnit unit)
        {
            if (unit.IsBoss) return unit.EnemyRole == CatalogEnemyRole.Beast ? 210f : 220f;
            if (unit.Team == CatalogTeam.Ally) return 80f;
            if (unit.Id.StartsWith("spine-", StringComparison.Ordinal)) return 100f;
            if (unit.Id.IndexOf("elite", StringComparison.Ordinal) >= 0)
                return unit.Id.StartsWith("stone-", StringComparison.Ordinal) ? 145f : 125f;
            if (unit.Id.StartsWith("crossbow-", StringComparison.Ordinal)) return 115f;
            return 70f;
        }

        static bool NeedsBodySpacing(CatalogBattleUnit a, CatalogBattleUnit b)
        {
            if (!a.IsAlive || !b.IsAlive || a.ChargeMoving || b.ChargeMoving ||
                a.Team == CatalogTeam.Ally && b.Team == CatalogTeam.Ally) return false;
            // Bosses retain their authored charge/slam positions and may overlap allies.
            if (a.IsBoss && b.Team == CatalogTeam.Ally ||
                b.IsBoss && a.Team == CatalogTeam.Ally) return false;
            return true;
        }

        static void BodyLimits(CatalogBattleUnit a, CatalogBattleUnit b, out float x, out float y)
        {
            // A little silhouette overlap keeps crowded formations compact.
            // Limit it against the smaller actor so an elite cannot hide a cub.
            x = ((BodyWidth(a) + BodyWidth(b)) * .5f -
                .3f * Math.Min(BodyWidth(a), BodyWidth(b))) / ScreenXPerWorld;
            float frontHeight = a.Y > b.Y ? BodyHeight(b) : b.Y > a.Y ? BodyHeight(a) :
                Math.Max(BodyHeight(a), BodyHeight(b));
            y = (frontHeight - .3f * Math.Min(BodyHeight(a), BodyHeight(b))) / ScreenYPerWorld;
        }

        // Only melee contact uses body reach. Ranged, area, support and raw skill
        // distances continue to use their catalog values.
        bool InBasicContact(CatalogBattleUnit actor, CatalogBattleUnit target)
        {
            if (Distance(actor, target) <= actor.Range + .001f) return true;
            if (actor.Range > 2.1f || actor.IsBoss || target.IsBoss ||
                !NeedsBodySpacing(actor, target)) return false;
            float x, y;
            BodyLimits(actor, target, out x, out y);
            return Math.Abs(actor.X - target.X) <= x + .12f &&
                Math.Abs(actor.Y - target.Y) <= y + .12f;
        }

        // A rear attacker that keeps walking into a larger friendly body must
        // clear its upper or lower edge before continuing toward the target.
        // This changes only its route, never attack or charge timing.
        bool SteerAroundFriendlyBlocker(CatalogBattleUnit actor, CatalogBattleUnit target,
            ref float dx, ref float dy)
        {
            if (actor.Team != CatalogTeam.Enemy || actor.IsBoss || Math.Abs(dx) < .01f) return false;
            float nearest = float.PositiveInfinity, flank = 0f;
            foreach (CatalogBattleUnit blocker in units)
            {
                if (blocker == actor || !blocker.IsAlive || blocker.Team != actor.Team ||
                    blocker.ChargeMoving || (target.X - actor.X) * (blocker.X - actor.X) <= 0f)
                    continue;
                float gapX = Math.Abs(blocker.X - actor.X);
                if (gapX >= Math.Abs(target.X - actor.X) || gapX >= nearest) continue;
                float limitX, limitY;
                BodyLimits(actor, blocker, out limitX, out limitY);
                float verticalGap = Math.Abs(actor.Y - blocker.Y);
                if (gapX > limitX + .16f || verticalGap > limitY + .35f)
                    continue;
                float side = actor.Y > blocker.Y ? 1f : actor.Y < blocker.Y ? -1f :
                    string.CompareOrdinal(actor.Id, blocker.Id) < 0 ? -1f : 1f;
                float escape = blocker.Y + side * (limitY + .14f);
                if (escape < BottomLimit || escape > TopLimit || Math.Abs(escape - actor.Y) > 1.2f)
                    continue;
                nearest = gapX;
                // Hold the clear lane until X passes the blocker; otherwise a
                // direct target vector immediately walks back into its body.
                flank = verticalGap >= limitY + .1f ? 0f : escape - actor.Y;
            }
            if (float.IsPositiveInfinity(nearest)) return false;
            if (flank != 0f) dx = 0f;
            dy = flank;
            return true;
        }

        void ResolveBodySpacing()
        {
            if (spacingOrderDirty)
            {
                spacingOrder.Clear();
                spacingOrder.AddRange(units);
                spacingOrder.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
                spacingOrderDirty = false;
            }
            for (int pass = 0; pass < 30; pass++)
            {
                bool changed = false;
                for (int i = 0; i < spacingOrder.Count; i++)
                for (int j = i + 1; j < spacingOrder.Count; j++)
                {
                    CatalogBattleUnit a = spacingOrder[i], b = spacingOrder[j];
                    if (!NeedsBodySpacing(a, b)) continue;
                    float limitX, limitY;
                    BodyLimits(a, b, out limitX, out limitY);
                    float dx = a.X - b.X, dy = a.Y - b.Y;
                    float overlapX = limitX - Math.Abs(dx), overlapY = limitY - Math.Abs(dy);
                    if (overlapX <= .0001f || overlapY <= .0001f) continue;
                    // Prefer the shorter correction, but use the other axis when
                    // the first would push a body beyond the visible battlefield.
                    // Actors advancing from the same side fan out across the lane.
                    // Pushing them backwards along X delays their first contact and
                    // can put a short charge outside its intended impact range.
                    bool preferX = a.Team == b.Team ?
                        Math.Abs(a.Y) > 1.5f || Math.Abs(b.Y) > 1.5f : overlapX <= overlapY;
                    changed |= TryPush(a, b, preferX, preferX ? overlapX : overlapY) ||
                        TryPush(a, b, !preferX, preferX ? overlapY : overlapX);
                }
                if (!changed) break;
            }
        }

        static bool TryPush(CatalogBattleUnit a, CatalogBattleUnit b, bool horizontal, float overlap)
        {
            float delta = horizontal ? a.X - b.X : a.Y - b.Y;
            float sign = delta < 0 ? -1f : delta > 0 ? 1f : -1f;
            float minimum = horizontal ? LeftLimit : BottomLimit;
            float maximum = horizontal ? RightLimit : TopLimit;
            float aPosition = horizontal ? a.X : a.Y, bPosition = horizontal ? b.X : b.Y;
            float aCapacity = a.IsBoss ? 0f : Math.Max(0f, sign > 0 ? maximum - aPosition : aPosition - minimum);
            float bCapacity = b.IsBoss ? 0f : Math.Max(0f, sign > 0 ? bPosition - minimum : maximum - bPosition);
            float needed = overlap + .001f;
            if (aCapacity + bCapacity < needed - .0001f) return false;
            float aShare = a.Team == CatalogTeam.Ally && b.Team == CatalogTeam.Enemy ? 0f :
                a.Team == CatalogTeam.Enemy && b.Team == CatalogTeam.Ally ? 1f :
                a.IsBoss ? 0f : b.IsBoss ? 1f :
                BodyWidth(b) * BodyHeight(b) / (BodyWidth(a) * BodyHeight(a) + BodyWidth(b) * BodyHeight(b));
            float aMove = Math.Min(needed * aShare, aCapacity);
            float bMove = Math.Min(needed - aMove, bCapacity);
            aMove = Math.Min(needed - bMove, aCapacity);
            if (horizontal) { a.X += sign * aMove; b.X -= sign * bMove; }
            else { a.Y += sign * aMove; b.Y -= sign * bMove; }
            return true;
        }
    }
}
