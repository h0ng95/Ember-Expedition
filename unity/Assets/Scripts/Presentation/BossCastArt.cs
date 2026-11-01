using System;
using UnityEngine;

namespace Expedition.Presentation
{
    // Supplementary whole-body drawings. No limb extraction or frame-by-frame resizing.
    public sealed class BossCastArt
    {
        public const string FrostPath = "Art/Enemies/v065/frost-cast";
        public const string GardenPath = "Art/Enemies/v065/garden-cast";
        static readonly Rect[][] Sources = {
            new[] { new Rect(18,34,767,441), new Rect(791,87,640,396),
                new Rect(131,486,593,519), new Rect(766,591,752,419) },
            new[] { new Rect(197,49,487,449), new Rect(888,181,489,324),
                new Rect(198,523,478,457), new Rect(864,657,523,326) }
        };
        static readonly float[][] Anchors = { new[] {400f,1210f,410f,1240f}, new[] {440f,1230f,480f,1220f} };
        readonly Texture2D[] textures = new Texture2D[2];
        readonly RenderTexture[] clipped = new RenderTexture[2];
        readonly bool[] attempted = new bool[2], clipAttempted = new bool[2];
        public static bool Supports(string id, int frame)
        { return (id == "frost-boss" || id == "garden-boss") && frame >= 24 && frame <= 27; }
        public static Rect BoundsFor(string id, int frame, Vector2 foot)
        {
            int family = id == "frost-boss" ? 0 : 1, pose = Mathf.Clamp(frame - 24, 0, 3);
            Rect source = Sources[family][pose];
            // Crown-to-skirt/root height, excluding raised hands. One scale per character.
            float scale = family == 0 ? 290f / 441f : 340f / 405f;
            return new Rect(foot.x + (source.x - Anchors[family][pose]) * scale,
                foot.y - source.height * scale - (family == 0 ? 10f : 0), source.width * scale, source.height * scale);
        }
        public bool Draw(string id, int frame, Vector2 foot, Material material, out Rect bounds)
        {
            bounds = BoundsFor(id, frame, foot);
            int family = id == "frost-boss" ? 0 : 1;
            if (!attempted[family])
            {
                attempted[family] = true;
                textures[family] = Resources.Load<Texture2D>(family == 0 ? FrostPath : GardenPath);
            }
            Texture2D texture = textures[family];
            if (texture == null) return false;
            if (!clipAttempted[family] && material != null && SystemInfo.supportsRenderTextures &&
                SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
            {
                clipAttempted[family] = true;
                RenderTexture previous = RenderTexture.active;
                try
                {
                    var target = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                    clipped[family] = target; target.filterMode = FilterMode.Point;
                    if (target.Create()) Graphics.Blit(texture, target, material);
                    else { target.Release(); Dispose(target); clipped[family] = null; }
                }
                catch (Exception)
                {
                    if (clipped[family] != null) { clipped[family].Release(); Dispose(clipped[family]); clipped[family] = null; }
                }
                finally { RenderTexture.active = previous; }
            }
            Rect source = Sources[family][frame - 24];
            GUI.DrawTextureWithTexCoords(bounds, clipped[family] != null ? (Texture)clipped[family] : texture,
                new Rect(source.x / 1536f, 1f - source.yMax / 1024f, source.width / 1536f, source.height / 1024f), true);
            return true;
        }
        public void Release()
        {
            for (int i = 0; i < textures.Length; i++)
            {
                if (clipped[i] != null) { clipped[i].Release(); Dispose(clipped[i]); }
                if (textures[i] != null) Resources.UnloadAsset(textures[i]);
                clipped[i] = null; textures[i] = null; attempted[i] = clipAttempted[i] = false;
            }
        }
        static void Dispose(UnityEngine.Object value)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
