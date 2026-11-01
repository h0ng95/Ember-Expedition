using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Presentation only: damage, warning areas and combat cooldowns stay in CatalogBattle.
    public static class BossActionPresentation
    {
        public static float ReleaseDuration(string ability)
        {
            switch (ability)
            {
                case "FROST_NOVA": return .36f;
                case "GARDEN_BRIARS": return .32f;
                case "FURNACE_BLAST": case "FURNACE_EMBER": return .36f;
                case "GOLEM_SLAM": case "GOLEM_AFTERSHOCK": return .28f;
                default: return .18f;
            }
        }

        public static int ReleaseVariant(CatalogBattleUnit unit)
        {
            if (unit.EnemyRole == CatalogEnemyRole.FrostWarden) return unit.FrostStrikeOrdinal;
            return unit.EnemyRole == CatalogEnemyRole.GardenLord && unit.GardenStrikeOrdinal == 2 ? 1 : 0;
        }

        // Preserve the impact even when the same simulation step begins the next warning.
        public static int Frame(CatalogBattleUnit unit, int fallback, float releaseRemaining,
            float releaseAge, string releaseAbility, int releaseVariant)
        {
            if (!unit.IsAlive || !unit.IsBoss) return fallback;
            bool releasing = releaseRemaining > 0;
            if (unit.EnemyRole == CatalogEnemyRole.FrostWarden)
            {
                if (releasing && releaseAbility == "FROST_NOVA")
                    return releaseAge < .22f ? (releaseVariant == 0 ? 25 : 27) : 16;
                if (unit.SlamWarningRemaining > 0) return unit.IsFrostFollowup ? 26 : 24;
            }
            if (unit.EnemyRole == CatalogEnemyRole.GardenLord)
            {
                if (releasing && releaseAbility == "GARDEN_BRIARS")
                    return releaseAge < .22f ? (releaseVariant == 0 ? 25 : 27) : 16;
                if (unit.SlamWarningRemaining > 0) return unit.GardenStrikeOrdinal == 2 ? 26 : 24;
            }
            if (unit.EnemyRole == CatalogEnemyRole.Furnace)
            {
                if (releasing && releaseAbility == "FURNACE_BLAST") return releaseAge < .22f ? 23 : 22;
                if (releasing && releaseAbility == "FURNACE_EMBER") return releaseAge < .18f ? 15 : 16;
                if (unit.SlamWarningRemaining > 0) return unit.SlamIsAftershock ? 13 : 22;
            }
            if (unit.EnemyRole == CatalogEnemyRole.Golem && releasing &&
                (releaseAbility == "GOLEM_SLAM" || releaseAbility == "GOLEM_AFTERSHOCK"))
                return releaseAge < .08f ? 14 : releaseAge < .18f ? 15 : 16;
            return fallback;
        }

        // Rooted, bowed and intact; the guardian remains in this pose during the story.
        public static int GardenDefeatFrame(float age, bool reduceMotion)
        { return reduceMotion || age >= .32f ? 19 : 18; }
    }
}
