using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class BossSpriteEffectsValidation
    {
        static readonly string[] Sheets = {
            "Assets/Resources/Art/Effects/v039/garden-roots.png",
            "Assets/Resources/Art/Effects/v039/dragon-breath.png",
            "Assets/Resources/Art/Effects/v039/dragon-firefall.png",
            "Assets/Resources/Art/Effects/v039/golem-shatter.png",
            "Assets/Resources/Art/Effects/v039/frost-eruption.png",
            "Assets/Resources/Art/Effects/v039/charge-dust.png"
        };

        [MenuItem("Expedition/Validate Boss Effect Sheets")]
        public static void Validate()
        {
            foreach (string path in Sheets) ValidateSheet(path);
            Debug.Log("Boss effect sheets: six transparent 4x2 sheets, 48 occupied frames, point imports verified.");
        }

        static int ColumnCut(string path, int row, int column, int width)
        {
            if (row == 0 && path.EndsWith("charge-dust.png", StringComparison.Ordinal))
            {
                float ratio = column == 0 ? 0f : column == 1 ? 444f / 1774f :
                    column == 2 ? 820f / 1774f : column == 3 ? 1280f / 1774f : 1f;
                return Mathf.RoundToInt(ratio * width);
            }
            return Mathf.RoundToInt(column * width / 4f);
        }

        static void ValidateSheet(string path)
        {
            if (!File.Exists(path)) throw new InvalidOperationException("Missing boss effect sheet: " + path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Default ||
                importer.filterMode != FilterMode.Point || importer.wrapMode != TextureWrapMode.Clamp ||
                importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.textureShape != TextureImporterShape.Texture2D ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                importer.maxTextureSize != 1024)
                throw new InvalidOperationException("Incorrect effect texture import: " + path);

            // Decode source bytes directly; the runtime importer intentionally keeps textures unreadable.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path), false) ||
                    texture.width < 4 || texture.height < 2)
                    throw new InvalidOperationException("Invalid 4x2 effect sheet dimensions: " + path);
                Color32[] pixels = texture.GetPixels32();
                for (int frame = 0; frame < 8; frame++)
                {
                    int column = frame % 4, row = frame / 4;
                    int count = 0;
                    int left = ColumnCut(path, row, column, texture.width);
                    int right = ColumnCut(path, row, column + 1, texture.width);
                    int yStart = texture.height - Mathf.RoundToInt((row + 1) * texture.height / 2f);
                    int yEnd = texture.height - Mathf.RoundToInt(row * texture.height / 2f);
                    for (int y = yStart; y < yEnd; y++)
                        for (int x = left; x < right; x++)
                            if (pixels[y * texture.width + x].a > 102) count++;
                    if (count == 0)
                        throw new InvalidOperationException("Empty boss effect frame " + frame + ": " + path);
                }
                // A visible seam would cut a frame or leak into its neighbor when sampled by UV.
                for (int row = 0; row < 2; row++)
                {
                    int yStart = texture.height - Mathf.RoundToInt((row + 1) * texture.height / 2f);
                    int yEnd = texture.height - Mathf.RoundToInt(row * texture.height / 2f);
                    for (int column = 1; column < 4; column++)
                    {
                        int cut = ColumnCut(path, row, column, texture.width);
                        for (int y = yStart; y < yEnd; y++)
                            if (pixels[y * texture.width + cut - 1].a > 102 ||
                                pixels[y * texture.width + cut].a > 102)
                                throw new InvalidOperationException("Occupied vertical frame seam: " + path);
                    }
                }
                int rowCut = texture.height - Mathf.RoundToInt(texture.height / 2f);
                for (int x = 0; x < texture.width; x++)
                    if (pixels[(rowCut - 1) * texture.width + x].a > 102 ||
                        pixels[rowCut * texture.width + x].a > 102)
                        throw new InvalidOperationException("Occupied horizontal frame seam: " + path);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
