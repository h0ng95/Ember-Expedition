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
                int checkedFrames = 0;
                for (int frame = 0; frame < 24; frame++)
                {
                    Rect bounds;
                    if (!art.TryGetBounds("frost-boss", frame, Vector2.zero, out bounds) ||
                        bounds.height < 140f || bounds.height > 295f || bounds.width < 335f || bounds.width > 425f)
                        throw new InvalidOperationException("Every frost state must keep complete body bounds.");
                    checkedFrames++;
                }
                for (int frame = 12; frame <= 17; frame++)
                {
                    Rect pose = FrostBossFloatingArt.BoundsFor(frame, Vector2.zero, 0f);
                    if (Mathf.Abs(pose.yMax + 10f) > .01f)
                        throw new InvalidOperationException("Complete attack poses must keep the hovering baseline.");
                }
                Rect idle = FrostBossFloatingArt.BoundsFor(0, Vector2.zero, 0);
                var grounded = FrostBossFloatingArt.BoundsFor(21, Vector2.zero, 0);
                if (Mathf.Abs(idle.height - 290f) > .01f || idle.width < 345f || idle.width > 385f ||
                    grounded.height >= idle.height * .7f ||
                    Mathf.Abs(grounded.yMax) > .01f || grounded.width < idle.width * .9f)
                    throw new InvalidOperationException("Defeat must lower the whole body onto the ground without shrinking it.");
                int previous = 17;
                foreach (float age in new[] { 0f, .25f, .50f, .75f, 2f, 100f })
                {
                    int frame = FrostBossFloatingArt.DefeatFrame(age, false);
                    if (frame < previous || frame < 18 || frame > 21 || FrostBossFloatingArt.DefeatFrame(age, true) != 21)
                        throw new InvalidOperationException("Defeat must progress once and reduced motion must settle immediately.");
                    previous = frame;
                }
                for (int frame = 18; frame <= 21; frame++)
                {
                    if (FrostBossFloatingArt.SpriteIndex(frame) != frame - 14 ||
                        FrostBossFloatingArt.BoundsFor(frame, Vector2.zero, .1f) !=
                        FrostBossFloatingArt.BoundsFor(frame, Vector2.zero, 1.9f))
                        throw new InvalidOperationException("All four defeat drawings are distinct and cannot keep hovering.");
                }
                var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/" + FrostBossFloatingArt.TexturePath + ".png");
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
                Debug.Log("PASS frost art: " + checkedFrames + " combat frames; eight full-body poses, monotonic grounded defeat, no defeat bob, reduced motion, Point/1024 verified.");
                Debug.Log("Enlarged dragon: head/torso clear the HUD at three depths and three flight lifts; wing tips may pass behind it.");
            }
            finally { art.Release(); }
        }
        public static void ValidateAndBuild() { Validate(); ExpeditionDialogueValidation.Validate(); PrototypeSetup.BuildAndroid(); }
    }
}
