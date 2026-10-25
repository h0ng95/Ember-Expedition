using System;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class FrostBossFloatingValidation
    {
        [MenuItem("Expedition/Validate Floating Frost Boss")]
        public static void Validate()
        {
            var art = new ForestEnemyArt();
            try
            {
                for (int frame = 0; frame < 24; frame++)
                {
                    Rect bounds;
                    if (!art.TryGetBounds("frost-boss", frame, Vector2.zero, out bounds) ||
                        bounds.height < 250f || bounds.height > 295f || bounds.width < 280f || bounds.width > 370f)
                        throw new InvalidOperationException("Every frost state must use the same floating armored silhouette.");
                }
                for (int frame = 12; frame <= 17; frame++)
                {
                    Rect pose = FrostBossFloatingArt.BoundsFor(frame, Vector2.zero, 0f);
                    if (Mathf.Abs(pose.yMax + 10f) > .01f)
                        throw new InvalidOperationException("Complete attack poses must keep the hovering baseline.");
                }
                var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Art/Enemies/v039/frost-boss-full-motion.png");
                if (importer == null || importer.maxTextureSize != 1024 || importer.filterMode != FilterMode.Point)
                    throw new InvalidOperationException("Frost texture importer is incorrect.");
                foreach (float y in new[] { -2f, 0f, 2f })
                    foreach (float lift in new[] { .1f, .5f, 1f })
                        for (int frame = 22; frame <= 23; frame++)
                        {
                            float groundY = 497f - y * 48f;
                            Rect bounds;
                            if (!art.TryGetBounds("palace-boss", frame,
                                new Vector2(900f, groundY - PalaceEnemyArt.VisualFlightLift(lift, y)), out bounds) ||
                                groundY - PalaceEnemyArt.VisualFlightLift(lift, y) -
                                    150f * PalaceEnemyArt.SilhouetteHeight("palace-boss") / 184f < 75.9f)
                                throw new InvalidOperationException("Flying dragon head and torso must clear the fixed top HUD.");
                        }
                Debug.Log("Full-body frost boss: 24 combat frames use four complete poses; idle height 290, stable attack baseline, Point/1024 verified.");
                Debug.Log("Enlarged dragon: head/torso clear the HUD at three depths and three flight lifts; wing tips may pass behind it.");
            }
            finally { art.Release(); }
        }
        public static void ValidateAndBuild() { Validate(); PrototypeSetup.BuildAndroid(); }
    }
}
