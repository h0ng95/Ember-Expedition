using System;
using System.Collections.Generic;
using UnityEngine;

namespace Expedition.Presentation
{
    [Serializable] public sealed class CatalogBodyScaleData
    {
        public string version;
        public float referenceBodyHeight;
        public CatalogBodyScaleEntry[] entries;
    }

    [Serializable] public sealed class CatalogBodyScaleEntry
    {
        public string id;
        public float bodyHeight;
        public int sourceWidth, sourceHeight;
    }

    // Calibration comes from the neutral idle pose. The multiplier is chosen
    // once per form, never from the currently displayed animation frame.
    public sealed class CatalogBodyScale
    {
        public const string ResourcePath = "Art/Units/v022/body-scale";
        readonly Dictionary<string, CatalogBodyScaleEntry> entries =
            new Dictionary<string, CatalogBodyScaleEntry>(StringComparer.Ordinal);
        readonly float referenceBodyHeight;

        public CatalogBodyScale() : this(ReadResource()) { }

        public CatalogBodyScale(CatalogBodyScaleData data)
        {
            if (data == null || data.version != "0.22" || !FinitePositive(data.referenceBodyHeight) ||
                data.entries == null) return;
            referenceBodyHeight = data.referenceBodyHeight;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            foreach (CatalogBodyScaleEntry entry in data.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id) || duplicates.Contains(entry.id)) continue;
                if (!seen.Add(entry.id))
                {
                    entries.Remove(entry.id);
                    duplicates.Add(entry.id);
                }
                else if (ValidEntry(entry)) entries.Add(entry.id, entry);
            }
        }

        static CatalogBodyScaleData ReadResource()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) return null;
            try { return JsonUtility.FromJson<CatalogBodyScaleData>(asset.text); }
            catch (Exception) { return null; }
        }

        public static bool ValidEntry(CatalogBodyScaleEntry entry)
        {
            return entry != null && !string.IsNullOrEmpty(entry.id) &&
                entry.sourceWidth > 0 && entry.sourceHeight > 0 &&
                FinitePositive(entry.bodyHeight) &&
                entry.bodyHeight <= entry.sourceHeight / 4f;
        }

        static bool FinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public float Multiplier(string id, int manifestWidth, int manifestHeight)
        {
            return TryMultiplier(id, manifestWidth, manifestHeight, out float multiplier) ? multiplier : 1f;
        }

        public bool TryMultiplier(string id, int manifestWidth, int manifestHeight, out float multiplier)
        {
            multiplier = 1f;
            CatalogBodyScaleEntry entry;
            if (referenceBodyHeight <= 0f || id == null ||
                !entries.TryGetValue(id, out entry) ||
                entry.sourceWidth != manifestWidth || entry.sourceHeight != manifestHeight)
                return false;
            float calibrated = referenceBodyHeight / entry.bodyHeight;
            if (!FinitePositive(calibrated)) return false;
            multiplier = calibrated;
            return true;
        }

        public float IdleHudHeight(string id, int manifestWidth, int manifestHeight,
            float idleFrameHeight, float destinationWidth, float destinationHeight,
            float padding, float fallback)
        {
            if (!TryMultiplier(id, manifestWidth, manifestHeight, out float multiplier) ||
                !FinitePositive(idleFrameHeight) || manifestWidth <= 0 || manifestHeight <= 0 ||
                !FinitePositive(destinationWidth) || !FinitePositive(destinationHeight))
                return fallback;
            float baseScale = Mathf.Min(destinationWidth / (manifestWidth / 6f),
                destinationHeight / (manifestHeight / 4f));
            return idleFrameHeight * baseScale * multiplier + padding;
        }

        public float PersonHeight(string id, float destinationWidth, float destinationHeight)
        {
            if (id == null || !entries.TryGetValue(id, out CatalogBodyScaleEntry entry)) return 84f;
            return referenceBodyHeight * Mathf.Min(destinationWidth / (entry.sourceWidth / 6f),
                destinationHeight / (entry.sourceHeight / 4f));
        }

        // The manifest pivot remains at the destination's ground point after
        // scaling, flipping, and switching between attack or death frames.
        public static Rect Fit(Rect destination, float pivotX, float pivotY,
            float frameWidth, float frameHeight, float baseScale, float multiplier)
        {
            float width = frameWidth * baseScale * multiplier;
            float height = frameHeight * baseScale * multiplier;
            return new Rect(destination.center.x - pivotX * width,
                destination.yMax - (1f - pivotY) * height, width, height);
        }
    }
}
