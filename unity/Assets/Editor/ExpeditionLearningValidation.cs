using System;
using System.IO;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class ExpeditionLearningValidation
    {
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static int checks;
        const string Key = "ember.expedition.lessons.v042";
        static void Check(bool value, string message) { checks++; if (!value) throw new Exception("Learning UI: " + message); }
        static void Set(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }
        static object Get(object target, string name) { return target.GetType().GetField(name, Private).GetValue(target); }
        static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
        static void Mode(ExpeditionScreen screen, string name)
        { var f = screen.GetType().GetField("modal", Private); f.SetValue(screen, Enum.Parse(f.FieldType, name)); }
        static void Win(ExpeditionRun run) { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
        static void Attach(ExpeditionScreen screen, ExpeditionRun run, int mask)
        { Set(screen, "run", run); Set(screen, "home", false); Set(screen, "tutorialMask", 3); Set(screen, "lessonMask", mask); Mode(screen, "None"); }
        static void Export(string name, ExpeditionRun run, int bestBefore)
        {
            File.WriteAllText("Logs/v042-fixtures/" + name + ".json", JsonUtility.ToJson(run.Capture()));
            var progress = ExpeditionProgress.Restore(new ExpeditionProgressSave { Version = 2, BestWave = bestBefore, ResearchIds = new string[0] });
            progress.Observe(run);
            File.WriteAllText("Logs/v042-fixtures/" + name + "-progress.json", JsonUtility.ToJson(progress.Capture()));
        }
        public static void ValidateAndBuild() { Validate(); PrototypeSetup.BuildAndroid(); }
        public static void Validate()
        {
            checks = 0;
            ExpeditionTutorialValidation.Validate();
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            bool hadKey = PlayerPrefs.HasKey(Key); int old = PlayerPrefs.GetInt(Key, 7);
            var go = new GameObject("Learning validation"); go.SetActive(false);
            var screen = go.AddComponent<ExpeditionScreen>(); Set(screen, "database", db);
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 17);
            try
            {
                Directory.CreateDirectory("Logs/v042-fixtures");
                var run = ExpeditionRun.New(db, 42001, researchBaselineBestWave: 0);
                Win(run); Win(run);
                string donor = run.Summon(); run.Transfer(donor, "ally-01", false); donor = run.Summon();
                Attach(screen, run, 0);
                string before = JsonUtility.ToJson(run.Capture());
                Check(Call(screen, "CurrentLessonStage").ToString() == "Growth", "first growth stage missing");
                Check((bool)Call(screen, "LessonPointerAllowed", new Vector2(222, 630), true, false), "donor blocked");
                Check(!(bool)Call(screen, "LessonPointerAllowed", new Vector2(1200, 670), true, false), "dimmed battle start accepted");
                Set(screen, "pointerId", donor); Set(screen, "pointerDragging", true);
                var recipient = run.Roster.Find("ally-01");
                Check((bool)Call(screen, "LessonPointerAllowed", ExpeditionScreen.PreparationLayout.Point(recipient.X, recipient.Y) + new Vector2(0,-45), false, true), "recipient drop blocked");
                Check(!(bool)Call(screen, "LessonPointerAllowed", new Vector2(200, 470), false, true), "invalid drop accepted");
                Call(screen, "CancelPointer");
                Check(before == JsonUtility.ToJson(run.Capture()), "guide read changed run");
                Export("growth", run, 0);
                Call(screen, "FinishLesson", ExpeditionLesson.Growth);
                Check((int)Get(screen, "lessonMask") == 1 && Call(screen, "CurrentLessonStage").ToString() == "None", "skip not acknowledged");
                Set(screen, "lessonReplay", ExpeditionLesson.Growth);
                Check(Call(screen, "CurrentLessonStage").ToString() == "Growth", "replay unavailable");
                Call(screen, "FinishLesson", ExpeditionLesson.Growth);
                Check((ExpeditionLesson)Get(screen, "lessonReplay") == ExpeditionLesson.None, "replay did not finish");
                run.Transfer(donor, "ally-01", false);
                Check(Call(screen, "CurrentLessonStage").ToString() == "EvolutionReady", "ready evolution missing");
                Call(screen, "PrepareEvolution", run.Roster.Find("ally-01"), false);
                Check(Call(screen, "CurrentLessonStage").ToString() == "EvolutionChoice", "choice guide missing");
                Call(screen, "FinishLesson", ExpeditionLesson.Evolution);
                Check(Call(screen, "CurrentLessonStage").ToString() == "None", "choice acknowledgement ineffective");
                Mode(screen, "None");
                while (run.Wave < 6) Win(run);
                Check(Call(screen, "CurrentLessonStage").ToString() == "TacticsReady", "tactics timing missing");
                Mode(screen, "Tactics");
                Check(Call(screen, "CurrentLessonStage").ToString() == "TacticsBoard", "tactics board guide missing");
                before = JsonUtility.ToJson(run.Capture());
                Call(screen, "FinishLesson", ExpeditionLesson.Tactics);
                Check(before == JsonUtility.ToJson(run.Capture()) && (int)Get(screen, "lessonMask") == 7, "reading tactic bought something");
                var tactics = ExpeditionRun.New(db, 42002, researchBaselineBestWave: 0);
                while (tactics.Wave < 6) Win(tactics);
                Export("tactics", tactics, 0);
                Export("speed", ExpeditionRun.New(db, 42003, researchBaselineBestWave: 0), 0);
                var failed = ExpeditionRun.New(db, 42004, researchBaselineBestWave: 5);
                for (int i = 0; i < 8; i++) Win(failed);
                for (int i = 0; i < 3; i++) { failed.BeginBattle(); failed.ResolveBattle(false); if (i < 2) failed.Continue(); }
                Export("defeat", failed, 5);
                var completed = ExpeditionRun.New(db, 42005, researchBaselineBestWave: 0);
                for (int i = 0; i < 30; i++) Win(completed);
                Export("complete", completed, 0);

                var lab = go.AddComponent<CatalogCombatLab>();
                int observed = 0; Set(lab, "expeditionMode", true);
                lab.ConfigureExpeditionSpeed(2, value => observed = value);
                Check((float)Get(lab, "speed") == 2, "lab resets selected speed");
                Call(lab, "ToggleSpeed"); Check(observed == 1, "speed callback missing");
                Set(lab, "expeditionMode", false); Call(lab, "ToggleSpeed");
                Check(observed == 1, "standalone lab changes expedition speed");
                var text = new GUIStyle { font = font, fontSize = 17, wordWrap = true };
                string lesson = "첫 성장 · 강조된 대기 동료를\n출전 동료 위에 끌어 잠시 유지한 뒤 놓으세요.\n성장 +1 · 재료는 소모 · 준비 중 되돌리기 가능";
                Check(text.CalcHeight(new GUIContent(lesson), 316) <= 126, "growth card text clipped");
                string tacticsText = "소환과 전술은 같은 골드를 사용합니다. 40G는 소환 2회분 · 현재 1234G\n노드를 눌러 효과와 조건을 비교하세요. 전술은 이번 원정에서만 유지됩니다.\n구매는 선택입니다. 소환할 돈을 남겨두어도 됩니다.";
                Check(text.CalcHeight(new GUIContent(tacticsText), 938) <= 75, "tactics card text clipped");
                Debug.Log("PASS: learning UI, " + checks + " assertions; exported QA checkpoints, not balance evidence.");
            }
            finally
            {
                if (hadKey) PlayerPrefs.SetInt(Key, old); else PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save(); UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(font);
            }
        }
    }
}
