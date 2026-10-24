using System;
using System.Collections.Generic;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    [Serializable] public sealed class ForestEnemyManifest
    {
        public string version;
        public ForestEnemySheet[] sheets;
    }

    [Serializable] public sealed class ForestEnemySheet
    {
        public string id, resourcePath;
        public int width, height;
        public float anchorX, referenceHeight;
        // Row cuts are top-origin; each row has seven independent column cuts.
        public int[] rowCuts, columnCuts;
        public ForestEnemyFrame[] frames;
    }

    [Serializable] public sealed class ForestEnemyFrame
    {
        // Source coordinates use the texture's bottom-left origin.
        public int x, y, width, height;
    }

    // The four forest sheets share one frame contract. All authored poses face left.
    public sealed class ForestEnemyArt
    {
        public const string ManifestPath = "Art/Enemies/v019/forest-manifest";
        public static readonly string[] Species = { "forest-cub", "forest-thorn", "forest-elite", "forest-boss" };
        readonly Dictionary<string, ForestEnemySheet> entries = new Dictionary<string, ForestEnemySheet>();
        readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, RenderTexture> clippedTextures = new Dictionary<string, RenderTexture>();
        readonly HashSet<string> missing = new HashSet<string>();
        Material clipMaterial;

        public ForestEnemyArt()
        {
            LoadManifest(ManifestPath);
            LoadManifest(CastleEnemyArt.ManifestPath);
            LoadManifest(MineEnemyArt.ManifestPath);
            LoadManifest(FrostEnemyArt.ManifestPath);
            Shader clipShader = Shader.Find("Expedition/CatalogSpriteClip");
            if (clipShader != null && clipShader.isSupported)
            {
                clipMaterial = new Material(clipShader);
                clipMaterial.SetFloat("_Cutoff", 103f / 255f);
            }
        }

        void LoadManifest(string path)
        {
            TextAsset asset = Resources.Load<TextAsset>(path);
            if (asset == null) return;
            ForestEnemyManifest manifest = JsonUtility.FromJson<ForestEnemyManifest>(asset.text);
            if (manifest == null || manifest.sheets == null) return;
            foreach (ForestEnemySheet sheet in manifest.sheets)
                if (sheet != null && !string.IsNullOrEmpty(sheet.id) && Valid(sheet)) entries[sheet.id] = sheet;
        }

        public static string SpeciesFor(CatalogBattleUnit unit)
        {
            if (unit == null || unit.Team != CatalogTeam.Enemy) return null;
            if (unit.Id.StartsWith("mine-", StringComparison.Ordinal) ||
                unit.Id == "furnace-boss" ||
                unit.Id.StartsWith("frost-", StringComparison.Ordinal)) return null;
            if (unit.IsBoss) return unit.EnemyRole == CatalogEnemyRole.Beast ? Species[3] : null;
            if (unit.Id.StartsWith("stone-", StringComparison.Ordinal) ||
                unit.Id.StartsWith("crossbow-", StringComparison.Ordinal)) return null;
            if (unit.Id.StartsWith("spine-", StringComparison.Ordinal)) return Species[1];
            if (unit.EnemyRole != CatalogEnemyRole.Beast) return null;
            return unit.Id.IndexOf("elite", StringComparison.Ordinal) >= 0 ? Species[2] : Species[0];
        }

        public static float SilhouetteHeight(string id)
        {
            return id == Species[0] ? 78f : id == Species[1] ? 112f :
                id == Species[2] ? 138f : id == Species[3] ? 244f :
                CastleEnemyArt.SilhouetteHeight(id) > 0f ? CastleEnemyArt.SilhouetteHeight(id) :
                MineEnemyArt.SilhouetteHeight(id) > 0f ? MineEnemyArt.SilhouetteHeight(id) :
                FrostEnemyArt.SilhouetteHeight(id);
        }

        // Returns an absolute index in the 6 by 4 sheet. Attack release begins on
        // the same battle event that applies damage, while the warning owns 22-23.
        public static int FrameIndex(int state, float stateTime, float windupAge,
            float releaseRemaining, float releaseAge, float chargeWarningRemaining,
            bool chargeMoving, float attackWindupRemaining, bool chargedSpecies)
        {
            if (state == 4) return 18 + Mathf.Min(3, Mathf.Max(0, (int)(stateTime * 7f)));
            if (chargedSpecies && chargeWarningRemaining > 0f)
                return 22 + Mathf.Min(1, Mathf.Max(0, (int)(windupAge * 4f)));
            if (chargeMoving)
                return 14 + Mathf.Min(3, Mathf.Max(0, (int)((.2f - attackWindupRemaining) / .05f)));
            if (releaseRemaining > 0f)
                return 14 + Mathf.Min(3, Mathf.Max(0, (int)(releaseAge / .045f)));
            if (state == 3) return 4 + (Mathf.Max(0, (int)(stateTime * 12f)) % 2);
            if (state == 1) return 6 + (Mathf.Max(0, (int)(stateTime * 8f)) % 6);
            if (state != 2) return Mathf.Max(0, (int)(stateTime * 5f)) % 4;
            return 12 + Mathf.Min(1, Mathf.Max(0, (int)(windupAge / .125f)));
        }

        public bool TryGetBounds(string id, int index, Vector2 foot, out Rect bounds)
        {
            bounds = default(Rect);
            if (!entries.TryGetValue(id, out ForestEnemySheet sheet) || !Valid(sheet) || index < 0 || index >= 24)
                return false;
            ForestEnemyFrame frame = sheet.frames[index];
            float nominalLeft = (index % 6) * sheet.width / 6f;
            float scale = SilhouetteHeight(id) / sheet.referenceHeight;
            bounds = new Rect(
                foot.x + (frame.x - nominalLeft - sheet.anchorX) * scale,
                foot.y - frame.height * scale,
                frame.width * scale, frame.height * scale);
            return bounds.width > 0 && bounds.height > 0;
        }

        public bool Draw(string id, int index, Vector2 foot, out Rect bounds)
        {
            if (!TryGetBounds(id, index, foot, out bounds)) return false;
            ForestEnemySheet sheet = entries[id];
            if (!textures.TryGetValue(id, out Texture2D texture) && !missing.Contains(id))
            {
                texture = Resources.Load<Texture2D>(sheet.resourcePath);
                if (texture == null) missing.Add(id);
                else
                {
                    texture.filterMode = FilterMode.Point;
                    textures[id] = texture;
                    if (clipMaterial != null && SystemInfo.supportsRenderTextures &&
                        SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
                    {
                        RenderTexture clipped = null;
                        RenderTexture previous = RenderTexture.active;
                        try
                        {
                            clipped = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                            clipped.filterMode = FilterMode.Point;
                            if (clipped.Create())
                            {
                                Graphics.Blit(texture, clipped, clipMaterial);
                                clippedTextures[id] = clipped;
                            }
                            else { clipped.Release(); DisposeObject(clipped); }
                        }
                        catch (Exception)
                        {
                            if (clipped != null) { clipped.Release(); DisposeObject(clipped); }
                        }
                        finally { RenderTexture.active = previous; }
                    }
                }
            }
            if (texture == null || texture.width != sheet.width || texture.height != sheet.height) return false;
            ForestEnemyFrame frame = sheet.frames[index];
            Rect uv = new Rect((float)frame.x / sheet.width, (float)frame.y / sheet.height,
                (float)frame.width / sheet.width, (float)frame.height / sheet.height);
            Texture displayed = clippedTextures.TryGetValue(id, out RenderTexture clippedTexture) ?
                clippedTexture : (Texture)texture;
            GUI.DrawTextureWithTexCoords(bounds, displayed, uv, true);
            return true;
        }

        public static bool Valid(ForestEnemySheet sheet)
        {
            if (sheet == null || sheet.width <= 0 || sheet.height <= 0 ||
                sheet.referenceHeight <= 0 || sheet.frames == null || sheet.frames.Length != 24 ||
                sheet.rowCuts == null || sheet.rowCuts.Length != 5 ||
                sheet.columnCuts == null || sheet.columnCuts.Length != 28) return false;
            if (sheet.rowCuts[0] != 0 || sheet.rowCuts[4] != sheet.height) return false;
            for (int row = 0; row < 4; row++)
            {
                if (sheet.rowCuts[row] >= sheet.rowCuts[row + 1] ||
                    sheet.columnCuts[row * 7] != 0 || sheet.columnCuts[row * 7 + 6] != sheet.width)
                    return false;
                for (int col = 0; col < 6; col++)
                    if (sheet.columnCuts[row * 7 + col] >= sheet.columnCuts[row * 7 + col + 1])
                        return false;
            }
            for (int index = 0; index < 24; index++)
            {
                ForestEnemyFrame frame = sheet.frames[index];
                int row = index / 6, col = index % 6;
                int left = sheet.columnCuts[row * 7 + col];
                int right = sheet.columnCuts[row * 7 + col + 1];
                int bottom = sheet.height - sheet.rowCuts[row + 1];
                int top = sheet.height - sheet.rowCuts[row];
                if (frame == null || frame.width <= 0 || frame.height <= 0 ||
                    frame.x < left || frame.x + frame.width > right ||
                    frame.y < bottom || frame.y + frame.height > top) return false;
            }
            return true;
        }

        public void Release()
        {
            foreach (RenderTexture texture in clippedTextures.Values)
                if (texture != null) { texture.Release(); DisposeObject(texture); }
            clippedTextures.Clear();
            foreach (Texture2D texture in textures.Values)
                if (texture != null) Resources.UnloadAsset(texture);
            textures.Clear();
            missing.Clear();
            if (clipMaterial != null) DisposeObject(clipMaterial);
            clipMaterial = null;
        }

        static void DisposeObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
