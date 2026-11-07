using UnityEngine;

namespace Expedition.Presentation
{
    // Small reusable IMGUI skin. Artwork stays untouched; these are UI surfaces.
    public static class ExpeditionUiTheme
    {
        public static readonly Color Background = Hex(0x0D1015);
        public static readonly Color Panel = Hex(0x14252B);
        public static readonly Color PanelLight = Hex(0x223A40);
        public static readonly Color Teal = Hex(0x79B5AE);
        public static readonly Color Cream = Hex(0xF4E9D4);
        public static readonly Color Gold = Hex(0xF2C16B);
        public static readonly Color Muted = Hex(0xB9B1A4);
        public static readonly Color Ink = Hex(0x35281F);
        public static readonly Color FadedInk = Hex(0x755537);
        static readonly Color Bronze = Hex(0x967D52);
        static readonly Color Edge = Hex(0x111319);
        static readonly Texture2D[] buttons = new Texture2D[6];
        public const float WindowPadding = 32f;
        public const float ActionGap = 16f;
        public const float ActionHeight = 64f;

        static Color Hex(uint value)
        {
            return new Color(((value >> 16) & 255) / 255f,
                ((value >> 8) & 255) / 255f, (value & 255) / 255f, 1f);
        }

        public static void Fill(Rect rect, Color color)
        {
            Color before = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = before;
        }

        public static void Rule(Rect rect)
        {
            Fill(rect, new Color(Bronze.r, Bronze.g, Bronze.b, .58f));
        }

        public static Rect ContentRect(Rect window)
        {
            return new Rect(window.x + WindowPadding, window.y + WindowPadding,
                window.width - 2 * WindowPadding, window.height - 2 * WindowPadding);
        }

        public static Rect FooterRect(Rect window)
        {
            return new Rect(window.x + WindowPadding, window.yMax - WindowPadding - ActionHeight,
                window.width - 2 * WindowPadding, ActionHeight);
        }

        public static Rect FooterButton(Rect footer, int index, int count)
        {
            float width = (footer.width - ActionGap * (count - 1)) / count;
            return new Rect(footer.x + index * (width + ActionGap), footer.y, width, footer.height);
        }

        public static void FooterRule(Rect window)
        {
            Rect footer = FooterRect(window);
            Rule(new Rect(footer.x, footer.y - ActionGap, footer.width, 2f));
        }

        public static void PanelBox(Rect rect, bool raised = false)
        {
            Fill(new Rect(rect.x + 2, rect.y + 4, rect.width, rect.height), new Color(0, 0, 0, .32f));
            Fill(rect, Edge);
            Fill(Inset(rect, 1), raised ? Bronze : Hex(0x3B5357));
            Fill(Inset(rect, 3), raised ? PanelLight : Panel);
            Fill(new Rect(rect.x + 5, rect.y + 4, rect.width - 10, 1), new Color(.6f, .85f, .8f, .14f));
            Fill(new Rect(rect.x + 4, rect.yMax - 5, rect.width - 8, 1), new Color(0, 0, 0, .45f));
            if (raised)
            {
                Corner(rect.x + 3, rect.y + 3, 1, 1, true);
                Corner(rect.xMax - 3, rect.y + 3, -1, 1, true);
            }
        }

        public static void Window(Rect rect, string title = null)
        {
            PanelBox(rect, true);
            // A quiet teal field and stepped brass fittings share the knight's palette.
            Fill(Inset(rect, 5), Panel);
            Fill(new Rect(rect.x + 6, rect.y + 6, rect.width - 12, 3), Hex(0x526B68));
            Fill(new Rect(rect.x + 6, rect.yMax - 9, rect.width - 12, 3), Hex(0x17181E));
            MetalCorner(rect.x, rect.y, 1, 1);
            MetalCorner(rect.xMax, rect.y, -1, 1);
            MetalCorner(rect.x, rect.yMax, 1, -1);
            MetalCorner(rect.xMax, rect.yMax, -1, -1);
            Fill(new Rect(rect.center.x - 44, rect.y + 6, 88, 3), Bronze);
            if (!string.IsNullOrEmpty(title))
            {
                var style = new GUIStyle(GUI.skin.label) {
                    fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft
                };
                style.normal.textColor = Cream;
                GUI.Label(new Rect(rect.x + 26, rect.y + 15, rect.width - 52, 40), title, style);
            }
        }

        static void MetalCorner(float x, float y, int dx, int dy)
        {
            float left = dx > 0 ? x : x - 22, top = dy > 0 ? y : y - 22;
            Fill(new Rect(left, dy > 0 ? top : top + 18, 22, 4), Bronze);
            Fill(new Rect(dx > 0 ? left : left + 18, top, 4, 22), Bronze);
            Fill(new Rect(dx > 0 ? left + 4 : left + 10, dy > 0 ? top + 4 : top + 14, 8, 4), Gold);
        }

        public static void ParchmentBox(Rect rect)
        {
            Fill(new Rect(rect.x + 3, rect.y + 5, rect.width, rect.height), new Color(0, 0, 0, .4f));
            Fill(rect, Hex(0x59412C));
            Fill(Inset(rect, 3), Hex(0xA98A5D));
            Fill(Inset(rect, 7), Hex(0xD6BD8C));
            Fill(Inset(rect, 11), Hex(0xE1CCA1));
            Fill(new Rect(rect.x + 12, rect.y + 12, 7, rect.height - 24), Hex(0xC5A773));
            // Light fibers, drawn once per strip; no per-frame texture allocation.
            for (int i = 0; i < 22; i++)
            {
                float y = rect.y + 18 + i * (rect.height - 36) / 22;
                Fill(new Rect(rect.x + 22, y, rect.width - 43, 1), new Color(.38f, .25f, .12f, .035f));
            }
            Fill(new Rect(rect.xMax - 45, rect.y + 6, 20, 53), Hex(0x75392E));
            Fill(new Rect(rect.xMax - 40, rect.y + 6, 2, 45), Hex(0xB7774C));
        }

