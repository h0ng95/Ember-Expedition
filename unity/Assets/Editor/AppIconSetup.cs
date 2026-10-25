using System;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class AppIconSetup
    {
        public const string IconPath = "Assets/Art/Branding/ignis-app-icon.png";

        public static void Configure()
        {
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("App icon is missing: " + IconPath);
            if (ConfigureImporter(importer)) importer.SaveAndReimport();

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null) throw new InvalidOperationException("Cannot load app icon: " + IconPath);

            // The project default icon and both single-layer Android fallback kinds
            // use the exact same approved source image.
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            SetAndroidSlots(AndroidPlatformIconKind.Legacy, icon);
            SetAndroidSlots(AndroidPlatformIconKind.Round, icon);
        }

        static bool ConfigureImporter(TextureImporter importer)
        {
            bool changed = importer.textureType != TextureImporterType.Default ||
                importer.textureShape != TextureImporterShape.Texture2D ||
                !importer.sRGBTexture ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                importer.npotScale != TextureImporterNPOTScale.None ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.mipmapEnabled || importer.filterMode != FilterMode.Bilinear ||
                importer.maxTextureSize != 2048;
            if (!changed) return false;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            return true;
        }

        static void SetAndroidSlots(PlatformIconKind kind, Texture2D icon)
        {
            PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            for (int i = 0; i < slots.Length; i++) slots[i].SetTexture(icon);
            if (slots.Length > 0)
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            else
                Debug.LogWarning("No Android icon slots available for " + kind + ".");
        }
    }
}
