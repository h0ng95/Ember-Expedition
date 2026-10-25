using System;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class StartupArtSetup
    {
        public const string ImagePath = "Assets/Resources/Art/Branding/ignis-splash-v1.png";

        public static void Configure()
        {
            AssetDatabase.ImportAsset(ImagePath, ImportAssetOptions.ForceSynchronousImport);
            Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(ImagePath);
            if (background == null)
                throw new InvalidOperationException("Startup background Sprite is missing: " + ImagePath);

            // Our background covers engine startup; runtime loading draws its own status UI.
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.background = background;
            PlayerSettings.SplashScreen.backgroundPortrait = null;
            PlayerSettings.SplashScreen.backgroundColor = new Color(.025f, .04f, .075f);
            PlayerSettings.SplashScreen.blurBackgroundImage = false;
            PlayerSettings.SplashScreen.overlayOpacity = 0f;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            // Unity skips the image when the sequence is empty. A blank entry
            // displays only our background while the engine starts, with no logo.
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, null) };
        }
    }
}
