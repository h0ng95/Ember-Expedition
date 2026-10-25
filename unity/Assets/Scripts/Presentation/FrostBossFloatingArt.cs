using System;
using UnityEngine;

namespace Expedition.Presentation
{
    // Finished full-character drawings. Arms are part of each frame, never separate layers.
    public sealed class FrostBossFloatingArt
    {
        public const string TexturePath = "Art/Enemies/v039/frost-boss-full-motion";
        static readonly Rect[] Frames = {
            new Rect(25, 30, 656, 566), new Rect(729, 58, 574, 544),
            new Rect(16, 647, 688, 512), new Rect(713, 608, 595, 560)
        };
        static readonly float[] Anchors = { 350f, 970f, 500f, 983f };
        Texture2D texture;
        RenderTexture clipped;
        bool attempted, clipAttempted;

        public static int SpriteIndex(int combatFrame)
        {
            if (combatFrame == 12 || combatFrame == 13 || combatFrame == 22 || combatFrame == 23) return 1;
            if (combatFrame == 14 || combatFrame == 15) return 2;
            if (combatFrame == 16 || combatFrame == 17) return 3;
            return 0;
        }

        public static Rect BoundsFor(int frame, Vector2 foot, float time)
        {
            int index = SpriteIndex(frame);
            Rect source = Frames[index];
            // One scale for all poses; leaning forward changes height naturally.
            float scale = FrostEnemyArt.SilhouetteHeight(FrostEnemyArt.Species[3]) / Frames[0].height;
            bool moving = frame >= 6 && frame <= 11;
            float bob = Mathf.Sin(time * Mathf.PI * 1.2f) * (moving ? 2f : 1.2f);
            float fall = frame >= 18 && frame <= 21 ? (frame - 18) * 2f : 0f;
            return new Rect(foot.x + (source.x - Anchors[index]) * scale,
                foot.y - source.height * scale - 10f + bob + fall, source.width * scale, source.height * scale);
        }

        void Load(Material material)
        {
            if (!attempted)
            {
                attempted = true;
                texture = Resources.Load<Texture2D>(TexturePath);
                if (texture != null) texture.filterMode = FilterMode.Point;
            }
            if (texture == null || clipAttempted || material == null || !SystemInfo.supportsRenderTextures ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32)) return;
            clipAttempted = true;
            RenderTexture previous = RenderTexture.active;
            try
            {
                clipped = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                clipped.filterMode = FilterMode.Point;
                if (clipped.Create()) Graphics.Blit(texture, clipped, material);
                else { clipped.Release(); DisposeObject(clipped); clipped = null; }
            }
            catch (Exception)
            {
                if (clipped != null) { clipped.Release(); DisposeObject(clipped); clipped = null; }
            }
            finally { RenderTexture.active = previous; }
        }

        public bool TryGetBounds(int frame, Vector2 foot, float time, out Rect bounds)
        {
            Load(null);
            bounds = BoundsFor(frame, foot, time);
            return texture != null;
        }

        public bool Draw(int frame, Vector2 foot, float time, Material material, out Rect bounds)
        {
            Load(material);
            bounds = BoundsFor(frame, foot, time);
            if (texture == null) return false;
            Color saved = GUI.color;
            try
            {
                if (frame >= 18 && frame <= 21)
                    GUI.color = new Color(saved.r, saved.g, saved.b, saved.a * (21 - frame) / 3f);
                Rect source = Frames[SpriteIndex(frame)];
                Rect uv = new Rect(source.x / 1312f, 1f - source.yMax / 1199f,
                    source.width / 1312f, source.height / 1199f);
                GUI.DrawTextureWithTexCoords(bounds, clipped != null ? (Texture)clipped : texture, uv, true);
            }
            finally { GUI.color = saved; }
            return true;
        }

        public void Release()
        {
            if (clipped != null) { clipped.Release(); DisposeObject(clipped); }
            clipped = null;
            if (texture != null) Resources.UnloadAsset(texture);
            texture = null;
            attempted = clipAttempted = false;
        }
        static void DisposeObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
