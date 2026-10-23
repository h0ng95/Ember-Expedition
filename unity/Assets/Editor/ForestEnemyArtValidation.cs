using System;
using System.Collections.Generic;
using System.IO;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class ForestEnemyArtValidation
    {
        const string Folder = "Assets/Resources/Art/Enemies/v019";
        const string ManifestFile = Folder + "/forest-manifest.json";
        const byte AlphaCutoff = 102; // alpha > .4
        static int assertions;

        static void Check(bool result, string message)
        {
            assertions++;
            if (!result) throw new InvalidOperationException("Forest enemy art: " + message);
        }

        [MenuItem("Expedition/Forest Enemies/Generate Manifest")]
        public static void GenerateManifest()
        {
            ForestEnemyManifest manifest = BuildManifest();
            Directory.CreateDirectory(Folder);
            File.WriteAllText(ManifestFile, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(ManifestFile, ImportAssetOptions.ForceUpdate);
            Debug.Log("Forest enemy manifest generated: " + ManifestFile);
        }

        static ForestEnemyManifest BuildManifest()
        {
            var manifest = new ForestEnemyManifest {
                version = "0.19", sheets = new ForestEnemySheet[ForestEnemyArt.Species.Length]
            };
            for (int i = 0; i < manifest.sheets.Length; i++)
                manifest.sheets[i] = ReadSheet(ForestEnemyArt.Species[i]);
            return manifest;
        }

        static ForestEnemySheet ReadSheet(string id)
        {
            string path = Folder + "/" + id + ".png";
            if (!File.Exists(path)) throw new FileNotFoundException("Forest sheet is missing", path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path), false))
                    throw new InvalidOperationException("Cannot decode " + path);
                int width = texture.width, height = texture.height;
                Color32[] pixels = texture.GetPixels32();
                var sheet = new ForestEnemySheet {
                    id = id, resourcePath = "Art/Enemies/v019/" + id,
                    width = width, height = height, frames = new ForestEnemyFrame[24],
                    rowCuts = new int[5], columnCuts = new int[28]
                };
                // Generated art can cross nominal grid lines. Locate transparent
                // gutters around each expected seam and reject occupied seams.
                int[] yProfile = new int[height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        if (pixels[(height - 1 - y) * width + x].a > AlphaCutoff) yProfile[y]++;
                sheet.rowCuts[0] = 0;
                sheet.rowCuts[4] = height;
                for (int row = 1; row < 4; row++)
                    sheet.rowCuts[row] = ClearGap(yProfile, height * row / 4f, height / 12, id + " row " + row);
                for (int row = 0; row < 4; row++)
                {
                    int top = sheet.rowCuts[row], bottom = sheet.rowCuts[row + 1];
                    int[] xProfile = new int[width];
                    for (int y = top; y < bottom; y++)
                        for (int x = 0; x < width; x++)
                            if (pixels[(height - 1 - y) * width + x].a > AlphaCutoff) xProfile[x]++;
                    sheet.columnCuts[row * 7] = 0;
                    sheet.columnCuts[row * 7 + 6] = width;
                    for (int col = 1; col < 6; col++)
                        sheet.columnCuts[row * 7 + col] = ClearGap(xProfile,
                            width * col / 6f, width / 24, id + " row " + row + " column " + col);
                }
                var standingCenters = new List<float>();
                for (int index = 0; index < 24; index++)
                {
                    int column = index % 6, row = index / 6;
                    int left = sheet.columnCuts[row * 7 + column];
                    int right = sheet.columnCuts[row * 7 + column + 1];
                    int bottom = height - sheet.rowCuts[row + 1];
                    int top = height - sheet.rowCuts[row];
                    int minX = right, minY = top, maxX = left - 1, maxY = bottom - 1;
                    for (int y = bottom; y < top; y++)
                        for (int x = left; x < right; x++)
                            if (pixels[y * width + x].a > AlphaCutoff)
                            {
                                if (x < minX) minX = x;
                                if (x > maxX) maxX = x;
                                if (y < minY) minY = y;
                                if (y > maxY) maxY = y;
                            }
                    if (maxX < minX || maxY < minY)
                        throw new InvalidOperationException(id + " has an empty frame at " + index);
                    if (column > 0 && minX == left || column < 5 && maxX == right - 1 ||
                        row > 0 && maxY == top - 1 || row < 3 && minY == bottom)
                        throw new InvalidOperationException(id + " frame " + index + " touches an occupied seam");
                    sheet.frames[index] = new ForestEnemyFrame {
                        x = minX, y = minY, width = maxX - minX + 1, height = maxY - minY + 1
                    };
                    if (index < 4 || (index >= 6 && index <= 11))
                        standingCenters.Add(((minX + maxX + 1) * .5f) - column * width / 6f);
                }
                standingCenters.Sort();
                sheet.anchorX = (standingCenters[4] + standingCenters[5]) * .5f;
                sheet.referenceHeight = sheet.frames[0].height;
                if (!ForestEnemyArt.Valid(sheet)) throw new InvalidOperationException(id + " frame bounds are invalid");
                return sheet;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static int ClearGap(int[] profile, float desired, int radius, string description)
        {
            int from = Mathf.Max(1, Mathf.RoundToInt(desired) - radius);
            int to = Mathf.Min(profile.Length - 2, Mathf.RoundToInt(desired) + radius);
            int bestStart = -1, bestEnd = -1;
            for (int position = from; position <= to;)
            {
                if (profile[position] != 0) { position++; continue; }
                int start = position;
                while (position <= to && profile[position] == 0) position++;
                int end = position - 1;
                if (bestStart < 0 || end - start > bestEnd - bestStart ||
                    end - start == bestEnd - bestStart &&
                    Mathf.Abs((start + end) * .5f - desired) <
                    Mathf.Abs((bestStart + bestEnd) * .5f - desired))
                { bestStart = start; bestEnd = end; }
            }
            if (bestStart < 0)
                throw new InvalidOperationException(description + " has no transparent gutter near " + desired);
            int cut = Mathf.RoundToInt((bestStart + bestEnd) * .5f);
            if (profile[cut] != 0) throw new InvalidOperationException(description + " seam crosses visible pixels");
            return cut;
        }

        [MenuItem("Expedition/Forest Enemies/Validate")]
        public static void Validate()
        {
            assertions = 0;
            ForestEnemyManifest fresh = BuildManifest();
            TextAsset saved = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestFile);
            Check(saved != null, "manifest is missing; run Generate Manifest");
            ForestEnemyManifest stored = JsonUtility.FromJson<ForestEnemyManifest>(saved.text);
            Check(stored != null && stored.sheets != null && stored.sheets.Length == 4,
                "manifest must contain exactly four sheets");
            for (int i = 0; i < 4; i++)
            {
                ForestEnemySheet expected = fresh.sheets[i], actual = stored.sheets[i];
                Check(actual != null && actual.id == ForestEnemyArt.Species[i] && ForestEnemyArt.Valid(actual),
                    "mapping or frame bounds for " + ForestEnemyArt.Species[i]);
                Check(actual.width == expected.width && actual.height == expected.height &&
                    actual.referenceHeight == expected.referenceHeight &&
                    Mathf.Abs(actual.anchorX - expected.anchorX) < .01f,
                    "sheet dimensions, scale, or center changed for " + actual.id);
                for (int c = 0; c < 5; c++)
                    Check(actual.rowCuts[c] == expected.rowCuts[c], actual.id + " row seam " + c + " changed");
                for (int c = 0; c < 28; c++)
                    Check(actual.columnCuts[c] == expected.columnCuts[c], actual.id + " column seam " + c + " changed");
                for (int f = 0; f < 24; f++)
                {
                    ForestEnemyFrame a = actual.frames[f], b = expected.frames[f];
                    Check(a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height,
                        actual.id + " frame " + f + " is stale or empty");
                }
                Check(ForestEnemyArt.SilhouetteHeight(actual.id) > 0f, "missing species scale");
            }
            Check(ForestEnemyArt.FrameIndex(0, 0f, 0f, 0f, 0f, 0f, false, 0f, false) == 0,
                "idle frame");
            Check(ForestEnemyArt.FrameIndex(1, .2f, 0f, 0f, 0f, 0f, false, 0f, false) >= 6,
                "walk frame");
            Check(ForestEnemyArt.FrameIndex(2, 0f, .3f, 0f, 0f, .4f, false, .4f, true) == 23,
                "charge warning frame");
            Check(ForestEnemyArt.FrameIndex(2, 0f, 0f, 0f, 0f, 0f, true, .2f, true) == 14,
                "charge movement frame");
            Check(ForestEnemyArt.FrameIndex(2, 0f, 0f, .18f, 0f, 0f, false, 0f, false) == 14,
                "damage release frame");
            Check(ForestEnemyArt.FrameIndex(3, 0f, 0f, 0f, 0f, 0f, false, 0f, false) == 4,
                "hit frame");
            Check(ForestEnemyArt.FrameIndex(4, 1f, 0f, 0f, 0f, 0f, false, 0f, false) == 21,
                "death terminal frame");
            Check(ForestEnemyArt.FrameIndex(3, 0f, .3f, .18f, 0f, .4f, false, .4f, true) == 23,
                "charge warning stays visible through a hit");
            Check(ForestEnemyArt.FrameIndex(3, 0f, .3f, .18f, 0f, 0f, true, .13f, true) == 15,
                "moving charge stays visible through a hit");
            Check(ForestEnemyArt.FrameIndex(3, 0f, 0f, .18f, 0f, 0f, false, 0f, false) == 14,
                "damage release stays visible through a hit");
            Check(ForestEnemyArt.FrameIndex(4, .1f, 0f, .18f, 0f, .4f, true, .1f, true) == 18,
                "death takes priority over attack effects");
            ValidateMapping();
            Debug.Log("Forest enemy art validated: " + assertions + " assertions, four sheets.");
        }

        static void ValidateMapping()
        {
            CatalogDatabase database = CatalogDatabase.FromJson(
                Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var battle = new CatalogBattle(database);
            CatalogBattleUnit cub = battle.AddEnemy("beast-1", 100, 10, 0, 1, 1, 1, false, 0, 0, CatalogEnemyRole.Beast);
            CatalogBattleUnit thorn = battle.AddEnemy("spine-raider-1", 100, 10, 0, 1, 3, 1, false, 0, 0, CatalogEnemyRole.Raider);
            CatalogBattleUnit elite = battle.AddEnemy("elite-beast", 100, 10, 0, 1, 1, 1, false, 0, 0, CatalogEnemyRole.Beast);
            CatalogBattleUnit boss = battle.AddEnemy("beast-boss", 100, 10, 0, 1, 1, 1, true, 0, 0, CatalogEnemyRole.Beast);
            CatalogBattleUnit stone = battle.AddEnemy("stone-elite", 100, 10, 0, 1, 1, 1, false, 0, 0, CatalogEnemyRole.Beast);
            Check(ForestEnemyArt.SpeciesFor(cub) == ForestEnemyArt.Species[0], "cub mapping");
            Check(ForestEnemyArt.SpeciesFor(thorn) == ForestEnemyArt.Species[1], "thorn mapping");
            Check(ForestEnemyArt.SpeciesFor(elite) == ForestEnemyArt.Species[2], "elite mapping");
            Check(ForestEnemyArt.SpeciesFor(boss) == ForestEnemyArt.Species[3], "boss mapping");
            Check(ForestEnemyArt.SpeciesFor(stone) == null, "stone keeps its own art");
        }

        // Invoked from Unity batch mode only after the source art has been imported.
        public static void BuildAndroid()
        {
            GenerateManifest();
            Validate();
            CombatSettlementValidation.Validate();
            BattleVisibilityValidation.Validate();
            BossHitReactionValidation.Validate();
            DragGrowthValidation.BuildAndroid();
        }
    }
}
