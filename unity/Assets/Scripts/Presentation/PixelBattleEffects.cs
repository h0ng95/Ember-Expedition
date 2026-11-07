using UnityEngine;

namespace Expedition.Presentation
{
    // Offline PixelFX exports: cached atlases only, no runtime particles or texture allocations.
    internal sealed class PixelBattleEffects
    {
        internal const float SlashDuration = .24f, HealDuration = .64f;
        const string Root = "Art/Effects/v070/";
        Texture2D slash, heal, frost;
        bool loaded;

        void Load()
        {
            if (loaded) return;
            loaded = true;
            slash = Resources.Load<Texture2D>(Root + "sword-hit");
            heal = Resources.Load<Texture2D>(Root + "healing");
            frost = Resources.Load<Texture2D>(Root + "frozen");
        }

        static bool Frame(Texture2D sheet, int rows, int count, Vector2 pivot,
            Vector2 point, float width, float progress, bool flip = false, float opacity = 1f)
        {
            if (sheet == null) return false;
            int index = Mathf.Clamp(Mathf.FloorToInt(progress * count), 0, count - 1);
            float cellWidth = sheet.width / 4f, cellHeight = sheet.height / (float)rows;
            float scale = width / cellWidth;
            float left = point.x - (flip ? cellWidth - pivot.x : pivot.x) * scale;
            var rect = new Rect(Mathf.Round(left), Mathf.Round(point.y - pivot.y * scale), width, cellHeight * scale);
            var uv = new Rect((index % 4) * .25f, 1f - (index / 4 + 1f) / rows, .25f, 1f / rows);
            if (flip) { uv.x += uv.width; uv.width = -uv.width; }
            Color previous = GUI.color;
            float dim = GamePreferences.Current.ReduceFlashes ? .65f : 1f;
            GUI.color = previous * new Color(dim, dim, dim, opacity);
            GUI.DrawTextureWithTexCoords(rect, sheet, uv, true);
            GUI.color = previous;
            return true;
        }

        internal bool Slash(Vector2 hit, float size, float progress, int direction)
        {
            Load();
            return Frame(slash, 2, 8, new Vector2(72,64), hit,
                size < 35 ? 144f : 174f, progress, direction < 0);
        }

        internal bool Heal(Vector2 foot, float progress)
        {
            Load();
            return Frame(heal, 4, 16, new Vector2(40,92), foot, 96f, progress,
                false, 1f - Mathf.Clamp01((progress - .8f) / .2f));
        }

        internal bool Frozen(Vector2 foot, float battleTime, float remaining)
        {
            Load();
            // The status is held, not repeatedly broken/re-formed. No motion after pause or thaw.
            float progress = GamePreferences.Current.ReduceMotion ? .25f : Mathf.Repeat(battleTime, 1f);
            return Frame(frost, 4, 16, new Vector2(32,56), foot, 96f, progress,
                false, Mathf.Clamp01(remaining / .12f));
        }
    }
}
