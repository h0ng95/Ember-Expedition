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
            var castleArt = Resources.Load<Texture2D>(CatalogCombatLab.CastleStoryArtPath);
            Check(castleArt != null && castleArt.width <= 1024 && castleArt.height <= 1024 &&
                castleArt.filterMode == FilterMode.Point, "castle props are bundled and mobile-bounded with point filtering");
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
                        if (wave >= 5)
                        {
                            string scenePrefix = wave == 5 ? "forestStory" : wave == 10 ? "castleStory" : "regionalStory";
                            float duration = wave == 5 ? CatalogCombatLab.ForestStoryDuration : wave == 10 ? CatalogCombatLab.CastleStoryDuration : CatalogCombatLab.RegionalStoryDuration;
                            Check((bool)Get(lab, scenePrefix + "Active") && (float)Get(lab, "dialogueAge") == 0,
                                "observation precedes dialogue instead of hiding the action beneath a bubble");
                            int unitCount = battle.Units.Count;
                            Call(lab, "NextDialogue");
                            Check((int)Get(lab, "dialogueIndex") == 0 &&
                                (float)Get(lab, scenePrefix + "Age") >= duration &&
                                battle.Units.Count == unitCount && battle.Time == beforeTime,
                                "skip reveal reaches its complete visual state without skipping speech or spawning a combatant");
                        }
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
            ValidateForestPreferences(db);
            ValidateCastlePreferences(db);
            SeasonEndingValidation.Validate(db);
            Debug.Log("PASS battlefield dialogue: " + checks + " checks, 28 victory/loss/skip scenarios, save-before-dialogue and bubble layout.");
        }

        static void ValidateCastlePreferences(CatalogDatabase db)
        {
            bool oldMotion = GamePreferences.Current.ReduceMotion, oldFlashes = GamePreferences.Current.ReduceFlashes;
            try
            {
                foreach (bool quiet in new[] { false, true })
                foreach (bool flashes in new[] { false, true })
                foreach (string form in new[] { "B-KN", "B-AR", "H-02" })
                {
                    GamePreferences.Current.ReduceMotion = quiet;
                    GamePreferences.Current.ReduceFlashes = flashes;
                    var battle = new CatalogBattle(db);
                    var ally = battle.AddUnit("survivor", CatalogTeam.Ally, form, null, null, -1, 0);
                    var enemy = battle.AddEnemy("golem-boss", 1, 1, 0, 100, 1, .01f, true, .5f, 0, CatalogEnemyRole.Golem);
                    typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(enemy, 0f, null);
                    battle.Step(1f / 60);
                    var go = new GameObject("Castle vignette settings QA");
                    try
                    {
                        var lab = go.AddComponent<CatalogCombatLab>(); int result = 0;
                        lab.OpenExpeditionBattle(battle, 10, 30, "QA", won => {}, () => {}, () => result++);
                        lab.ConfigureExpeditionSpeed(2, null);
                        Set(lab, "settling", true); Set(lab, "settlementVictory", true);
                        Call(lab, "AdvanceSettlement", .6f);
                        float hp = ally.Hp, x = ally.X, y = ally.Y, time = battle.Time;
                        Call(lab, "AdvanceDialogue", 3f);
                        Check((float)Get(lab, "dialogueAge") > 0 && Get(lab, "dialogueSpeaker") == ally,
                            "castle scene supports one survivor including a fused form and accessibility settings");
                        Check(ally.Hp == hp && ally.X == x && ally.Y == y && enemy.Hp == 0 && battle.Time == time && battle.Units.Count == 2,
                            "castle props cannot revive the golem, spawn targets, move units or tick combat");
                        Call(lab, "FinishDialogue"); Call(lab, "FinishDialogue");
                        Check(result == 1 && !(bool)Get(lab, "castleStoryActive"), "castle exit clears state and finishes once");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(go); }
                }
            }
            finally { GamePreferences.Current.ReduceMotion = oldMotion; GamePreferences.Current.ReduceFlashes = oldFlashes; }
        }

        static void ValidateForestPreferences(CatalogDatabase db)
        {
            bool oldMotion = GamePreferences.Current.ReduceMotion;
            try
            {
                foreach (bool quiet in new[] { false, true })
                foreach (string form in new[] { "B-KN", "B-AR", "H-02" })
                {
                    GamePreferences.Current.ReduceMotion = quiet;
                    var battle = new CatalogBattle(db);
                    var ally = battle.AddUnit("survivor", CatalogTeam.Ally, form, null, null, -1, 0);
                    var enemy = battle.AddEnemy("beast-boss", 1, 1, 0, 100, 1, .01f, true, .5f, 0, CatalogEnemyRole.Beast);
                    typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(enemy, 0f, null);
                    battle.Step(1f / 60);
                    var go = new GameObject("Forest vignette settings QA");
                    try
                    {
                        var lab = go.AddComponent<CatalogCombatLab>();
                        int result = 0;
                        lab.OpenExpeditionBattle(battle, 5, 30, "QA", won => {}, () => {}, () => result++);
                        Set(lab, "settling", true); Set(lab, "settlementVictory", true);
                        Call(lab, "AdvanceSettlement", .6f);
                        float hp = ally.Hp, x = ally.X, y = ally.Y;
                        Call(lab, "AdvanceDialogue", 3.3f);
                        Check((float)Get(lab, "dialogueAge") > 0 && Get(lab, "dialogueSpeaker") == ally,
                            "motion setting and a lone basic/fused survivor reach the first line");
                        Check(ally.Hp == hp && ally.X == x && ally.Y == y && enemy.Hp == 0 && battle.Units.Count == 2,
                            "vignette changes no HP, position, boss life or roster");
                        Call(lab, "FinishDialogue"); Call(lab, "FinishDialogue");
                        Check(result == 1 && !(bool)Get(lab, "forestStoryActive"), "skip cleans the vignette and returns once");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(go); }
                }
            }
            finally { GamePreferences.Current.ReduceMotion = oldMotion; }
        }
    }
}
