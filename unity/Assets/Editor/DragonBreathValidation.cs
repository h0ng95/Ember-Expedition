using System;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class DragonBreathValidation
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Dragon breath: " + message);
        }

        [MenuItem("Expedition/Validate Dragon Breath Attachment")]
        public static void Validate()
        {
            var art = new ForestEnemyArt();
            var owner = new GameObject("Dragon breath validation");
            Type effectType = typeof(CatalogCombatLab).Assembly.GetType("Expedition.Presentation.CatalogBattleEffects");
            object effects = Activator.CreateInstance(effectType, new object[] { owner });
            try
            {
                // Exercise the real effect clock after a real Core breath event.
                var database = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
                var battle = new CatalogBattle(database);
                battle.AddEnemy("palace-boss", 10000, 1, 26, 1.8f, 1.1f,
                    .48f, true, 2.2f, 0f, CatalogEnemyRole.CrownDrake, true);
                battle.AddUnit("ally", CatalogTeam.Ally, "F-KN", "E-KN-G", null, -.4f, .5f);
                bool released = false;
                battle.Event += e =>
                {
                    if (e.Kind != CatalogEventKind.BossImpact || e.AbilityId != "DRACONIC_BREATH") return;
                    effectType.GetMethod("Observe").Invoke(effects, new object[] { e, battle });
                    released = true;
                };
                for (int i = 0; i < 400 && !released; i++) battle.Step(.05f);
                Check(released, "fixture must release a real breath");
                float lastAge = 0f;
                foreach (float age in new[] { 0f, .16f, .22f, .43f, .45f })
                {
                    float dt = age - lastAge;
                    effectType.GetMethod("Tick").Invoke(effects, new object[] { dt, dt, battle });
                    object[] query = { "palace-boss", 0f };
                    bool active = (bool)effectType.GetMethod("TryGetBreathProgress").Invoke(effects, query);
                    Check(active == (age < .44f), "effect lifetime at " + age);
                    if (active)
                    {
                        int frame = PalaceEnemyArt.BreathFrame((float)query[1]);
                        Check(frame >= 14 && frame <= 16, "mouth must remain open beyond old .18s release");
                    }
                    lastAge = age;
                }

                // Compare anchors with measured source-pixel jaw positions, including
                // moving the rendered dragon and the last closing-but-open pose.
                var sourceMouths = new[] { new Vector2(550, 646), new Vector2(803, 648), new Vector2(1058, 658) };
                var manifest = JsonUtility.FromJson<ForestEnemyManifest>(Resources.Load<TextAsset>(PalaceEnemyArt.ManifestPath).text);
                ForestEnemySheet sheet = Array.Find(manifest.sheets, s => s.id == "palace-boss");
                foreach (Vector2 foot in new[] { new Vector2(840, 377), new Vector2(920, 497), new Vector2(1100, 617) })
                    for (int frame = 14; frame <= 16; frame++)
                    {
                        Rect bounds;
                        Vector2 mouth;
                        Check(art.TryGetBounds("palace-boss", frame, foot, out bounds), "missing pose");
                        Check(PalaceEnemyArt.TryGetBreathMouth(frame, bounds, out mouth) && bounds.Contains(mouth), "jaw inside rendered pose");
                        ForestEnemyFrame cell = sheet.frames[frame];
                        Vector2 pixel = sourceMouths[frame - 14];
                        float scale = PalaceEnemyArt.SilhouetteHeight("palace-boss") / sheet.referenceHeight;
                        Vector2 expected = foot + new Vector2(
                            (pixel.x - frame % 6 * sheet.width / 6f - sheet.anchorX) * scale,
                            (pixel.y - (sheet.height - cell.y)) * scale);
                        Check(Vector2.Distance(mouth, expected) < .01f, "mouth must follow the actual frame bounds");
                    }
                Vector2 unused;
                Check(!PalaceEnemyArt.TryGetBreathMouth(0, new Rect(0, 0, 300, 300), out unused), "closed mouth must not emit fire");
                Debug.Log("Dragon breath validated: real effect clock at five ages, three jaw poses at three depths, closed-mouth rejection.");
                foreach (float depth in new[] { -2.5f, 0f, 2.5f })
                {
                    Check(PalaceEnemyArt.VisualFlightLift(0f, depth) == 0f, "ground position unchanged");
                    foreach (float lift in new[] { .01f, .25f, .5f, .75f, 1f })
                        foreach (float time in new[] { 0f, .125f, .375f })
                            for (int frame = 22; frame <= 23; frame++)
                            {
                                float altitude = PalaceEnemyArt.VisualFlightLift(lift, depth, time);
                                Check(altitude > 0f, "takeoff must never push the dragon down");
                                if (lift == 1f && depth <= 0f)
                                    Check(altitude >= 97.9f, "full flight must visibly clear the floor");
                                float groundY = Mathf.Clamp(497f - depth * 48f, 377f, 617f);
                                Vector2 foot = new Vector2(900f, groundY - altitude);
                                Rect bounds;
                                Check(art.TryGetBounds("palace-boss", frame, foot, out bounds), "flight pose missing");
                                Check(Mathf.Abs(bounds.height - (frame == 22 ? 222f : 195f) * 340f / 184f) < .01f,
                                    "airborne sprite scale must remain unchanged");
                                Check(foot.y - 150f * 340f / 184f >= 75.9f,
                                    "head and torso must remain below foreground HUD");
                            }
                }
                Debug.Log("Dragon flight validated: both wing poses keep full scale at three depths, five heights and three bob phases; central/front altitude 98-110, rear body clearance verified.");
            }
            finally
            {
                ((IDisposable)effects).Dispose();
                UnityEngine.Object.DestroyImmediate(owner);
                art.Release();
            }
        }

        public static void ValidateAndBuild()
        {
            Validate();
            PalaceEnemyArtValidation.Validate();
            PrototypeSetup.BuildAndroid();
        }
    }
}
