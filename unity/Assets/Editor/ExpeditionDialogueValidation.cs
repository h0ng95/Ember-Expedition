using System;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class ExpeditionDialogueValidation
    {
        static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static int checks;
        static void Check(bool condition, string why) { checks++; if (!condition) throw new Exception("Dialogue: " + why); }
        static void Set(object target, string field, object value) { target.GetType().GetField(field, Hidden).SetValue(target, value); }
        static object Get(object target, string field) { return target.GetType().GetField(field, Hidden).GetValue(target); }
        static void Call(object target, string method, params object[] args) { target.GetType().GetMethod(method, Hidden).Invoke(target, args); }

        public static void Validate()
        {
            checks = 0;
            var coreArt = Resources.Load<Texture2D>("Art/Ignis/ignis-living-core");
            Check(coreArt != null && coreArt.isReadable && coreArt.width <= 256 && coreArt.height <= 256,
                "Ignis artwork is bundled, bounded and available for regional palette swaps");
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            foreach (int wave in new[] { 1, 5, 10, 15, 20, 25, 30 })
            foreach (bool victory in new[] { false, true })
            foreach (bool skip in new[] { false, true })
            {
                var run = ExpeditionRun.New(db, 59059);
                while (run.Wave < wave) { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
                var battle = run.BeginBattle();
                foreach (var unit in battle.Units)
                    if (unit.Team == (victory ? CatalogTeam.Enemy : CatalogTeam.Ally))
                        typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(unit, 0f, null);
                battle.Step(1f / 60f);
                Check(battle.Ended, "fixture reaches a genuine end state");
                var go = new GameObject("Dialogue settlement QA");
                try
                {
                    var lab = go.AddComponent<CatalogCombatLab>();
                    int settled = 0, displayed = 0;
                    lab.OpenExpeditionBattle(battle, wave, 30, "QA", won => {
                        settled++; run.ResolveBattle(won);
                    }, () => {}, () => displayed++);
                    Set(lab, "settling", true); Set(lab, "settlementVictory", victory);
                    Call(lab, "AdvanceSettlement", .6f);
                    string saved = JsonUtility.ToJson(run.Capture());
                    bool scene = victory && wave % 5 == 0;
                    Check(settled == 1 && (bool)Get(lab, "dialogueActive") == scene,
                        "only won bosses hold a scene after resolving exactly once");
                    Check(displayed == (scene ? 0 : 1), "result screen waits only for the dialogue");
                    if (scene)
                    {
                        var restored = ExpeditionRun.Restore(db, JsonUtility.FromJson<ExpeditionRunSave>(saved));
                        Check(restored.LastVictory && restored.Victories == wave && restored.Phase == ExpeditionPhase.Result,
                            "app restart during dialogue restores the already-paid result");
                        float beforeTime = battle.Time;
                        Call(lab, "AdvanceDialogue", 1.1f);
                        Check(battle.Time == beforeTime, "dialogue never advances combat");
                        if (skip) Call(lab, "FinishDialogue");
                        else for (int i = 0; i < ExpeditionDialogue.ForBoss(wave).Count; i++)
                        { Set(lab, "dialogueAge", 10f); Call(lab, "NextDialogue"); }
                        Call(lab, "FinishDialogue");
                        Check(displayed == 1 && settled == 1 && JsonUtility.ToJson(run.Capture()) == saved,
                            "skip/finish cannot duplicate rewards or mutate the saved result");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            var font = Resources.Load<Font>("Fonts/Galmuri11");
            Check(font != null, "bundled Korean pixel font is available offline");
            {
                var style = new GUIStyle { font = font, fontSize = 20, wordWrap = true };
                foreach (int wave in new[] { 5, 10, 15, 20, 25, 30 })
                foreach (var line in ExpeditionDialogue.ForBoss(wave))
                {
                    font.RequestCharactersInTexture(line.Text, 20);
                    bool allGlyphs = true;
                    foreach (char glyph in line.Text) if (!char.IsWhiteSpace(glyph) && !font.HasCharacter(glyph)) allGlyphs = false;
                    Check(allGlyphs, "pixel font covers every Korean dialogue glyph");
                    string wrapped = CatalogCombatLab.WrapDialogueText(style, line.Text, 328);
                    float height = Mathf.Ceil(Mathf.Max(76, style.CalcHeight(new GUIContent(wrapped), 328) + 36) / 4) * 4;
                    Check(height <= 210, "speech stays short enough for a unit bubble");
                    foreach (float x in new[] { 80f, 300f, 640f, 1000f, 1200f })
                    foreach (float y in new[] { 180f, 330f, 460f })
                    {
                        Rect rect = CatalogCombatLab.DialogueBubbleRect(new Vector2(x, y), height);
                        Check(rect.x >= 24 && rect.xMax <= 1256 && rect.y >= 88 && rect.yMax <= 610,
                            "bubble stays clear of header/footer and screen edges");
                    }
                }
            }
            CombatSettlementValidation.Validate();
            Debug.Log("PASS battlefield dialogue: " + checks + " checks, 28 victory/loss/skip scenarios, save-before-dialogue and bubble layout.");
        }
    }
}
