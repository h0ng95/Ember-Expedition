using System;
using System.Reflection;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class BattleReadabilityValidation
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Battle readability: " + message);
        }

        [MenuItem("Expedition/Validate Battle Readability")]
        public static void Validate()
        {
            Rect boss = CatalogCombatLab.HudRules.BossPanel;
            Check(boss.x >= 0f && boss.y >= 0f && boss.xMax <= 1280f && boss.yMax <= 76f,
                "boss status stays in the fixed safe header");
            Check(!boss.Overlaps(new Rect(26, 17, 302, 40)) &&
                !boss.Overlaps(new Rect(758, 14, 135, 49)) &&
                !boss.Overlaps(new Rect(904, 14, 125, 49)) &&
                !boss.Overlaps(new Rect(1043, 14, 211, 49)),
                "boss panel leaves encounter, timer, speed and pause controls clear");

            var kind = CatalogCombatLab.BattleFloatKind.Burn;
            Check(CatalogCombatLab.HudRules.MergeDot(kind, "unit", .18f, kind, "unit"),
                "nearby ticks for one target and one condition combine");
            Check(!CatalogCombatLab.HudRules.MergeDot(kind, "unit", .23f, kind, "unit") &&
                !CatalogCombatLab.HudRules.MergeDot(kind, "unit", .1f,
                    CatalogCombatLab.BattleFloatKind.Poison, "unit") &&
                !CatalogCombatLab.HudRules.MergeDot(kind, "unit", .1f, kind, "other") &&
                !CatalogCombatLab.HudRules.MergeDot(CatalogCombatLab.BattleFloatKind.Direct, "unit", .1f,
                    CatalogCombatLab.BattleFloatKind.Direct, "unit"),
                "different ticks, targets, and direct hits remain separate");
            Check(CatalogCombatLab.HudRules.DamageText(kind, 2.25f + 3.5f) == "화상 −5.75" &&
                CatalogCombatLab.HudRules.DamageText(CatalogCombatLab.BattleFloatKind.Poison, 6f) == "중독 −6" &&
                CatalogCombatLab.HudRules.DamageText(CatalogCombatLab.BattleFloatKind.Direct, 4f) == "−4",
                "display uses the accumulated raw event amount and identifies damage source");
            int occupied = 0;
            for (int i = 0; i < 6; i++)
            {
                int lane = CatalogCombatLab.HudRules.FirstFreeDirectLane(occupied);
                Check(lane == i, "simultaneous direct hits take distinct lanes");
                Rect candidate = new Rect(CatalogCombatLab.HudRules.DirectLane(lane).x - 46f,
                    CatalogCombatLab.HudRules.DirectLane(lane).y, 92f, 28f);
                for (int prior = 0; prior < i; prior++)
                {
                    Vector2 offset = CatalogCombatLab.HudRules.DirectLane(prior);
                    Check(!candidate.Overlaps(new Rect(offset.x - 46f, offset.y, 92f, 28f)),
                        "simultaneous direct-hit labels have separate footprints");
                }
                occupied |= 1 << lane;
            }
            foreach (float baseY in new[] { 116f, 622f })
            {
                float shift = CatalogCombatLab.HudRules.DirectLaneShiftY(baseY, 116f, 622f);
                for (int i = 0; i < 6; i++)
                {
                    Vector2 lane = CatalogCombatLab.HudRules.DirectLane(i);
                    Rect rect = new Rect(lane.x - 46f, baseY + lane.y + shift, 92f, 28f);
                    Check(rect.yMin >= 116f && rect.yMin <= 622f,
                        "all direct-hit lanes stay below boss HUD and above footer");
                    for (int prior = 0; prior < i; prior++)
                    {
                        Vector2 other = CatalogCombatLab.HudRules.DirectLane(prior);
                        Check(!rect.Overlaps(new Rect(other.x - 46f,
                            baseY + other.y + shift, 92f, 28f)),
                            "top and bottom translations preserve six distinct lanes");
                    }
                }
            }
            Type effects = typeof(CatalogCombatLab).Assembly.GetType("Expedition.Presentation.CatalogBattleEffects");
            MethodInfo select = effects == null ? null : effects.GetMethod("BossEffectKind",
                BindingFlags.NonPublic | BindingFlags.Static);
            Check(select != null, "boss impacts have a visual mapping");
            string[] ability = { "BOSS_CHARGE", "GOLEM_SLAM", "GOLEM_AFTERSHOCK",
                "FURNACE_BLAST", "FURNACE_EMBER", "FROST_NOVA", "GARDEN_BRIARS",
                "DRACONIC_BREATH", "DRACONIC_BOMB", "DRACONIC_LAND" };
            string[] visual = { "ChargeDust", "GolemCrack", "GolemCrack", "FurnacePillar",
                "FurnaceEmber", "FrostSpire", "RootBurst", "Breath", "BombBurst", "DrakeLand" };
            for (int i = 0; i < ability.Length; i++)
                Check(select.Invoke(null, new object[] { ability[i] }).ToString() == visual[i],
                    ability[i] + " uses its own impact visual");
            Debug.Log("Battle readability validated: boss HUD, damage lanes and ten boss impact mappings.");
        }
    }
}
