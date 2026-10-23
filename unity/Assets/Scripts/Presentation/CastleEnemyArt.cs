using System;
using Expedition.Core;

namespace Expedition.Presentation
{
    // The castle sheets use the same 6 by 4 frame layout and shared renderer
    // as the forest sheets. All authored poses face left.
    public static class CastleEnemyArt
    {
        public const string ManifestPath = "Art/Enemies/v021/castle-manifest";
        public static readonly string[] Species = {
            "castle-infantry", "castle-crossbow", "castle-elite", "castle-boss"
        };

        public static string SpeciesFor(CatalogBattleUnit unit)
        {
            if (unit == null || unit.Team != CatalogTeam.Enemy) return null;
            if (unit.IsBoss) return unit.Id == "golem-boss" &&
                unit.EnemyRole == CatalogEnemyRole.Golem ? Species[3] : null;
            if (unit.Id.StartsWith("stone-elite", StringComparison.Ordinal)) return Species[2];
            if (unit.Id.StartsWith("stone-infantry", StringComparison.Ordinal)) return Species[0];
            if (unit.Id.StartsWith("crossbow-raider", StringComparison.Ordinal)) return Species[1];
            return null;
        }

        public static float SilhouetteHeight(string id)
        {
            return id == Species[0] ? 90f : id == Species[1] ? 100f :
                id == Species[2] ? 140f : id == Species[3] ? 244f : 0f;
        }

        // The first slam impact must play its release pose even when an enraged
        // golem has already begun the aftershock warning in the same core tick.
        public static int FrameIndex(string id, int state, float stateTime, float windupAge,
            float releaseRemaining, float releaseAge, float chargeWarningRemaining,
            bool chargeMoving, float attackWindupRemaining, float slamWarningRemaining,
            bool slamAftershock, string releaseAbilityId)
        {
            if (state == 4)
                return ForestEnemyArt.FrameIndex(4, stateTime, windupAge, releaseRemaining,
                    releaseAge, chargeWarningRemaining, chargeMoving, attackWindupRemaining, false);
            bool boss = id == Species[3], elite = id == Species[2];
            if (boss && slamWarningRemaining > 0f)
            {
                if (slamAftershock && releaseRemaining > 0f && releaseAbilityId == "GOLEM_SLAM")
                    return 14 + Math.Min(3, Math.Max(0, (int)(releaseAge / .045f)));
                return slamAftershock || slamWarningRemaining <= .55f ? 23 : 22;
            }
            // Bosses have no received-hit pose. Their attack warning and release
            // remain readable while damage feedback comes from HP and effects.
            return ForestEnemyArt.FrameIndex(boss && state == 3 ? 0 : state,
                stateTime, windupAge, releaseRemaining, releaseAge,
                elite ? chargeWarningRemaining : 0f, elite && chargeMoving,
                attackWindupRemaining, elite);
        }
    }
}
