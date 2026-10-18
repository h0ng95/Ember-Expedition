using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Expedition.EditorTools
{
    public static class PrototypeSetup
    {
        public const string ScenePath = "Assets/Scenes/CombatPrototype.unity";

        [InitializeOnLoadMethod]
        private static void FirstOpen()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists(ScenePath))
                    Configure();
            };
        }

        [MenuItem("Expedition/Prepare Combat Prototype")]
        public static void Configure()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "ExpeditionPrototype";
            PlayerSettings.productName = "Ember Expedition Prototype";
            PlayerSettings.bundleVersion = "0.0.1";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.expeditionprototype.combatdemo");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            // Runtime-created components and the built-in IMGUI skin must survive prototype builds.
            // An incremental stripped build omitted MonoScript (class ID 115) on the test device.
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            int originalQuality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.vSyncCount = 0;
                QualitySettings.antiAliasing = 0;
                QualitySettings.shadows = ShadowQuality.Disable;
            }
            QualitySettings.SetQualityLevel(originalQuality, false);

            // Ensure Shader.Find works in a player even if no scene material references the shader yet.
            var shader = Shader.Find("Expedition/HitFlash");
            var graphicsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (shader != null && graphicsAssets.Length > 0)
            {
                var settings = new SerializedObject(graphicsAssets[0]);
                var shaders = settings.FindProperty("m_AlwaysIncludedShaders");
                if (shaders == null || !shaders.isArray)
                    throw new InvalidOperationException("Cannot configure Always Included Shaders. Check Graphics Settings before building.");
                bool found = false;
                for (int i = 0; i < shaders.arraySize; i++)
                    found |= shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader;
                if (!found)
                {
                    int index = shaders.arraySize;
                    shaders.InsertArrayElementAtIndex(index);
                    shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                    settings.ApplyModifiedProperties();
                }
            }
            else
                Debug.LogWarning("HitFlash shader or Graphics Settings not ready. Run Prepare Combat Prototype again after import.");

            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Expedition prototype ready. Open CombatPrototype and press Play. Android device performance is not yet verified.");
        }

        [MenuItem("Expedition/Build Android Test APK")]
        public static void BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Install Android Build Support, SDK & NDK Tools, and OpenJDK through Unity Hub first.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Switch the active platform to Android in Build Profiles first, then run this menu again.");
            Configure();
            Directory.CreateDirectory("Builds/Android");
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = BuildTarget.Android,
                locationPathName = "Builds/Android/expedition-combat-prototype.apk",
                options = BuildOptions.Development | BuildOptions.CleanBuildCache
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Android build failed: " + report.summary.result);
            Debug.Log("Test APK created: Builds/Android/expedition-combat-prototype.apk");
        }
    }

    public sealed class PrototypeArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Art/", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/", StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
        }
    }
}
