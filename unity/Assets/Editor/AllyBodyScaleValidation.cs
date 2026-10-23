using System;
using System.Collections.Generic;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class AllyBodyScaleValidation
    {
        [Serializable] sealed class ArtManifest { public ArtSheet[] entries; }
        [Serializable] sealed class ArtSheet
        {
            public string id;
            public int width, height;
            public ArtFrame[] frames;
        }
        [Serializable] sealed class ArtFrame
        {
            public float width, height, pivotX, pivotY;
        }

        static int assertions;

        static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("Ally body scale: " + message);
        }

        [MenuItem("Expedition/Validate Ally Body Scale")]
        public static void Validate()
        {
            assertions = 0;
            TextAsset calibrationAsset = Resources.Load<TextAsset>(CatalogBodyScale.ResourcePath);
            Check(calibrationAsset != null, "v022 body-scale resource is missing");
            CatalogBodyScaleData data = JsonUtility.FromJson<CatalogBodyScaleData>(calibrationAsset.text);
            Check(data != null && data.version == "0.22" &&
                Mathf.Abs(data.referenceBodyHeight - 179.2f) < .01f &&
                data.entries != null && data.entries.Length == 30,
                "calibration must contain the 30 forms and the 179.2 source-pixel reference");
            TextAsset catalogAsset = Resources.Load<TextAsset>("Content/unit-catalog-v010");
            TextAsset artAsset = Resources.Load<TextAsset>("Art/Units/v010/art-manifest");
            Check(catalogAsset != null && artAsset != null, "catalog or art manifest is missing");
            CatalogDatabase catalog = CatalogDatabase.FromJson(catalogAsset.text);
            ArtManifest art = JsonUtility.FromJson<ArtManifest>(artAsset.text);
            Check(art != null && art.entries != null && art.entries.Length == 30,
                "art manifest must contain 30 sheets");
            var catalogIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CatalogForm form in catalog.All) catalogIds.Add(form.Id);
            Check(catalogIds.Count == 30, "catalog must have 30 unique forms");
            var artById = new Dictionary<string, ArtSheet>(StringComparer.Ordinal);
            foreach (ArtSheet sheet in art.entries)
            {
                Check(sheet != null && !string.IsNullOrEmpty(sheet.id) && !artById.ContainsKey(sheet.id),
                    "art sheet IDs must be unique");
                artById.Add(sheet.id, sheet);
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var scale = new CatalogBodyScale(data);
            Rect destination = new Rect(100, 100, 120, 144);
            foreach (CatalogBodyScaleEntry entry in data.entries)
            {
                Check(CatalogBodyScale.ValidEntry(entry) && seen.Add(entry.id) && catalogIds.Contains(entry.id),
                    "calibration IDs and body heights must be valid and unique");
                Check(artById.TryGetValue(entry.id, out ArtSheet sheet) &&
                    entry.sourceWidth == sheet.width && entry.sourceHeight == sheet.height,
                    entry.id + " calibration source dimensions differ from art manifest");
                float expected = data.referenceBodyHeight / entry.bodyHeight;
                float multiplier = scale.Multiplier(entry.id, sheet.width, sheet.height);
                Check(Mathf.Abs(multiplier - expected) < .0001f,
                    entry.id + " fixed multiplier differs from neutral-pose calibration");
                Check(sheet.frames != null && sheet.frames.Length == 22,
                    entry.id + " must retain 22 animation frames");
                float idleHud = scale.IdleHudHeight(entry.id, sheet.width, sheet.height,
                    sheet.frames[0].height, 120f, 144f, 12f, 134f);
                float idleScale = Mathf.Min(120f / (sheet.width / 6f), 144f / (sheet.height / 4f));
                Check(Mathf.Abs(idleHud - (sheet.frames[0].height * idleScale * multiplier + 12f)) < .01f,
                    entry.id + " HUD must use first idle silhouette plus fixed clearance");
                foreach (int frameIndex in new[] { 0, 12, 18 })
                {
                    ArtFrame frame = sheet.frames[frameIndex];
                    Check(frame != null && frame.width > 0 && frame.height > 0 &&
                        frame.pivotX >= 0 && frame.pivotX <= 1 &&
                        frame.pivotY >= 0 && frame.pivotY <= 1,
                        entry.id + " idle/attack/death frame metadata is invalid");
                    const float baseScale = 120f / 256f;
                    Rect fitted = CatalogBodyScale.Fit(destination, frame.pivotX, frame.pivotY,
                        frame.width, frame.height, baseScale, multiplier);
                    Rect flipped = CatalogBodyScale.Fit(destination, 1f - frame.pivotX, frame.pivotY,
                        frame.width, frame.height, baseScale, multiplier);
                    Check(Mathf.Abs(fitted.x + frame.pivotX * fitted.width - destination.center.x) < .01f &&
                        Mathf.Abs(fitted.y + (1f - frame.pivotY) * fitted.height - destination.yMax) < .01f &&
                        Mathf.Abs(flipped.x + (1f - frame.pivotX) * flipped.width - destination.center.x) < .01f &&
                        Mathf.Abs(flipped.y + (1f - frame.pivotY) * flipped.height - destination.yMax) < .01f,
                        entry.id + " ground pivot changes across pose or facing");
                    Check(Mathf.Abs(fitted.width / frame.width - baseScale * multiplier) < .0001f &&
                        Mathf.Abs(flipped.height / frame.height - baseScale * multiplier) < .0001f,
                        entry.id + " attack/death frame changed the fixed form scale");
                }
            }
            Check(seen.SetEquals(catalogIds), "calibration must cover exactly the 30 catalog forms");
            Check(scale.Multiplier("not-a-form", 1536, 1024) == 1f,
                "missing form uses existing scale");
            CatalogBodyScaleEntry sample = data.entries[0];
            Check(scale.Multiplier(sample.id, sample.sourceWidth + 1, sample.sourceHeight) == 1f &&
                scale.Multiplier(sample.id, sample.sourceWidth, sample.sourceHeight + 1) == 1f,
                "mismatched source dimensions use existing scale");
            Check(scale.IdleHudHeight(sample.id, sample.sourceWidth + 1, sample.sourceHeight,
                100f, 120f, 144f, 12f, 134f) == 134f &&
                new CatalogBodyScale(null).IdleHudHeight(sample.id, sample.sourceWidth,
                    sample.sourceHeight, 100f, 120f, 144f, 12f, 134f) == 134f,
                "uncalibrated HUD uses its previous height");
            Check(new CatalogBodyScale(null).Multiplier(sample.id, sample.sourceWidth, sample.sourceHeight) == 1f &&
                new CatalogBodyScale(new CatalogBodyScaleData {
                    version = "0.22", referenceBodyHeight = 179.2f,
                    entries = new[] { sample, sample }
                }).Multiplier(sample.id, sample.sourceWidth, sample.sourceHeight) == 1f &&
                new CatalogBodyScale(new CatalogBodyScaleData {
                    version = "0.22", referenceBodyHeight = 179.2f,
                    entries = new[] { new CatalogBodyScaleEntry {
                        id = sample.id, bodyHeight = 0,
                        sourceWidth = sample.sourceWidth, sourceHeight = sample.sourceHeight } }
                }).Multiplier(sample.id, sample.sourceWidth, sample.sourceHeight) == 1f,
                "missing, duplicate, or invalid calibration uses existing scale");
            Debug.Log("Ally body scale validated: " + assertions + " assertions across 30 forms.");
        }

        public static void BuildAndroid()
        {
            Validate();
            CastleEnemyArtValidation.BuildAndroid();
        }
    }
}
