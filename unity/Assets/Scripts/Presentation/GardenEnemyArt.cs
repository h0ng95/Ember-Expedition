using System;
using Expedition.Core;

namespace Expedition.Presentation
{
    // Garden sheets share the regional 6 by 4 frame contract and face left.
    public static class GardenEnemyArt
    {
        public const string ManifestPath = "Art/Enemies/v030/garden-manifest";
        public static readonly string[] Species = {
            "garden-guard", "garden-spitter", "garden-keeper", "garden-boss"
        };

        public static string SpeciesFor(CatalogBattleUnit unit)
        {
            if (unit == null || unit.Team != CatalogTeam.Enemy) return null;
            if (unit.Id == "garden-boss" && unit.IsBoss &&
                unit.EnemyRole == CatalogEnemyRole.GardenLord) return Species[3];
            if (unit.Id == "garden-keeper" && unit.EnemyRole == CatalogEnemyRole.Supervisor)
                return Species[2];
            if (unit.Id.StartsWith("garden-spitter-", StringComparison.Ordinal) &&
                unit.EnemyRole == CatalogEnemyRole.GardenSpitter) return Species[1];
            if (unit.Id.StartsWith("garden-guard-", StringComparison.Ordinal) &&
                unit.EnemyRole == CatalogEnemyRole.MineWorker) return Species[0];
            return null;
        }

        public static float SilhouetteHeight(string id)
        {
            return id == Species[0] ? 104f : id == Species[1] ? 112f :
                id == Species[2] ? 145f : id == Species[3] ? 250f : 0f;
        }

        public static int FrameIndex(string id, int state, float stateTime, float windupAge,
            float releaseRemaining, float releaseAge, float attackWindupRemaining,
            float slamWarningRemaining, string releaseAbilityId)
        {
            if (state == 4)
                return ForestEnemyArt.FrameIndex(4, stateTime, windupAge, releaseRemaining,
                    releaseAge, 0f, false, attackWindupRemaining, false);
            if (id == Species[2] && releaseRemaining > 0f && releaseAbilityId == "MINE_GUARD")
                return releaseAge < .09f ? 22 : 23;
            if (id == Species[3] && slamWarningRemaining > 0f)
                return slamWarningRemaining <= .6f ? 23 : 22;
            return ForestEnemyArt.FrameIndex(id == Species[3] && state == 3 ? 0 : state,
                stateTime, windupAge, releaseRemaining, releaseAge, 0f, false,
                attackWindupRemaining, false);
        }
    }
}
