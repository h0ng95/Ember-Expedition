using UnityEngine;

namespace Expedition.Presentation
{
    // Original rune-board artwork, generated once and shared by the two progression views.
    // Nodes remain normal IMGUI controls; decoration never adds hidden purchases or hit targets.
    public static class ExpeditionResearchBoard
    {
        static Texture2D ring, disc, glow;
        static readonly Texture2D[] glyphs = new Texture2D[6];
        static readonly string[][] patterns = {
            new[] { "..#.....", "..#####.", "..####..", "..#####.", "..#.....", "..#.....", "..#.....", ".###...." },
            new[] { ".######.", ".#....#.", ".#..#.#.", ".#.##.#.", ".#.##.#.", "..####..", "...##...", "...##..." },
            new[] { "....#...", "...##...", "...###..", ".#.###..", ".#####..", ".##.###.", "..#..#..", "...##..." },
            new[] { ".##..##.", "#######.", "#..#..#.", "##.#.##.", "#..#..#.", "##.#.##.", "#######.", "...#...." },
            new[] { "...#....", ".#.#.#..", "..###...", "#######.", "..###...", ".#.#.#..", "...#....", "........" },
            new[] { "...#....", "..###...", ".#.#.#..", "#######.", ".#.#.#..", "..###...", "...#....", "........" }
        };

        public static Color BranchColor(int branch)
        {
            return branch == 0 ? new Color(.92f, .73f, .42f) : branch == 1 ?
                new Color(1f, .46f, .31f) : new Color(.43f, .76f, 1f);
        }

        public static readonly Vector2 Core = new Vector2(229, 337);
        public static Vector2 Position(int branch, int tier)
        {
            return tier == 1 ? new Vector2(branch == 1 ? 475 : 428, 219 + branch * 118) :
                new Vector2(branch == 1 ? 734 : 681, 201 + branch * 136);
        }

        public static void Background(Rect rect)
        {
            EnsureTextures();
            ExpeditionUiTheme.Fill(rect, new Color(.035f, .044f, .065f));
            Draw(glow, new Rect(rect.x - 40, rect.y - 30, rect.width + 80, rect.height + 60),
                new Color(.22f, .31f, .48f, .18f));
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 15; x++)
                {
                    float px = rect.x + 24 + x * 49, py = rect.y + 23 + y * 57;
                    ExpeditionUiTheme.Fill(new Rect(px, py, 2, 2), new Color(.65f, .68f, .75f, .12f));
                }
            Draw(ring, new Rect(Core.x - 153, Core.y - 153, 306, 306), new Color(.54f, .43f, .28f, .13f));
            Draw(ring, new Rect(Core.x - 116, Core.y - 116, 232, 232), new Color(.54f, .43f, .28f, .15f));
            ExpeditionUiTheme.Rule(new Rect(rect.x, rect.yMax, rect.width, 1));
        }

        public static void Center(int count, int capacity, string name, GUIStyle centered, GUIStyle caption)
        {
            EnsureTextures();
            Draw(glow, new Rect(Core.x - 90, Core.y - 90, 180, 180), new Color(1f, .58f, .22f, .35f));
            Draw(disc, new Rect(Core.x - 43, Core.y - 43, 86, 86), new Color(.11f, .10f, .12f));
            Draw(ring, new Rect(Core.x - 49, Core.y - 49, 98, 98), ExpeditionUiTheme.Gold);
            Draw(ring, new Rect(Core.x - 55, Core.y - 55, 110, 110), new Color(.78f, .6f, .33f, .5f));
            ExpeditionUiTheme.DrawEmber(Core, 37);
            GUI.Label(new Rect(Core.x - 80, Core.y + 57, 160, 27), name, centered);
            GUI.Label(new Rect(Core.x - 80, Core.y + 84, 160, 25), count + " / " + capacity + " 활성", caption);
        }

        public static void Path(Vector2 from, Vector2 to, Color color, bool learned, bool focused)
        {
            // Small joint crystals show direction; they are decoration, not extra research nodes.
            Vector2 joint = Vector2.Lerp(from, to, .52f);
            Color edge = learned ? color : new Color(.31f, .32f, .37f);
            if (learned || focused)
                Line(from, to, new Color(color.r, color.g, color.b, learned ? .13f : .06f), 9);
            Line(from, to, edge, learned ? 2.5f : 1.3f);
            Draw(disc, new Rect(joint.x - 3, joint.y - 3, 6, 6), learned ? color : new Color(.35f, .36f, .41f));
        }

