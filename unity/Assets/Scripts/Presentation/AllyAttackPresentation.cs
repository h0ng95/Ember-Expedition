using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Presentation only: the simulation's release/damage event is authoritative.
    public static class AllyAttackPresentation
    {
        public const float ProjectileFlight = .18f;
        public const float PoseDuration = .06f;
        public static bool Projectile(CatalogBattleUnit unit) { return unit.Range > 2.1f; }
        public static bool InFlight(CatalogBattleUnit unit)
        {
            return unit.IsAlive && unit.FrozenRemaining <= 0f && Projectile(unit) &&
                unit.AttackPhase == CatalogAttackPhase.WindingBasic &&
                unit.AttackWindupRemaining <= ProjectileFlight && unit.ChargeWarningRemaining <= 0f;
        }
        public static int ReleaseFrame(float age)
        { return 3 + Mathf.Clamp((int)(age / PoseDuration), 0, 2); }
        public static int AttackFrame(CatalogBattleUnit unit, float windupAge,
            float releaseAge, float releaseRemaining)
        {
            // Fire with the projectile, recover as it travels; do not fire again on arrival.
            if (InFlight(unit)) return ReleaseFrame(ProjectileFlight - unit.AttackWindupRemaining);
            if (releaseRemaining > 0f) return ReleaseFrame(releaseAge);
            float anticipation = unit.AttackPhase == CatalogAttackPhase.WindingBasic && Projectile(unit)
                ? .25f - ProjectileFlight : .25f;
            return Mathf.Clamp((int)(windupAge / anticipation * 3f), 0, 2);
        }
        public static int State(bool alive, bool frozen, bool attacking, bool hit, bool moving)
        {
            // Non-stunning damage still flashes, but cannot replace an ongoing attack pose.
            return !alive ? 4 : frozen ? 0 : attacking ? 2 : hit ? 3 : moving ? 1 : 0;
        }
    }
}
