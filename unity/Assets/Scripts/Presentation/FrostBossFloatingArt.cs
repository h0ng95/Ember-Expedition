using System;
using UnityEngine;

namespace Expedition.Presentation
{
    // Finished full-character drawings. Arms are part of each frame, never separate layers.
    public sealed class FrostBossFloatingArt
    {
        public const string TexturePath = "Art/Enemies/v064/frost-warden";
        const float SheetWidth = 1087f, SheetHeight = 1447f;
        static readonly Rect[] Frames = {
            new Rect(45, 35, 506, 396), new Rect(600, 49, 471, 382),
            new Rect(12, 442, 563, 371), new Rect(580, 442, 495, 377),
            new Rect(43, 818, 510, 356), new Rect(583, 879, 486, 276),
            new Rect(32, 1186, 493, 242), new Rect(555, 1224, 512, 205)
        };
        static readonly float[] Anchors = { 287f, 800f, 318f, 805f, 292f, 824f, 277f, 812f };
        Texture2D texture;
        RenderTexture clipped;
        bool attempted, clipAttempted;

        public static int SpriteIndex(int combatFrame)
        {
            if (combatFrame >= 18 && combatFrame <= 21) return combatFrame - 14;
            if (combatFrame == 12 || combatFrame == 13 || combatFrame == 22 || combatFrame == 23) return 1;
            if (combatFrame == 14 || combatFrame == 15) return 2;
            if (combatFrame == 16 || combatFrame == 17) return 3;
            return 0;
        }

        public static int DefeatFrame(float age, bool reduceMotion)
        { return reduceMotion ? 21 : 18 + Mathf.Clamp((int)(Mathf.Max(0, age) / .24f), 0, 3); }

        public static Rect BoundsFor(int frame, Vector2 foot, float time)
        {
            int index = SpriteIndex(frame);
            Rect source = Frames[index];
            // One scale for all poses; leaning forward changes height naturally.
            float scale = FrostEnemyArt.SilhouetteHeight(FrostEnemyArt.Species[3]) / Frames[0].height;
            bool moving = frame >= 6 && frame <= 11;
            bool defeated = index >= 4;
            float bob = defeated || GamePreferences.Current.ReduceMotion ? 0 :
                Mathf.Sin(time * Mathf.PI * 1.2f) * (moving ? 2f : 1.2f);
            float hover = !defeated ? 10f : index == 4 ? 6f : index == 5 ? 3f : 0f;
            return new Rect(foot.x + (source.x - Anchors[index]) * scale,
                foot.y - source.height * scale - hover + bob, source.width * scale, source.height * scale);
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
                // The caller owns corpse fading. Story scenes keep the settled pose opaque.
                Rect source = Frames[SpriteIndex(frame)];
                Rect uv = new Rect(source.x / SheetWidth, 1f - source.yMax / SheetHeight,
                    source.width / SheetWidth, source.height / SheetHeight);
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