        public static bool Node(Vector2 center, int glyph, int tier, Color color, string title,
            string cost, bool owned, bool available, bool selected, bool recent,
            GUIStyle label, GUIStyle caption)
        {
            EnsureTextures();
            float radius = tier == 1 ? 27 : 32;
            float hitRadius = radius + 3;
            bool clicked = GUI.Button(new Rect(center.x - 48, center.y - hitRadius, 96, hitRadius * 2),
                GUIContent.none, GUIStyle.none);
            // The title and cost are part of the same visible target on a touch screen.
            clicked |= GUI.Button(new Rect(center.x - 95, center.y + 39, 190, 49), GUIContent.none, GUIStyle.none);
            Color state = owned ? color : available ? Color.Lerp(color, Color.white, .25f) : new Color(.38f, .4f, .46f);
            if (owned || selected || available)
            {
                float alpha = recent ? .62f : owned ? .27f : selected ? .23f : .12f;
                Draw(glow, new Rect(center.x - 62, center.y - 62, 124, 124), new Color(color.r, color.g, color.b, alpha));
            }
            Draw(disc, new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2),
                owned ? Color.Lerp(new Color(.08f, .09f, .13f), color, .22f) : new Color(.075f, .085f, .12f));
            Draw(ring, new Rect(center.x - radius - 2, center.y - radius - 2, radius * 2 + 4, radius * 2 + 4), state);
            if (selected)
            {
                float r = radius + 9;
                Draw(ring, new Rect(center.x - r, center.y - r, r * 2, r * 2), ExpeditionUiTheme.Gold);
                ExpeditionUiTheme.Fill(new Rect(center.x - 3, center.y - r - 5, 6, 6), ExpeditionUiTheme.Gold);
            }
            Draw(glyphs[glyph], new Rect(center.x - 15, center.y - 15, 30, 30), owned || available ? color : state);
            if (owned)
            {
                Vector2 check = center + new Vector2(radius - 3, radius - 6);
                Draw(disc, new Rect(check.x - 9, check.y - 9, 18, 18), new Color(.1f, .16f, .17f));
                Line(check + new Vector2(-4, 0), check + new Vector2(-1, 3), ExpeditionUiTheme.Cream, 2);
                Line(check + new Vector2(-1, 3), check + new Vector2(5, -4), ExpeditionUiTheme.Cream, 2);
            }
            GUI.Label(new Rect(center.x - 95, center.y + 39, 190, 25), title, label);
            GUI.Label(new Rect(center.x - 95, center.y + 65, 190, 23),
                owned ? "활성" : available ? cost + " · 해금 가능" : cost + " · 잠김", caption);
            return clicked;
        }

        public static void Glyph(Rect rect, int index, Color color)
        { EnsureTextures(); Draw(glyphs[index], rect, color); }

        static void Line(Vector2 a, Vector2 b, Color color, float width)
        {
            Matrix4x4 saved = GUI.matrix;
            // Compose in logical board space; RotateAroundPivot offsets the pivot under a safe-area scale.
            GUI.matrix = saved * Matrix4x4.TRS(new Vector3(a.x, a.y, 0),
                Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg), Vector3.one);
            ExpeditionUiTheme.Fill(new Rect(0, -width * .5f, Vector2.Distance(a, b), width), color);
            GUI.matrix = saved;
        }

        static void Draw(Texture2D texture, Rect rect, Color color)
        {
            Color saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture);
            GUI.color = saved;
        }

        static void EnsureTextures()
        {
            if (ring != null) return;
            ring = Radial(0); disc = Radial(1); glow = Radial(2);
            for (int i = 0; i < glyphs.Length; i++)
            {
                Texture2D tex = Texture(8, "Research rune " + i);
                var pixels = new Color[64];
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++) pixels[(7 - y) * 8 + x] = patterns[i][y][x] == '#' ? Color.white : Color.clear;
                tex.SetPixels(pixels); tex.Apply(false, true); glyphs[i] = tex;
            }
        }

        static Texture2D Radial(int kind)
        {
            const int size = 96;
            Texture2D tex = Texture(size, "Research seal " + kind);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2(x - 47.5f, y - 47.5f).magnitude / 47f;
                    float alpha = kind == 0 ? Mathf.Clamp01(1f - Mathf.Abs(r - .94f) * 30f) :
                        kind == 1 ? Mathf.Clamp01((1f - r) * 30f) : Mathf.Pow(Mathf.Max(0, 1f - r), 2f);
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
            tex.SetPixels(pixels); tex.Apply(false, true); return tex;
        }

        static Texture2D Texture(int size, string name)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false) {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        public static void Release()
        {
            if (ring != null) Object.Destroy(ring);
            if (disc != null) Object.Destroy(disc);
            if (glow != null) Object.Destroy(glow);
            ring = disc = glow = null;
            for (int i = 0; i < glyphs.Length; i++)
            { if (glyphs[i] != null) Object.Destroy(glyphs[i]); glyphs[i] = null; }
        }
    }
}
