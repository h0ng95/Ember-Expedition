using System;
using UnityEngine;

namespace Expedition.Presentation
{
    // Small world props, deliberately subordinate to actors. All crops retain generated alpha.
    public sealed class StoryPixelArt : IDisposable
    {
        public const string ResourcePath = "Art/Story/v062/story-props";
        Texture2D atlas;
        static readonly Rect[] Crops = {
            new Rect(32, 190, 345, 286), new Rect(430, 275, 294, 216),
            new Rect(788, 250, 320, 224), new Rect(1150, 250, 368, 228),
            new Rect(18, 645, 376, 199), new Rect(444, 588, 302, 328),
            new Rect(794, 668, 308, 219), new Rect(1150, 590, 370, 280)
        };
        public StoryPixelArt() { atlas = Resources.Load<Texture2D>(ResourcePath); }
        public void Draw(int index, Vector2 foot, float width, float alpha = 1, float shade = 1)
        {
            if (atlas == null) return;
            Rect source = Crops[index];
            Color before = GUI.color;
            GUI.color *= new Color(shade, shade, shade, alpha);
            GUI.DrawTextureWithTexCoords(new Rect(Mathf.Round(foot.x - width / 2),
                Mathf.Round(foot.y - width * source.height / source.width), width, width * source.height / source.width),
                atlas, new Rect(source.x / 1536f, 1 - source.yMax / 1024f, source.width / 1536f, source.height / 1024f), true);
            GUI.color = before;
        }
        public void Dispose() { if (atlas != null) Resources.UnloadAsset(atlas); atlas = null; }
    }
}