        public static void DrawHeaderPlate(Rect rect)
        {
            Fill(rect, new Color(.045f, .055f, .075f, .97f));
            Fill(new Rect(rect.x, rect.yMax - 3, rect.width, 2), Hex(0x68573E));
            Fill(new Rect(rect.x + 16, rect.yMax - 1, rect.width - 32, 1), new Color(1, .8f, .45f, .22f));
        }

        public static void DrawBackdrop(Rect rect, Texture2D texture, bool dim = true)
        {
            Fill(rect, Background);
            if (texture != null)
            {
                Color before = GUI.color;
                GUI.color = dim ? new Color(.53f, .51f, .49f, 1f) : new Color(.84f, .84f, .84f, 1f);
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleAndCrop);
                GUI.color = before;
            }
            Fill(rect, new Color(.025f, .035f, .055f, dim ? .25f : .08f));
            // Edge shade keeps the existing art visible behind the home frame.
            for (int i = 0; i < 5; i++)
            {
                float height = rect.height * .035f;
                float alpha = .22f - i * .035f;
                Fill(new Rect(rect.x, rect.y + i * height, rect.width, height), new Color(0, 0, 0, alpha));
                Fill(new Rect(rect.x, rect.yMax - (i + 1) * height, rect.width, height), new Color(0, 0, 0, alpha));
            }
        }

        public static void DrawEmber(Vector2 center, float size)
        {
            float u = size / 12f;
            Fill(new Rect(center.x - 5 * u, center.y + 3 * u, 10 * u, 2 * u), Edge);
            Fill(new Rect(center.x - 4 * u, center.y - u, 8 * u, 5 * u), Hex(0xB96F35));
            Fill(new Rect(center.x - 2 * u, center.y - 5 * u, 5 * u, 8 * u), Hex(0xE59B43));
            Fill(new Rect(center.x, center.y - 7 * u, 2 * u, 4 * u), Gold);
            Fill(new Rect(center.x - 2 * u, center.y - u, 4 * u, 4 * u), Gold);
            Fill(new Rect(center.x - u, center.y + u, 2 * u, 2 * u), Cream);
        }

        public static GUIStyle CreateButtonStyle(Font font, int size, bool primary = false)
        {
            int start = primary ? 3 : 0;
            for (int i = start; i < start + 3; i++)
                if (buttons[i] == null) buttons[i] = CreateButton(i >= 3, i % 3);
            var style = new GUIStyle(GUI.skin.button) {
                font = font, fontSize = size, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, wordWrap = true,
                border = new RectOffset(7, 7, 7, 7),
                padding = new RectOffset(12, 12, 7, 7),
                margin = new RectOffset(0, 0, 0, 0),
                overflow = new RectOffset(0, 0, 0, 0)
            };
            style.normal.background = style.onNormal.background = buttons[start];
            style.hover.background = style.onHover.background = buttons[start + 1];
            style.active.background = style.onActive.background = buttons[start + 2];
            style.focused.background = style.onFocused.background = buttons[start + 1];
            Color text = primary ? Hex(0x30251B) : Cream;
            style.normal.textColor = style.onNormal.textColor = text;
            style.hover.textColor = style.onHover.textColor = primary ? Hex(0x21170D) : Gold;
            style.active.textColor = style.onActive.textColor = text;
            style.focused.textColor = style.onFocused.textColor = text;
            return style;
        }

        static Texture2D CreateButton(bool primary, int state)
        {
            const int size = 24;
            var texture = NewTexture(size, "Expedition button");
            var pixels = new Color[size * size];
            Color baseColor = primary ? Hex(state == 1 ? 0xEAC782u : state == 2 ? 0xA77C42u : 0xD4AC68u) :
                Hex(state == 1 ? 0x304E53u : state == 2 ? 0x102329u : 0x1F363Du);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int edge = Mathf.Min(x, size - 1 - x, y, size - 1 - y);
                    Color color = baseColor;
                    if (edge == 0) color = Hex(0x101217);
                    else if (edge == 1) color = primary ? Hex(0x8E6B37) : Hex(0x52716F);
                    else if (edge == 2) color = Color.Lerp(baseColor, Edge, .35f);
                    else if (y == size - 4) color = Color.Lerp(baseColor, Cream, state == 2 ? .08f : .32f);
                    else if (y < 5) color = Color.Lerp(baseColor, Edge, .27f);
                    int corner = Mathf.Min(x, size - 1 - x) + Mathf.Min(y, size - 1 - y);
                    if (corner < 4)
                        color = Color.clear;
                    pixels[y * size + x] = color;
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Texture2D NewTexture(int size, string name)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false) {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        static Rect Inset(Rect rect, float amount)
        {
            return new Rect(rect.x + amount, rect.y + amount,
                Mathf.Max(0, rect.width - amount * 2), Mathf.Max(0, rect.height - amount * 2));
        }

        static void Corner(float x, float y, int dx, int dy, bool raised)
        {
            float left = dx > 0 ? x : x - 12, top = dy > 0 ? y : y - 2;
            Fill(new Rect(left, top, 12, 2), raised ? Gold : Bronze);
            left = dx > 0 ? x : x - 2;
            top = dy > 0 ? y : y - 12;
            Fill(new Rect(left, top, 2, 12), raised ? Gold : Bronze);
        }

        public static void Release()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null) Object.Destroy(buttons[i]);
                buttons[i] = null;
            }
        }
    }
}
