using System;
using System.Collections.Generic;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class BossActionValidation
    {
        static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Boss action: " + message); }
        [MenuItem("Expedition/Validate Boss Actions")]
        public static void Validate()
        {
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var roles = new[] { CatalogEnemyRole.Beast, CatalogEnemyRole.Golem, CatalogEnemyRole.Furnace,
                CatalogEnemyRole.FrostWarden, CatalogEnemyRole.GardenLord, CatalogEnemyRole.CrownDrake };
            int impacts = 0;
            foreach (var role in roles)
            {
                var battle = new CatalogBattle(db, null, null, CatalogBalance.CurrentRules);
                for (int i = 0; i < 3; i++)
                {
                    var ally = battle.AddUnit("ally" + i, CatalogTeam.Ally, "B-KN", null, null, -1f-i*.7f, i-1f);
                    Set(ally, "Hp", 10000f); Set(ally, "MaxHp", 10000f);
                }
                var boss = battle.AddEnemy("boss", 100000, 5, 10, 1.8f, 1.4f, .55f, true, 1.6f, 0, role, true);
                Set(boss, "Hp", 35000f); // Enrage activates through normal damage, exercising follow-ups.
                string ability = null; float impactAt = -100; int variant = 0;
                var seen = new HashSet<int>();
                battle.Event += e => {
                    if (e.SourceId != boss.Id || e.Kind != CatalogEventKind.BossImpact) return;
                    ability = e.AbilityId; impactAt = battle.Time; impacts++;
                    variant = BossActionPresentation.ReleaseVariant(boss);
                };
                for (int tick = 0; tick < 1200; tick++)
                {
                    battle.Step(1f/60f);
                    float age = battle.Time - impactAt, remaining = Mathf.Max(0, BossActionPresentation.ReleaseDuration(ability) - age);
                    int frame = BossActionPresentation.Frame(boss, 0, remaining, age, ability, variant);
                    seen.Add(frame);
                    if (remaining > 0 && age < .2f && ability == "FROST_NOVA")
                        Check(frame == (variant == 0 ? 25 : 27), "a new frost warning must not erase the previous impact");
                    if (remaining > 0 && age < .2f && ability == "GARDEN_BRIARS")
                        Check(frame == (variant == 0 ? 25 : 27), "garden final strike uses its own whole-body pose");
                    if (role == CatalogEnemyRole.Furnace && remaining == 0 && boss.SlamWarningRemaining > 0)
                        Check(frame == (boss.SlamIsAftershock ? 13 : 22), "furnace opens the vent at impact, not during warning");
                }
                if (role == CatalogEnemyRole.FrostWarden || role == CatalogEnemyRole.GardenLord)
                    foreach (int frame in new[] {24,25,26,27}) Check(seen.Contains(frame), role + " missing cast " + frame);
                if (role == CatalogEnemyRole.Furnace) Check(seen.Contains(23) && seen.Contains(15), "furnace vent and ground aftershock differ");
                Set(boss, "Hp", 0f);
                Check(BossActionPresentation.Frame(boss, 19, 1, 0, ability, variant) == 19, "death takes priority over pending releases");
            }
            foreach (string id in new[] {"frost-boss","garden-boss"}) for (int frame = 24; frame < 28; frame++)
            {
                Rect bounds = BossCastArt.BoundsFor(id, frame, new Vector2(900,497));
                Check(bounds.width > 300 && bounds.width < 520 && bounds.height > 240 && bounds.height < 390,
                    "whole body scale must remain stable; raised hands may enlarge the envelope");
                Check(Mathf.Abs(bounds.yMax - (id == "frost-boss" ? 487 : 497)) < .01f, "root/skirt baseline is fixed");
            }
            foreach (string path in new[] { BossCastArt.FrostPath, BossCastArt.GardenPath })
            {
                var importer = AssetImporter.GetAtPath("Assets/Resources/" + path + ".png") as TextureImporter;
                Check(Resources.Load<Texture2D>(path) != null && importer != null &&
                    importer.filterMode == FilterMode.Point && importer.maxTextureSize == 1024, "cast asset import");
            }
            foreach (float age in new[] { .32f, 1f, 4f, 100f })
                Check(BossActionPresentation.GardenDefeatFrame(age,false) == 19, "garden stays bowed throughout story");
            Check(BossActionPresentation.GardenDefeatFrame(0,true)==19, "reduced motion settles immediately");
            Debug.Log("PASS boss actions: six boss simulations, " + impacts + " impacts; chained release priority, all eight cast poses, grounded garden ending, body bounds and Point/1024 imports.");
        }
        static void Set(CatalogBattleUnit unit, string property, float value)
        { typeof(CatalogBattleUnit).GetProperty(property).SetValue(unit,value,null); }
        public static void ValidateAndBuild()
        {
            Validate(); FrostBossFloatingValidation.Validate(); BossHitReactionValidation.Validate();
            ExpeditionDialogueValidation.Validate();
            PrototypeSetup.BuildAndroid();
        }
    }
}
