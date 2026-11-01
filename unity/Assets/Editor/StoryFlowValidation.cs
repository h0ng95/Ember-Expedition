using System;
using System.IO;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class StoryFlowValidation
    {
        static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static int checks;
        static void Check(bool value, string why) { checks++; if (!value) throw new Exception("Story flow: " + why); }
        static void Set(object o, string key, object value) { o.GetType().GetField(key, Hidden).SetValue(o, value); }
        static object Get(object o, string key) { return o.GetType().GetField(key, Hidden).GetValue(o); }
        static object Call(object o, string key, params object[] args) { return o.GetType().GetMethod(key, Hidden).Invoke(o, args); }
        static void Modal(object o, string value)
        { var f = o.GetType().GetField("modal", Hidden); f.SetValue(o, Enum.Parse(f.FieldType, value)); }
        public static void ValidateAndBuild()
        {
            Validate(); ExpeditionLearningValidation.Validate(); ExpeditionDialogueValidation.Validate();
            PrototypeSetup.BuildAndroid();
        }
        public static void Validate()
        {
            checks = 0;
            foreach (float x in new[] { 450f, 700f, 900f, 1140f })
            foreach (float y in new[] { 350f, 490f, 620f })
            foreach (int wave in new[] { 10, 15, 20, 25, 30 })
            {
                Vector2 core = BossStoryReveal.CoreGround(new Vector2(x, y)), clue = BossStoryReveal.ClueGround(core);
                float half = BossStoryReveal.ClueWidth(wave) * .5f;
                Check(clue.x - half > core.x + 54 && clue.x + half < 1256 && clue.y < 610,
                    "clue stays beside core without covering the name or footer");
                Check(BossStoryReveal.ClueIndex(wave) >= 0, "regional clue missing");
            }
            foreach (bool quiet in new[] { false, true })
            {
                Check(BossStoryReveal.Alpha(false, 100, quiet) == 0, "props hidden before defeat even with reduced motion");
                Check(BossStoryReveal.Alpha(true, 1, quiet) == 1, "props visible when core reveal finishes");
            }
            Check(BossStoryReveal.Alpha(true, 0, false) == 0 && BossStoryReveal.Alpha(true, .5f, false) > 0,
                "clues fade with the core, never wait for a later dialogue tap");
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var run = ExpeditionRun.New(db, 66001);
            while (run.Wave < 6) { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
            for (int i = 0; i < 2; i++) run.Transfer(run.Summon(), "ally-01", false);
            run.Evolve("ally-01", "E-KN-G");
            for (int i = 0; i < 2; i++) run.Transfer(run.Summon(), "ally-03", false);
            run.Evolve("ally-03", "E-PR-H");
            Check(run.GetCombinationOptions().Count > 0, "combination fixture legal");
            var progress = ExpeditionProgress.New(); progress.Observe(run);
            var journal = ExpeditionJournal.New(); journal.Observe(run);
            var account = new ExpeditionAccountSave { Version = 2, HasRun = true, Run = run.Capture(),
                Progress = progress.Capture(), Journal = journal.Capture(), TutorialMask = 3, LessonMask = 7,
                History = new ExpeditionHistory().Capture() };
            account.Validate(db);
            Directory.CreateDirectory("Logs/v066-fixtures");
            File.WriteAllText("Logs/v066-fixtures/combination.json", JsonUtility.ToJson(account));
            var go = new GameObject("Story flow guide QA"); go.SetActive(false);
            try
            {
                var screen = go.AddComponent<ExpeditionScreen>(); Set(screen, "database", db);
                Set(screen, "run", run); Set(screen, "home", false); Set(screen, "tutorialMask", 3);
                Set(screen, "loadedAccount", account); Call(screen, "LoadLessons");
                string before = JsonUtility.ToJson(run.Capture());
                Check((int)Get(screen, "lessonMask") == 7, "old accounts default to unseen combination guide");
                Check(Call(screen, "CurrentLessonStage").ToString() == "CombinationReady", "first available combination is introduced");
                Check((bool)Call(screen, "LessonPointerAllowed", new Vector2(608,132), true, false), "combination button reachable");
                Check(!(bool)Call(screen, "LessonPointerAllowed", new Vector2(400,620), true, false), "dimmed sacrifice input blocked");
                Call(screen, "OpenCombinationOptions"); Call(screen, "ChooseCombinationOption", 0);
                Check(Call(screen, "CurrentLessonStage").ToString() == "CombinationChoice", "comparison guide appears before confirming");
                Check((bool)Call(screen, "GrowthPreviewHeaderCovered"), "covered detail button cannot intercept guide acknowledgement");
                Call(screen, "FinishLesson", ExpeditionLesson.Combination);
                Check(!(bool)Call(screen, "GrowthPreviewHeaderCovered"), "detail button returns after guide acknowledgement");
                Check(before == JsonUtility.ToJson(run.Capture()), "reading or dismissing guide never consumes units, gold or undo");
                Check((int)Get(screen, "lessonMask") == 15, "acknowledgement includes all four guide bits");
                Modal(screen, "None");
                Check(Call(screen, "CurrentLessonStage").ToString() == "None", "guide does not recur after acknowledgement");
                Set(screen, "lessonReplay", ExpeditionLesson.Combination);
                Check(Call(screen, "CurrentLessonStage").ToString() == "CombinationReady", "guide can be replayed");
                Call(screen, "FinishLesson", ExpeditionLesson.Combination);
                Check((ExpeditionLesson)Get(screen, "lessonReplay") == ExpeditionLesson.None, "replay finishes");
                Set(screen, "progress", progress); Set(screen, "journal", journal);
                string storage = "Logs/v066-fixtures/account-" + Guid.NewGuid().ToString("N");
                Call(screen, "InitializeAccountStorage", storage); Call(screen, "PersistAccount");
                var store = (ExpeditionSaveStore)Get(screen, "accountStore");
                var saved = JsonUtility.FromJson<ExpeditionAccountSave>(store.Load().Payload); saved.Validate(db);
                Check(saved.LessonMask == 7 && saved.CombinationGuideSeen, "persisted mask remains compatible and new flag survives");
                Set(screen, "loadedAccount", saved); Set(screen, "lessonMask", 0); Call(screen, "LoadLessons");
                Check((int)Get(screen, "lessonMask") == 15, "guide acknowledgement survives reload");
                Set(screen, "run", ExpeditionRun.New(db, 66002)); Set(screen, "lessonMask", 7);
                Check(Call(screen, "CurrentLessonStage").ToString() == "None", "no recipe means no combination prompt");
                Set(screen, "lessonMask", 7); Call(screen, "ObserveLessonAction", "combine");
                Check((int)Get(screen, "lessonMask") == 15, "direct successful combination acknowledges guide");
                var style = new GUIStyle { font = Font.CreateDynamicFontFromOSFont(new[] {"Noto Sans CJK KR", "Malgun Gothic", "Arial"}, 17), fontSize = 17, wordWrap = true };
                Check(style.CalcHeight(new GUIContent("첫 결합 · 두 동료가 한 명이 됩니다. 역할과 능력, 빈 출전 자리를 함께 확인하세요.\n확정할 때만 재료가 사라집니다. 두 명을 그대로 쓰는 것도 좋은 선택입니다."), 788) <= 64,
                    "combination comparison message fits without hiding unit stats");
                UnityEngine.Object.DestroyImmediate(style.font);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            Debug.Log("PASS story reveal and optional combination guide: " + checks + " assertions.");
        }
    }
}
