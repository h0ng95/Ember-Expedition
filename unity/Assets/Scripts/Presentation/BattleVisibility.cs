using System;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public static class BattleVisibility
    {
        // Body proxies exclude banners, staves and cast effects. Count the union of
        // foreground bodies, rather than summing overlap twice at a shared edge.
        public static Rect AllyBody(Vector2 foot, float height)
        { return new Rect(foot.x - 31f, foot.y - height, 62f, height * .82f); }

        public static float CoveredFraction(Rect body, System.Collections.Generic.IList<Rect> foreground)
        {
            if (body.width <= 0 || body.height <= 0) return 0f;
            int covered = 0;
            for (int y = 0; y < 6; y++) for (int x = 0; x < 5; x++)
            {
                Vector2 p = new Vector2(body.x + body.width * (x + .5f) / 5f,
                    body.y + body.height * (y + .5f) / 6f);
                foreach (Rect r in foreground) if (r.Contains(p)) { covered++; break; }
            }
            return covered / 30f;
        }

        public static Color AllyColor(int index)
        {
            switch (index)
            {
                case 1: return new Color(.42f,.83f,1f);
                case 2: return new Color(.83f,.68f,1f);
                case 3: return new Color(.58f,.91f,.63f);
                case 4: return new Color(1f,.66f,.49f);
                default: return new Color(1f,.83f,.45f);
            }
        }
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
