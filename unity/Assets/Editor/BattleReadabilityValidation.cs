using System;
using System.IO;
using System.Reflection;
using Expedition.Core;
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
            Check(boss.x >= 0f && boss.y >= 0f && boss.xMax <= 1280f && boss.yMax <= 92f,
                "boss status stays in the fixed safe header");
            Check(!boss.Overlaps(CatalogCombatLab.HudRules.Encounter(true)) &&
                !boss.Overlaps(CatalogCombatLab.HudRules.Clock) &&
                !boss.Overlaps(new Rect(928, 14, 108, 49)) &&
                !boss.Overlaps(new Rect(1050, 14, 204, 49)),
                "boss panel leaves encounter, timer, speed and pause controls clear");
            ValidateHeaderText();

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
            Check(CatalogCombatLab.HudRules.DamageText(kind, 2.25f + 3.5f) == "화상 −6" &&
                CatalogCombatLab.HudRules.DamageText(CatalogCombatLab.BattleFloatKind.Poison, 6f) == "중독 −6" &&
                CatalogCombatLab.HudRules.DamageText(CatalogCombatLab.BattleFloatKind.Direct, 4f) == "−4",
                "display rounds only the accumulated number and identifies damage source");
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
                "FurnaceEmber", "FrostSpire", "RootBurst", "PillarLine", "Breath", "DrakeLand" };
            for (int i = 0; i < ability.Length; i++)
                Check(select.Invoke(null, new object[] { ability[i] }).ToString() == visual[i],
                    ability[i] + " uses its own impact visual");
            Debug.Log("Battle readability validated: boss HUD, damage lanes and ten boss impact mappings.");
        }

        static void Fits(GUIStyle style, Rect rect, string text)
        {
            var content = new GUIContent(text);
            Check(style.CalcSize(content).x <= rect.width && style.CalcHeight(content, rect.width) <= rect.height,
                "header text clipped: " + text + " needs " + style.CalcSize(content).x + "x" +
                style.CalcHeight(content, rect.width) + " in " + rect.width + "x" + rect.height);
        }

        static void ValidateHeaderText()
        {
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 21);
            var go = new GameObject("Boss header validation"); go.SetActive(false);
            var lab = go.AddComponent<CatalogCombatLab>();
            try
            {
                var name = CatalogCombatLab.HudRules.HeaderText(font, 21, TextAnchor.MiddleLeft, true);
                var health = CatalogCombatLab.HudRules.HeaderText(font, 15, TextAnchor.MiddleCenter);
                var badge = CatalogCombatLab.HudRules.HeaderText(font, 12, TextAnchor.MiddleCenter, true);
                var timer = CatalogCombatLab.HudRules.HeaderText(font, 11, TextAnchor.MiddleLeft, true);
                MethodInfo unitName = typeof(CatalogCombatLab).GetMethod("UnitName", BindingFlags.Instance | BindingFlags.NonPublic);
                var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
                var run = ExpeditionRun.New(db, 43030);
                var hudProgress = ExpeditionProgress.New(); var hudJournal = ExpeditionJournal.New();
                Directory.CreateDirectory("Logs/v043-fixtures");
                Directory.CreateDirectory("Logs/v044-fixtures");
                int bosses = 0;
                for (int wave = 1; wave <= 30; wave++)
                {
                    var encounterStyle = CatalogCombatLab.HudRules.HeaderText(font, wave % 5 == 0 ? 21 : 23, TextAnchor.MiddleLeft, true);
                    Fits(encounterStyle, CatalogCombatLab.HudRules.Encounter(wave % 5 == 0), CatalogCombatLab.HudRules.EncounterLabel(run.EncounterName,wave,30,wave%5==0));
                    hudProgress.Observe(run); hudJournal.Observe(run);
                    var snapshot = new ExpeditionAccountSave { Version = 1, Run = run.Capture(), Progress = hudProgress.Capture(),
                        Journal = hudJournal.Capture(), TutorialMask = 3, LessonMask = 7 };
                    snapshot.Validate(db);
                    File.WriteAllText("Logs/v044-fixtures/hud-" + wave + ".json", JsonUtility.ToJson(snapshot));
                    if (wave % 5 == 0)
                        File.WriteAllText("Logs/v043-fixtures/hud-" + wave + ".json", JsonUtility.ToJson(run.Capture()));
                    CatalogBattle battle = run.BeginBattle();
                    foreach (CatalogBattleUnit unit in battle.Units)
                    {
                        if (!unit.IsBoss) continue;
                        bosses++;
                        Fits(name, CatalogCombatLab.HudRules.BossName, (string)unitName.Invoke(lab, new object[] { unit }));
                        Fits(health, CatalogCombatLab.HudRules.BossHealth, unit.MaxHp.ToString("0") + " / " + unit.MaxHp.ToString("0"));
                    }
                    run.ResolveBattle(true); if (wave < 30) run.Continue();
                }
                Check(bosses == 6, "all six actual boss headers were measured");
                Fits(CatalogCombatLab.HudRules.HeaderText(font, 17, TextAnchor.MiddleLeft), CatalogCombatLab.HudRules.Clock, "75.0 / 75초");
                for (int allies = 0; allies <= 5; allies++)
                    for (int enemies = 0; enemies <= 12; enemies++)
                        Fits(CatalogCombatLab.HudRules.HeaderText(font, 21, TextAnchor.MiddleRight, true), CatalogCombatLab.HudRules.UnitCount,
                            "아군 " + allies + "  ·  적 " + enemies);
                Check(!CatalogCombatLab.HudRules.Notice.Overlaps(CatalogCombatLab.HudRules.UnitCount) &&
                    CatalogCombatLab.HudRules.UnitCount.yMax <= 712, "footer regions overlap or touch bottom edge");
                var notice = CatalogCombatLab.HudRules.HeaderText(font, 17, TextAnchor.MiddleLeft);
                foreach (CatalogForm form in db.All) Fits(notice, CatalogCombatLab.HudRules.Notice, form.Name + " · 기술 발동");
                // Measure current Korean announcement literals, including each boss's warning/enrage suffix.
                string source = File.ReadAllText("Assets/Scripts/Presentation/CatalogCombatLab.cs");
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(source, "\"([^\"\\r\\n]*[가-힣][^\"\\r\\n]*)\""))
                    if (match.Value.Contains("·") || match.Value.Contains("전투가 끝나면"))
                        Fits(notice, CatalogCombatLab.HudRules.Notice, match.Groups[1].Value);
                Fits(health, CatalogCombatLab.HudRules.BossHealth, "99999 / 99999");
                for (int i = 0; i < 4; i++)
                {
                    Rect chip = CatalogCombatLab.HudRules.BossChip(i);
                    Check(chip.yMin > CatalogCombatLab.HudRules.BossHealth.yMax &&
                        chip.xMax <= CatalogCombatLab.HudRules.BossPanel.xMax - 10 &&
                        chip.yMax <= CatalogCombatLab.HudRules.BossPanel.yMax,
                        "four status chips stay separate from name, health and border");
                    if (i > 0) Check(!chip.Overlaps(CatalogCombatLab.HudRules.BossChip(i - 1)), "status chips overlap");
                    Fits(badge, chip, "분노");
                    Fits(timer, new Rect(chip.x + 22, chip.y, chip.width - 22, chip.height), "10.0");
                }
                Debug.Log("Battle HUD validated: 30 encounter titles, clock, 78 ally/enemy counts, notices, 6 boss names/HP, rage and 4 status slots.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(font); }
        }
    }
}
