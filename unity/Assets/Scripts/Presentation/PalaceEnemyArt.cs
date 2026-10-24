using System;
using Expedition.Core;

namespace Expedition.Presentation
{
    // Palace sprites use the regional 6 by 4 sheet and face left.
    public static class PalaceEnemyArt
    {
        public const string ManifestPath = "Art/Enemies/v031/palace-manifest";
        public static readonly string[] Species = {
            "palace-guard", "palace-arbalist", "palace-sentinel", "palace-boss"
        };

        public static string SpeciesFor(CatalogBattleUnit unit)
        {
            if (unit == null || unit.Team != CatalogTeam.Enemy) return null;
            if (unit.Id == "palace-boss" && unit.IsBoss &&
                unit.EnemyRole == CatalogEnemyRole.CrownDrake) return Species[3];
            if (unit.Id == "palace-sentinel" && unit.EnemyRole == CatalogEnemyRole.Supervisor)
                return Species[2];
            if (unit.Id.StartsWith("palace-arbalist-", StringComparison.Ordinal) &&
                unit.EnemyRole == CatalogEnemyRole.Raider) return Species[1];
            if (unit.Id.StartsWith("palace-guard-", StringComparison.Ordinal) &&
                unit.EnemyRole == CatalogEnemyRole.MineWorker) return Species[0];
            return null;
        }

        public static float SilhouetteHeight(string id)
        {
            return id == Species[0] ? 108f : id == Species[1] ? 110f :
                id == Species[2] ? 150f : id == Species[3] ? 300f : 0f;
        }

        public static int FrameIndex(string id, int state, float stateTime, float windupAge,
            float releaseRemaining, float releaseAge, float attackWindupRemaining,
            CrownDrakePhase phase, float flightLift, float breathWarningRemaining,
            string releaseAbilityId)
        {
            if (state == 4)
                return ForestEnemyArt.FrameIndex(4, stateTime, windupAge, releaseRemaining,
                    releaseAge, 0f, false, attackWindupRemaining, false);
            if (id == Species[2] && releaseRemaining > 0f && releaseAbilityId == "MINE_GUARD")
                return releaseAge < .09f ? 22 : 23;
            if (id == Species[3])
            {
                if (flightLift > 0f || phase == CrownDrakePhase.Rising ||
                    phase == CrownDrakePhase.AirWarning || phase == CrownDrakePhase.LandingWarning)
                    return 22 + (Math.Max(0, (int)(stateTime * 8f)) % 2);
                if (breathWarningRemaining > 0f) return breathWarningRemaining < .42f ? 13 : 12;
            }
            return ForestEnemyArt.FrameIndex(id == Species[3] && state == 3 ? 0 : state,
                stateTime, windupAge, releaseRemaining, releaseAge, 0f, false,
                attackWindupRemaining, false);
        }
    }
}
