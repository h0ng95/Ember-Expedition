using System;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class DrakePatternPresentationValidation
    {
        static int checks;
        static void Check(bool ok, string why)
        { checks++; if (!ok) throw new Exception("Drake pattern presentation: " + why); }

        public static void ValidateAndBuild() { Validate(); PrototypeSetup.BuildAndroid(); }
        public static void Validate()
        {
            checks = 0;
            foreach (string path in new[] { DrakeAttackPresentation.PillarsPath, DrakeAttackPresentation.FlightPath })
            {
                string asset = "Assets/Resources/" + path + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(asset);
                importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true; importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                var tex = Resources.Load<Texture2D>(path);
                Check(tex != null && tex.width == 1774 && tex.height == 887, "full-resolution sprite atlas " + path);
            }
            BattleReadabilityValidation.Validate();
            PalaceEnemyArtValidation.Validate();
            DragonBreathValidation.Validate();
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var owner = new GameObject("Drake pattern QA");
            Type type = typeof(CatalogCombatLab).Assembly.GetType("Expedition.Presentation.CatalogBattleEffects");
            object effects = Activator.CreateInstance(type, new object[] { owner });
            try
            {
                var battle = new CatalogBattle(db);
                var boss = battle.AddEnemy("palace-boss", 100000, 1, 26, 1.8f, 3.2f, .48f,
                    true, 2.2f, 0, CatalogEnemyRole.CrownDrake, true);
                battle.AddUnit("front", CatalogTeam.Ally, "F-KN", "E-KN-G", null, -1, 0);
                battle.AddUnit("rear", CatalogTeam.Ally, "F-PR", "E-PR-H", null, -4, 1.2f);
                bool ground = false, air = false, landed = false;
                Vector2 warnedTarget = Vector2.zero;
                float airAt = -1;
                battle.Event += e => {
                    type.GetMethod("Observe").Invoke(effects, new object[] { e, battle });
                    if (e.Kind == CatalogEventKind.BossTelegraph && e.AbilityId == "DRACONIC_BOMB")
                        warnedTarget = new Vector2(boss.SlamTargetX, boss.SlamTargetY);
                    if (e.Kind != CatalogEventKind.BossImpact) return;
                    if (e.AbilityId == "DRACONIC_BREATH")
                    {
                        ground = true;
                        object[] args = { boss.Id, 0f };
                        Check(!(bool)type.GetMethod("TryGetBreathProgress").Invoke(effects, args), "ground hit must not emit mouth breath");
                    }
                    if (e.AbilityId == "DRACONIC_BOMB")
                    {
                        air = true; airAt = battle.Time;
                        Check(Vector2.Distance(warnedTarget, new Vector2(boss.SlamTargetX, boss.SlamTargetY)) < .001f,
                            "air breath hits the locked circle, not a moving ally");
                        Check(boss.IsAirborne, "breath starts in flight");
                        var list = (System.Collections.IList)type.GetField("effects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effects);
                        object effect = list[list.Count - 1];
                        Vector2 target = (Vector2)effect.GetType().GetField("EndPoint").GetValue(effect);
                        Check(Vector2.Distance(target, warnedTarget) < .001f, "visual end stays at warned center before landing resets aim");
                    }
                    if (e.AbilityId == "DRACONIC_LAND") landed = true;
                };
                for (int i = 0; i < 1500 && !landed; i++)
                {
                    type.GetMethod("Tick").Invoke(effects, new object[] { 1f / 60f, 1f / 60f, battle });
                    battle.Step(1f / 60f);
                    if (airAt >= 0 && battle.Time - airAt < PalaceEnemyArt.BreathVisualDuration)
                        Check(Mathf.Abs(DrakeAttackPresentation.FlightLift(boss) - 1f) < .001f,
                            "full height held while breathing");
                }
                Check(ground && air && landed, "complete ground/air/landing cycle");
                Check(DrakeAttackPresentation.FlightLift(boss) == 0f, "landing restores grounded height");
                var art = new ForestEnemyArt();
                try
                {
                    foreach (int frame in new[] { 24, 25 })
                        foreach (Vector2 foot in new[] { new Vector2(840, 280), new Vector2(920, 390), new Vector2(1100, 500) })
                        {
                            Rect bounds; Vector2 jaw;
                            Check(art.TryGetBounds("palace-boss", frame, foot, out bounds), "flight pose bounds");
                            Check(PalaceEnemyArt.TryGetBreathMouth(frame, bounds, out jaw) && bounds.Contains(jaw), "emission on open jaw");
                            Check(Mathf.Abs(bounds.width / DrakeAttackPresentation.FlightSource(frame).width -
                                DrakeAttackPresentation.FlightScale) < .001f, "same body scale for both wing poses");
                        }
                }
                finally { art.Release(); }
                foreach (Vector2 end in new[] { new Vector2(-3.3f, 0), new Vector2(-3, 1.5f), new Vector2(-3, -1.5f) })
                {
                    Vector2 start = new Vector2(2.2f, 0), last = start;
                    for (int i = 0; i < DrakeAttackPresentation.PillarCount; i++)
                    {
                        Vector2 point = DrakeAttackPresentation.PillarPoint(start, end, i);
                        Check(Vector2.Distance(last, point) <= 1.3f, "pillar bases cover the capsule without holes");
                        Check(DrakeAttackPresentation.PillarProgress(0, i) == 0, "all bases ignite on damage frame");
                        last = point;
                    }
                    Check(Vector2.Distance(last, end) < .001f, "last pillar reaches capsule end cap");
                }
                Check(DrakeAttackPresentation.BasicFrame == 13, "basic uses a fire-free frame");
                Debug.Log("PASS drake pattern presentation: " + checks + " checks; ground/air mapping, locked aim, full-size flight, shared impact timing, pillar coverage, fire-free basic; Core unchanged.");
            }
            finally { ((IDisposable)effects).Dispose(); UnityEngine.Object.DestroyImmediate(owner); }
        }
    }
}
