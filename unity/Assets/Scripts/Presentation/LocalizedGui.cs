using System.Collections.Generic;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Fit fixed controls without changing their hit areas. Long prose uses its own scroll area.
    public static class LocalizedGui
    {
        static readonly Dictionary<GUIStyle, Dictionary<int, GUIStyle>> styles = new Dictionary<GUIStyle, Dictionary<int, GUIStyle>>();
        static readonly Dictionary<GUIStyle, GUIStyle> labels = new Dictionary<GUIStyle, GUIStyle>();
        public static bool Audit;
        public static readonly HashSet<string> Overflows = new HashSet<string>();
        public static void ClearCache() { styles.Clear(); labels.Clear(); Overflows.Clear(); }
        static GUIStyle LabelStyle(GUIStyle style)
        {
            // Label rects already describe content bounds. Unity's default skin adds hidden
            // vertical padding, which clipped short captions even in Korean on Android.
            if (style.padding.horizontal == 0 && style.padding.vertical == 0) return style;
            GUIStyle result;
            if (!labels.TryGetValue(style, out result))
            { result = new GUIStyle(style) { padding = new RectOffset(0, 0, 0, 0) }; labels.Add(style, result); }
            result.normal.textColor = style.normal.textColor;
            return result;
        }
        static GUIStyle Sized(GUIStyle style, int size)
        {
            Dictionary<int, GUIStyle> sizes;
            if (!styles.TryGetValue(style, out sizes)) { sizes = new Dictionary<int, GUIStyle>(); styles.Add(style, sizes); }
            GUIStyle result;
            if (!sizes.TryGetValue(size, out result)) { result = new GUIStyle(style) { fontSize = size }; sizes.Add(size, result); }
            // Some existing controls temporarily tint their style for a selection.
            result.normal.textColor = style.normal.textColor;
            return result;
        }
        public static bool Fits(Rect rect, string text, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return true;
            GUIContent content = new GUIContent(text);
            return style.CalcHeight(content, rect.width) <= rect.height + 1 &&
                (style.wordWrap || style.CalcSize(content).x <= rect.width + 1);
        }
        static GUIStyle Fit(Rect rect, string text, GUIStyle style)
        {
            if (style == GUIStyle.none || string.IsNullOrEmpty(text)) return style;
            GUIStyle result = style;
            int minimum = Mathf.Min(style.fontSize, 15);
            for (int size = style.fontSize - 1; !Fits(rect, text, result) && size >= minimum; size--)
                result = Sized(style, size);
            if (Audit && Event.current.type == EventType.Repaint && !Fits(rect, text, result))
                Overflows.Add(rect + " | " + text);
            return result;
        }
        public static void Label(Rect rect, string text, GUIStyle style)
        { text = CatalogText.Resolve(text); GUI.Label(rect, text, Fit(rect, text, LabelStyle(style))); }
        public static void Label(Rect rect, GUIContent content, GUIStyle style)
        { if (content.image != null) GUI.Label(rect, content, style); else Label(rect, content.text, style); }
        public static void Label(Rect rect, string text) { Label(rect, text, GUI.skin.label); }
        public static bool Button(Rect rect, string text, GUIStyle style)
        { text = CatalogText.Resolve(text); return GUI.Button(rect, text, Fit(rect, text, style)); }
        public static bool Button(Rect rect, GUIContent content, GUIStyle style)
        { if (content == GUIContent.none || content.image != null) return GUI.Button(rect, content, style); return Button(rect, content.text, style); }
        public static bool Button(Rect rect, string text) { return Button(rect, text, GUI.skin.button); }
    }
}
