using System;
using UnityEngine;

namespace Expedition.Presentation
{
    [Serializable] internal sealed class FrostBossWalkManifest
    {
        public int width, height, rowCut;
        public float anchorX, referenceBodyHeight;
        public FrostBossWalkFrame[] frames;
    }

    [Serializable] internal sealed class FrostBossWalkFrame
    {
        // Source pixels, top-left origin; footY is the actual ground contact row.
        public int x, y, width, height;
        public float footY;
    }

    // Only replaces frost-boss's six walking poses. All other states use the 24-frame sheet.
    internal sealed class FrostBossWalkArt
    {
        internal const string TexturePath = "Art/Enemies/v039/frost-boss-walk";
        internal const string ManifestPath = "Art/Enemies/v039/frost-boss-walk-manifest";
        FrostBossWalkManifest manifest;
        Texture2D texture;
        RenderTexture clippedTexture;
        bool attempted;
        bool clipAttempted;

        void Load()
        {
            if (attempted) return;
            attempted = true;
            TextAsset source = Resources.Load<TextAsset>(ManifestPath);
            if (source == null) return;
            try { manifest = JsonUtility.FromJson<FrostBossWalkManifest>(source.text); }
            catch (ArgumentException) { manifest = null; }
            Resources.UnloadAsset(source);
            if (!Valid(manifest)) { manifest = null; return; }
            texture = Resources.Load<Texture2D>(TexturePath);
            if (texture != null) texture.filterMode = FilterMode.Point;
        }

        internal static bool Valid(FrostBossWalkManifest data)
        {
            if (data == null || data.width < 3 || data.height < 2 ||
                data.rowCut <= 0 || data.rowCut >= data.height ||
                data.referenceBodyHeight <= 0f || data.anchorX < 0f ||
                data.anchorX > data.width / 3f || data.frames == null ||
                data.frames.Length != 6) return false;
            for (int index = 0; index < 6; index++)
            {
                int row = index / 3, column = index % 3;
                int left = Mathf.RoundToInt(column * data.width / 3f);
                int right = Mathf.RoundToInt((column + 1) * data.width / 3f);
                int top = row == 0 ? 0 : data.rowCut;
                int bottom = row == 0 ? data.rowCut : data.height;
                FrostBossWalkFrame frame = data.frames[index];
                if (frame == null || frame.width <= 0 || frame.height <= 0 ||
                    frame.x < left || frame.x + frame.width > right ||
                    frame.y < top || frame.y + frame.height > bottom ||
                    frame.footY < top || frame.footY > bottom) return false;
            }
            return true;
        }

        internal bool TryGetBounds(int walkFrame, Vector2 foot, out Rect bounds)
        {
            bounds = default(Rect);
            Load();
            if (texture == null || manifest == null || walkFrame < 0 || walkFrame >= 6) return false;
            FrostBossWalkFrame frame = manifest.frames[walkFrame];
            int column = walkFrame % 3;
            int cellLeft = Mathf.RoundToInt(column * manifest.width / 3f);
            float scale = FrostEnemyArt.SilhouetteHeight(FrostEnemyArt.Species[3]) /
                manifest.referenceBodyHeight;
            bounds = new Rect(foot.x + (frame.x - cellLeft - manifest.anchorX) * scale,
                foot.y - (frame.footY - frame.y) * scale,
                frame.width * scale, frame.height * scale);
            return true;
        }

        internal bool Draw(int walkFrame, Vector2 foot, Material clipMaterial, out Rect bounds)
        {
            if (!TryGetBounds(walkFrame, foot, out bounds)) return false;
            if (!clipAttempted && clipMaterial != null && SystemInfo.supportsRenderTextures &&
                SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
            {
                clipAttempted = true;
                RenderTexture previous = RenderTexture.active;
                try
                {
                    clippedTexture = new RenderTexture(texture.width, texture.height, 0,
                        RenderTextureFormat.ARGB32);
                    clippedTexture.filterMode = FilterMode.Point;
                    if (clippedTexture.Create()) Graphics.Blit(texture, clippedTexture, clipMaterial);
                    else { clippedTexture.Release(); DisposeObject(clippedTexture); clippedTexture = null; }
                }
                catch (Exception)
                {
                    if (clippedTexture != null)
                    { clippedTexture.Release(); DisposeObject(clippedTexture); clippedTexture = null; }
                }
                finally { RenderTexture.active = previous; }
            }
            FrostBossWalkFrame frame = manifest.frames[walkFrame];
            Rect uv = new Rect(frame.x / (float)manifest.width,
                1f - (frame.y + frame.height) / (float)manifest.height,
                frame.width / (float)manifest.width, frame.height / (float)manifest.height);
            GUI.DrawTextureWithTexCoords(bounds,
                clippedTexture != null ? (Texture)clippedTexture : texture, uv, true);
            return true;
        }

        internal void Release()
        {
            if (clippedTexture != null)
            { clippedTexture.Release(); DisposeObject(clippedTexture); }
            clippedTexture = null;
            clipAttempted = false;
            if (texture != null) Resources.UnloadAsset(texture);
            texture = null;
            manifest = null;
            attempted = false;
        }

        static void DisposeObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
