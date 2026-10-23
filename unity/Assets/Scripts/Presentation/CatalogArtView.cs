using System;
using System.Collections.Generic;
using UnityEngine;

namespace Expedition.Presentation
{
    // The preparation screen uses the same manifest crop and opaque-background clip as the combat lab.
    internal sealed class CatalogArtView : IDisposable
    {
        [Serializable] sealed class Manifest { public Entry[] entries; }
        [Serializable] sealed class Entry
        {
            public string id, resourcePath;
            public int width, height;
            public Frame[] frames;
        }
        [Serializable] sealed class Frame
        {
            public float x, y, width, height, pivotX, pivotY;
        }

        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        readonly Dictionary<string, Texture2D> sheets = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, RenderTexture> clipped = new Dictionary<string, RenderTexture>();
        readonly HashSet<string> missing = new HashSet<string>();
        readonly CatalogBodyScale bodyScale = new CatalogBodyScale();
        Material clipMaterial;

        public CatalogArtView()
        {
            TextAsset source = Resources.Load<TextAsset>("Art/Units/v010/art-manifest");
            if (source != null)
            {
                Manifest manifest = JsonUtility.FromJson<Manifest>(source.text);
                if (manifest != null && manifest.entries != null)
                    foreach (Entry entry in manifest.entries)
                        if (entry != null && !string.IsNullOrEmpty(entry.id)) entries[entry.id] = entry;
            }
            Shader shader = Shader.Find("Expedition/CatalogSpriteClip");
            if (shader != null && shader.isSupported) clipMaterial = new Material(shader);
        }

        public void Draw(string id, Rect destination, int frame)
        {
            if (string.IsNullOrEmpty(id)) return;
            Entry entry;
            entries.TryGetValue(id, out entry);
            Texture2D sheet = Load(id, entry);
            if (sheet == null)
            {
                Color old = GUI.color;
                GUI.color = new Color(.35f, .47f, .39f);
                GUI.DrawTexture(destination, Texture2D.whiteTexture);
                GUI.color = old;
                return;
            }
            int index = Mathf.Clamp(frame, 0, 3);
            Rect uv;
            float pivotX = .5f, pivotY = 0f, cropWidth, cropHeight;
            if (entry != null && entry.frames != null && entry.frames.Length > index &&
                entry.frames[index] != null && entry.width > 0 && entry.height > 0 &&
                entry.frames[index].width > 0 && entry.frames[index].height > 0)
            {
                Frame cell = entry.frames[index];
                uv = new Rect(cell.x / entry.width, cell.y / entry.height,
                    cell.width / entry.width, cell.height / entry.height);
                pivotX = Mathf.Clamp01(cell.pivotX);
                pivotY = Mathf.Clamp01(cell.pivotY);
                cropWidth = cell.width;
                cropHeight = cell.height;
            }
            else
            {
                uv = new Rect(index / 6f, .75f, 1f / 6f, 1f / 4f);
                cropWidth = sheet.width / 6f;
                cropHeight = sheet.height / 4f;
            }
            float cellWidth = entry != null && entry.width > 0 ? entry.width / 6f : sheet.width / 6f;
            float cellHeight = entry != null && entry.height > 0 ? entry.height / 4f : sheet.height / 4f;
            float scale = Mathf.Min(destination.width / cellWidth, destination.height / cellHeight);
            float multiplier = entry == null ? 1f : bodyScale.Multiplier(id, entry.width, entry.height);
            Rect fitted = CatalogBodyScale.Fit(destination, pivotX, pivotY,
                cropWidth, cropHeight, scale, multiplier);
            RenderTexture output;
            Texture display = clipped.TryGetValue(id, out output) ? (Texture)output : sheet;
            GUI.DrawTextureWithTexCoords(fitted, display, uv, true);
        }

        Texture2D Load(string id, Entry entry)
        {
            Texture2D existing;
            if (sheets.TryGetValue(id, out existing)) return existing;
            if (missing.Contains(id)) return null;
            string path = entry != null && !string.IsNullOrEmpty(entry.resourcePath) ? entry.resourcePath :
                "Art/Units/v010/" + id;
            if (path.StartsWith("Assets/Resources/", StringComparison.Ordinal))
                path = path.Substring("Assets/Resources/".Length);
            if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 4);
            Texture2D sheet = Resources.Load<Texture2D>(path);
            if (sheet == null) { missing.Add(id); return null; }
            sheet.filterMode = FilterMode.Point;
            sheets[id] = sheet;
            if (clipMaterial != null && SystemInfo.supportsRenderTextures &&
                SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
            {
                RenderTexture target = null;
                RenderTexture previous = RenderTexture.active;
                try
                {
                    target = new RenderTexture(sheet.width, sheet.height, 0, RenderTextureFormat.ARGB32);
                    target.filterMode = FilterMode.Point;
                    if (target.Create())
                    {
                        Graphics.Blit(sheet, target, clipMaterial);
                        clipped[id] = target;
                    }
                    else { target.Release(); UnityEngine.Object.Destroy(target); }
                }
                catch (Exception)
                {
                    if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
                }
                finally { RenderTexture.active = previous; }
            }
            return sheet;
        }

        public void Dispose()
        {
            foreach (RenderTexture texture in clipped.Values)
                if (texture != null) { texture.Release(); UnityEngine.Object.Destroy(texture); }
            clipped.Clear();
            foreach (Texture2D texture in sheets.Values)
                if (texture != null) Resources.UnloadAsset(texture);
            sheets.Clear();
            if (clipMaterial != null) UnityEngine.Object.Destroy(clipMaterial);
            clipMaterial = null;
        }
    }
}
