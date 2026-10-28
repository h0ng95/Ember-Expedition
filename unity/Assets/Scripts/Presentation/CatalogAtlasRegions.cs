using System;
using UnityEngine;

namespace Expedition.Presentation
{
    [Serializable]
    internal sealed class CatalogArtRegion
    {
        // Source-pixel coordinates relative to the frame's top-left corner.
        public float x, y, width, height;
    }

    internal static class CatalogAtlasRegions
    {
        // Some sheets have interlocking pose bounds. Draw only the source regions
        // belonging to this pose, preserving its original light/arrow pixels.
        internal static void Draw(Rect destination, Texture texture, Rect uv,
            float frameWidth, float frameHeight, CatalogArtRegion[] regions)
        {
            if (regions == null || regions.Length == 0)
            {
                GUI.DrawTextureWithTexCoords(destination, texture, uv, true);
                return;
            }
            foreach (CatalogArtRegion region in regions)
            {
                float w = region.width / frameWidth, h = region.height / frameHeight;
                float x = region.x / frameWidth, y = region.y / frameHeight;
                if (uv.width < 0f) x = 1f - x - w;
                Rect target = new Rect(destination.x + x * destination.width,
                    destination.y + y * destination.height,
                    w * destination.width, h * destination.height);
                // Shared boundaries must land on the same physical pixel. Otherwise
                // fractional GUI scaling can leave bright seams between UV regions.
                Vector2 start = GUIUtility.GUIToScreenPoint(target.min);
                Vector2 end = GUIUtility.GUIToScreenPoint(target.max);
                start = GUIUtility.ScreenToGUIPoint(new Vector2(Mathf.Round(start.x), Mathf.Round(start.y)));
                end = GUIUtility.ScreenToGUIPoint(new Vector2(Mathf.Round(end.x), Mathf.Round(end.y)));
                target = Rect.MinMaxRect(start.x, start.y, end.x, end.y);
                Rect source = new Rect(uv.x + x * uv.width,
                    uv.y + (1f - y - h) * uv.height, w * uv.width, h * uv.height);
                GUI.DrawTextureWithTexCoords(target, texture, source, true);
            }
        }
    }
}
