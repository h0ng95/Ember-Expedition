using System;
using Expedition.Core;

namespace Expedition.Presentation
{
    // Mine and furnace sheets use the forest renderer's 6 by 4 frame contract.
    // All authored poses face left; the boss never plays a received-hit pose.
    public static class MineEnemyArt
    {
        public const string ManifestPath = "Art/Enemies/v024/mine-manifest";
        public static readonly string[] Species = {
            "mine-worker", "mine-slinger", "mine-elite", "furnace-boss"
        };

        public static string SpeciesFor(CatalogBattleUnit unit)
        {
            if (unit == null || unit.Team != CatalogTeam.Enemy) return null;
            if (unit.Id == "furnace-boss" && unit.IsBoss &&
                unit.EnemyRole == CatalogEnemyRole.Furnace) return Species[3];
            if (unit.Id == "mine-elite" && unit.EnemyRole == CatalogEnemyRole.Supervisor)
                return Species[2];
            if (unit.Id.StartsWith("mine-slinger-", StringComparison.Ordinal) &&
                unit.EnemyRole == CatalogEnemyRole.Raider) return Species[1];
            if (unit.Id.StartsWith("mine-worker-", StringComparison.Ordinal) &&
                unit.EnemyRole == CatalogEnemyRole.MineWorker) return Species[0];
            return null;
        }

        public static float SilhouetteHeight(string id)
        {
            return id == Species[0] ? 90f : id == Species[1] ? 100f :
                id == Species[2] ? 145f : id == Species[3] ? 244f : 0f;
        }

        public static int FrameIndex(string id, int state, float stateTime, float windupAge,
            float releaseRemaining, float releaseAge, float attackWindupRemaining,
            float slamWarningRemaining, bool slamAftershock, string releaseAbilityId)
        {
            if (state == 4)
                return ForestEnemyArt.FrameIndex(4, stateTime, windupAge, releaseRemaining,
                    releaseAge, 0f, false, attackWindupRemaining, false);
            if (id == Species[2] && releaseRemaining > 0f && releaseAbilityId == "MINE_GUARD")
                return releaseAge < .09f ? 22 : 23;
            if (id == Species[3] && slamWarningRemaining > 0f)
            {
                if (slamAftershock && releaseRemaining > 0f &&
                    releaseAbilityId == "FURNACE_BLAST")
                    return 14 + Math.Min(3, Math.Max(0, (int)(releaseAge / .045f)));
                return slamAftershock || slamWarningRemaining <= .55f ? 23 : 22;
            }
            return ForestEnemyArt.FrameIndex(id == Species[3] && state == 3 ? 0 : state,
                stateTime, windupAge, releaseRemaining, releaseAge, 0f, false,
                attackWindupRemaining, false);
        }
    }
}
