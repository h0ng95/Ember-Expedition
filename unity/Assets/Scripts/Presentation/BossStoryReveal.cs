using UnityEngine;

namespace Expedition.Presentation
{
    public static class BossStoryReveal
    {
        public static Vector2 CoreGround(Vector2 boss)
        { return new Vector2(Mathf.Clamp(boss.x + 210, 820, 1118), Mathf.Clamp(boss.y + 30, 430, 550)); }
        public static Vector2 ClueGround(Vector2 core) { return core + new Vector2(90, 24); }
        public static int ClueIndex(int wave)
        { return wave == 10 ? 1 : wave == 15 ? 3 : wave == 20 ? 4 : wave == 25 ? 7 : wave == 30 ? 6 : -1; }
        public static float ClueWidth(int wave) { return wave == 20 ? 68 : wave == 30 ? 54 : 58; }
        public static float Alpha(bool active, float ignisAge, bool quiet)
        { return !active ? 0 : quiet ? 1 : Mathf.Clamp01((ignisAge - .14f) / .6f); }
    }
}
