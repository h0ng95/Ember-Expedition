using System;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class SeasonEndingValidation
    {
        static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        static int checks;
        static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception("Season ending: " + why); }
        static object Get(object obj, string field) { return obj.GetType().GetField(field, Hidden).GetValue(obj); }
        static void Set(object obj, string field, object value) { obj.GetType().GetField(field, Hidden).SetValue(obj, value); }
        static void Call(object obj, string method, params object[] args) { obj.GetType().GetMethod(method, Hidden).Invoke(obj, args); }
        public static void Validate(CatalogDatabase db)
        {
            checks = 0;
            Check(Resources.Load<Texture2D>(SeasonOneEndingView.TownPath) != null, "ending town is bundled");
            foreach (bool won in new[] { false, true }) foreach (bool unlocked in new[] { false, true })
            foreach (int wave in new[] { 5, 25, 30 })
                Check(SeasonOneEndingView.ShouldAutoPlay(won, wave, unlocked) == (won && wave == 30 && !unlocked), "only first final victory autoplays");
            bool oldMotion = GamePreferences.Current.ReduceMotion, oldFlash = GamePreferences.Current.ReduceFlashes;
            try
            {
                foreach (bool quiet in new[] { false, true }) foreach (bool flash in new[] { false, true })
                {
                    GamePreferences.Current.ReduceMotion = quiet; GamePreferences.Current.ReduceFlashes = flash;
                    foreach (int wave in new[] { 15, 20, 25, 30 }) foreach (string form in new[] { "B-KN", "B-AR", "H-02" })
                    {
                        var battle = new CatalogBattle(db);
                        var ally = battle.AddUnit("survivor", CatalogTeam.Ally, form, null, null, -1, 0);
                        var role = wave == 15 ? CatalogEnemyRole.Furnace : wave == 20 ? CatalogEnemyRole.FrostWarden : wave == 25 ? CatalogEnemyRole.GardenLord : CatalogEnemyRole.CrownDrake;
                        var enemy = battle.AddEnemy("qa-boss", 1, 1, 0, 100, 1, .01f, true, .5f, 0, role);
                        typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(enemy, 0f, null); battle.Step(1f / 60);
                        var go = new GameObject("Regional story QA");
                        try
                        {
                            var lab = go.AddComponent<CatalogCombatLab>(); int result = 0;
                            lab.OpenExpeditionBattle(battle, wave, 30, "QA", won => {}, () => {}, () => result++);
                            lab.ConfigureExpeditionSpeed(2, null);
                            Set(lab, "settling", true); Set(lab, "settlementVictory", true); Call(lab, "AdvanceSettlement", .6f);
                            float hp = ally.Hp, x = ally.X, y = ally.Y, time = battle.Time;
                            Call(lab, "AdvanceDialogue", 4f);
                            Check((bool)Get(lab, "regionalStoryActive") && Get(lab, "dialogueSpeaker") == ally, "each regional scene supports a solo real survivor");
                            Check(ally.Hp == hp && ally.X == x && ally.Y == y && enemy.Hp == 0 && battle.Time == time && battle.Units.Count == 2, "regional story cannot change combat state");
                            Call(lab, "FinishDialogue"); Call(lab, "FinishDialogue");
                            Check(result == 1 && !(bool)Get(lab, "regionalStoryActive"), "regional result returns exactly once");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(go); }
                    }
                    for (int skipPage = -1; skipPage < 3; skipPage++)
                    {
                        int done = 0;
                        using (var view = new SeasonOneEndingView(new[] { "B-AR", "H-02" }, () => done++))
                        {
                            for (int page = 0; page < 3 && view.Active; page++)
                            {
                                view.Tick(3); Check(view.Page == page, "ending advances in order");
                                if (page == skipPage) view.Finish(); else view.Next();
                            }
                            view.Next(); view.Finish();
                            Check(done == 1 && !view.Active, "skip or final next completes once");
                        }
                    }
                }
            }
            finally { GamePreferences.Current.ReduceMotion = oldMotion; GamePreferences.Current.ReduceFlashes = oldFlash; }
            var journal = ExpeditionJournal.Restore(new ExpeditionJournalSave { Version = 1, UnlockedIds = new[] { "palace-clear" } });
            Check(journal.EndingParty.Length == 0, "old journals without the optional ending party remain valid");
            journal.RecordEndingParty(new[] { "B-AR", "H-02" });
            var save = journal.Capture(); var restored = ExpeditionJournal.Restore(JsonUtility.FromJson<ExpeditionJournalSave>(JsonUtility.ToJson(save)));
            save.EndingParty[0] = "B-KN";
            Check(restored.EndingParty[0] == "B-AR" && journal.EndingParty[0] == "B-AR", "ending party persists and captures are independent");
            var font = Resources.Load<Font>("Fonts/Galmuri11");
            var style = new GUIStyle { font = font, fontSize = 23, wordWrap = true };
            foreach (string caption in SeasonOneEndingView.Captions) Check(style.CalcHeight(new GUIContent(caption), 920) <= 64, "ending captions fit mobile footer");
            Debug.Log("PASS season ending: " + checks + " checks; 48 regional survivor/settings cases; ending skip/replay and optional journal party.");
        }
    }
}
