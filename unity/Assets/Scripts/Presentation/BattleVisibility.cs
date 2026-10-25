using System;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public static class BattleVisibility
    {
        // Corpses recede. Living enemies, including bosses, use foot depth;
        // allies remain in front of bosses.
        public static int Compare(bool aliveA, float footYA, CatalogTeam teamA, string idA,
            bool aliveB, float footYB, CatalogTeam teamB, string idB)
        {
            if (aliveA != aliveB) return aliveA ? 1 : -1;
            if (aliveA && teamA != teamB) return teamA == CatalogTeam.Enemy ? -1 : 1;
            int depth = footYB.CompareTo(footYA);
            if (depth != 0) return depth;
            if (teamA != teamB) return teamA == CatalogTeam.Enemy ? -1 : 1;
            return string.Compare(idA, idB, StringComparison.Ordinal);
        }

        public static bool NeedsEscortReveal(Rect escort, Rect boss)
        {
            if (escort.width <= 0f || escort.height <= 0f || boss.width <= 0f || boss.height <= 0f)
                return false;
            float coveredWidth = Mathf.Max(0f, Mathf.Min(escort.xMax, boss.xMax) -
                Mathf.Max(escort.xMin, boss.xMin)) / escort.width;
            float coveredHeight = Mathf.Max(0f, Mathf.Min(escort.yMax, boss.yMax) -
                Mathf.Max(escort.yMin, boss.yMin)) / escort.height;
            return coveredWidth >= .85f && coveredHeight >= .85f &&
                coveredWidth * coveredHeight >= .85f;
        }

    }
}
