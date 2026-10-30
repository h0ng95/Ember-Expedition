using UnityEngine;

namespace Expedition.Presentation
{
    // Battlefield and ending share one palette conversion, including dark flame outlines.
    public static class IgnisCoreArt
    {
        public static Color RegionColor(int wave)
        {
            return wave == 5 ? new Color32(103, 233, 167, 255) :
                wave == 10 ? new Color32(255, 203, 105, 255) :
                wave == 15 ? new Color32(255, 137, 83, 255) :
                wave == 20 ? new Color32(126, 221, 255, 255) :
                wave == 25 ? new Color32(206, 155, 255, 255) : new Color32(255, 230, 161, 255);
        }
        public static Texture2D Create(int wave)
        {
            var source = Resources.Load<Texture2D>("Art/Ignis/ignis-living-core");
            if (source == null) return null;
            Color32[] pixels = source.GetPixels32();
            float hue, saturation, value;
            Color.RGBToHSV(RegionColor(wave), out hue, out saturation, out value);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color original = pixels[i]; if (original.a <= 0) continue;
                float h, s, v; Color.RGBToHSV(original, out h, out s, out v);
                float mask = (h < .19f || h > .96f) ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.03f, .2f, s)) : 0;
                Color tint = Color.HSVToRGB(hue, Mathf.Clamp01(s * (wave == 30 ? .45f : 1.12f)), v);
                Color result = Color.Lerp(original, tint, mask); result.a = original.a; pixels[i] = result;
            }
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            texture.name = "Ignis regional flame " + wave;
            texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }
    }
}
