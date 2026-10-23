using System;
using Expedition.Core;

namespace Expedition.Presentation
{
    public static class BattleVisibility
    {
        // Corpses and then bosses recede. Living escorts and allies must stay
        // readable in front of a large boss, irrespective of their foot depth.
        public static int Compare(bool aliveA, float footYA, CatalogTeam teamA, string idA,
            bool aliveB, float footYB, CatalogTeam teamB, string idB,
            bool bossA = false, bool bossB = false)
        {
            if (aliveA != aliveB) return aliveA ? 1 : -1;
            if (bossA != bossB) return bossA ? -1 : 1;
            int depth = footYB.CompareTo(footYA);
            if (depth != 0) return depth;
            if (teamA != teamB) return teamA == CatalogTeam.Enemy ? -1 : 1;
            return string.Compare(idA, idB, StringComparison.Ordinal);
        }

    }
}
