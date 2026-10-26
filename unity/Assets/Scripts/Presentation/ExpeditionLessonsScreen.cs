using System.Collections.Generic;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        const string LessonsKey = "ember.expedition.lessons.v042";
        enum LessonStage { None, Growth, EvolutionReady, EvolutionChoice, TacticsReady, TacticsBoard }
        int lessonMask;
        ExpeditionLesson lessonReplay;
        GUIStyle lessonText;
        readonly List<Rect> lessonHoles = new List<Rect>(5);
        readonly List<Rect> lessonDark = new List<Rect>(50);
        static readonly Rect PreparationLessonCard = new Rect(724, 124, 523, 137);
        static readonly Rect EvolutionLessonCard = new Rect(162, 42, 956, 64);
        static readonly Rect TacticsLessonCard = new Rect(76, 62, 1128, 91);

        void LoadLessons()
        {
            if (loadedAccount != null) { lessonMask = loadedAccount.LessonMask; return; }
            if (storageBlocked) { lessonMask = 7; return; }
            try
            {
            lessonMask = PlayerPrefs.HasKey(LessonsKey) ? ExpeditionLearning.RestoreMask(PlayerPrefs.GetInt(LessonsKey, 7)) :
                ExpeditionLearning.ExistingKnowledge(run);
            }
            catch { lessonMask = 7; }
        }
        bool WantsLesson(ExpeditionLesson lesson)
        { return lessonReplay == lesson || lessonReplay == ExpeditionLesson.None && (lessonMask & (int)lesson) == 0; }
        void FinishLesson(ExpeditionLesson lesson)
        {
            lessonMask |= (int)lesson;
            if (lessonReplay == lesson) lessonReplay = ExpeditionLesson.None;
            SaveGuidePreferences();
        }
        void ObserveLessonAction(string action)
        {
            if (action == "transfer") FinishLesson(ExpeditionLesson.Growth);
            if (action == "evolve") FinishLesson(ExpeditionLesson.Evolution);
            if (action == "tactic_purchase") FinishLesson(ExpeditionLesson.Tactics);
        }
        LessonStage CurrentLessonStage()
        {
            if (home || run == null || run.Phase != ExpeditionPhase.Preparation || CurrentTutorialStep() != ExpeditionTutorialStep.None)
                return LessonStage.None;
            if (modal == Modal.Confirm && pendingAction == GrowthAction.Evolve && WantsLesson(ExpeditionLesson.Evolution))
                return LessonStage.EvolutionChoice;
            if (modal == Modal.Tactics && WantsLesson(ExpeditionLesson.Tactics)) return LessonStage.TacticsBoard;
            if (modal != Modal.None) return LessonStage.None;
            if (WantsLesson(ExpeditionLesson.Evolution) && ExpeditionLearning.EvolutionExample(run) != null)
                return LessonStage.EvolutionReady;
            if (WantsLesson(ExpeditionLesson.Growth) && (run.Wave >= 3 || lessonReplay == ExpeditionLesson.Growth) &&
                ExpeditionLearning.GrowthExample(run, benchPage) != null) return LessonStage.Growth;
            if (WantsLesson(ExpeditionLesson.Tactics) &&
                (ExpeditionLearning.CanExploreTactics(run) || lessonReplay == ExpeditionLesson.Tactics)) return LessonStage.TacticsReady;
            return LessonStage.None;
        }
        static ExpeditionLesson LessonFor(LessonStage stage)
        {
            return stage == LessonStage.Growth ? ExpeditionLesson.Growth :
                stage == LessonStage.EvolutionReady || stage == LessonStage.EvolutionChoice ? ExpeditionLesson.Evolution : ExpeditionLesson.Tactics;
        }
        static Rect LessonCard(LessonStage stage)
        { return stage == LessonStage.EvolutionChoice ? EvolutionLessonCard : stage == LessonStage.TacticsBoard ? TacticsLessonCard : PreparationLessonCard; }
        Rect LessonMemberRect(CatalogMember member)
        {
            Vector2 foot = ArenaPoint(member);
            return new Rect(foot.x - 60, foot.y - 115, 120, 149);
        }
        void BuildLessonHoles(LessonStage stage)
        {
            lessonHoles.Clear();
            if (stage == LessonStage.Growth)
            {
                ExpeditionGrowthExample example = ExpeditionLearning.GrowthExample(run, benchPage);
                if (example == null) return;
                int index = 0;
                foreach (CatalogMember member in run.Roster.Members)
                    if (!IsDeployed(member.Id))
                    {
                        if (member.Id == example.DonorId) lessonHoles.Add(BenchRect(index % 5));
                        index++;
                    }
                lessonHoles.Add(LessonMemberRect(run.Roster.Find(example.RecipientId)));
                if (pointerDragging) lessonHoles.Add(new Rect(pointerCurrent.x - 58, pointerCurrent.y - 112, 116, 146));
            }
            else if (stage == LessonStage.EvolutionReady)
            {
                CatalogMember member = ExpeditionLearning.EvolutionExample(run);
                if (member != null && IsDeployed(member.Id)) lessonHoles.Add(LessonMemberRect(member));
            }
            else if (stage == LessonStage.EvolutionChoice)
            {
                lessonHoles.Add(new Rect(162, 112, 956, 88));
                lessonHoles.Add(new Rect(162, 212, 956, 278));
                lessonHoles.Add(new Rect(162, 506, 956, 154));
            }
            else if (stage == LessonStage.TacticsReady)
            {
                lessonHoles.Add(new Rect(1075, 270, 154, 48));
                lessonHoles.Add(new Rect(939, 569, 320, 68));
            }
            else if (stage == LessonStage.TacticsBoard)
            {
                lessonHoles.Add(new Rect(76, 159, 1128, 407));
                lessonHoles.Add(new Rect(76, 594, 1128, 64));
            }
        }
        bool LessonPointerAllowed(Vector2 point, bool down, bool up)
        {
            LessonStage stage = CurrentLessonStage();
            if (stage == LessonStage.None) return true;
            BuildLessonHoles(stage);
            if (stage == LessonStage.Growth && up && pointerDragging)
            {
                ExpeditionGrowthExample example = ExpeditionLearning.GrowthExample(run, benchPage);
                return example != null && pointerId == example.DonorId && MemberAt(point, pointerId) == example.RecipientId;
            }
            return !down && !up || ExpeditionTutorialMask.AllowsPointer(LessonCard(stage), lessonHoles, point);
        }
        void FilterLessonGuiEvent()
        {
            LessonStage stage = CurrentLessonStage();
            if (stage == LessonStage.None || Event.current.type != EventType.MouseDown) return;
            BuildLessonHoles(stage);
            Vector2 screen = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            Vector2 point = new Vector2((screen.x - viewport.x) * Width / viewport.width,
                (screen.y - viewport.y) * Height / viewport.height);
            if (!ExpeditionTutorialMask.AllowsPointer(LessonCard(stage), lessonHoles, point)) Event.current.Use();
        }
        void DrawLessonSpotlight()
        {
            LessonStage stage = CurrentLessonStage();
            if (stage == LessonStage.None) return;
            if (lessonText == null) lessonText = Style(17, Cream, false, TextAnchor.UpperLeft);
            BuildLessonHoles(stage);
            ExpeditionTutorialMask.DarkRects(new Rect(0, 0, Width, Height), lessonHoles, lessonDark);
            foreach (Rect rect in lessonDark) Fill(rect, new Color(0, 0, 0, .76f));
            foreach (Rect rect in lessonHoles) DrawTutorialFrame(rect);
            Rect card = LessonCard(stage);
            ExpeditionUiTheme.PanelBox(card, true);
            if (stage == LessonStage.EvolutionChoice)
            {
                GUI.Label(new Rect(card.x + 12, card.y + 8, card.width - 168, 48),
                    "첫 진화 · 모습과 역할을 비교한 뒤 골라보세요.\n아래 기술·연구 탭도 확인할 수 있어요. 확정 전에는 바뀌지 않습니다.", lessonText);
                if (GUI.Button(new Rect(card.xMax - 144, card.y + 11, 132, 42), "알겠어", button)) FinishLesson(ExpeditionLesson.Evolution);
                return;
            }
            if (stage == LessonStage.TacticsBoard)
            {
                GUI.Label(new Rect(card.x + 14, card.y + 10, card.width - 190, 75),
                    "소환과 전술은 같은 골드를 사용합니다. 40G는 소환 2회분 · 현재 " + run.Gold + "G\n노드를 눌러 효과와 조건을 비교하세요. 전술은 이번 원정에서만 유지됩니다.\n구매는 선택입니다. 소환할 돈을 남겨두어도 됩니다.", lessonText);
                if (GUI.Button(new Rect(card.xMax - 162, card.y + 20, 148, 44), "알겠어", button)) FinishLesson(ExpeditionLesson.Tactics);
                return;
            }
            string message = stage == LessonStage.Growth ?
                "첫 성장 · 강조된 대기 동료를\n출전 동료 위에 끌어 잠시 유지한 뒤 놓으세요.\n성장 +1 · 재료는 소모 · 준비 중 되돌리기 가능" :
                stage == LessonStage.EvolutionReady ?
                "진화할 동료가 있어요.\n모습·역할·기술을 비교한 뒤 골라보세요.\n미리보기만 열어서는 동료가 변하지 않습니다." :
                "전술을 선택할 수 있어요.\n소환과 같은 골드를 써서 원정 전체를 강화해요.\n먼저 효과를 살펴보고 지출을 결정하세요.";
            GUI.Label(new Rect(card.x + 14, card.y + 8, card.width - 28, 78), message, lessonText);
            if (stage != LessonStage.Growth && GUI.Button(new Rect(card.x + 14, card.y + 90, 330, 42),
                stage == LessonStage.EvolutionReady ? "진화 비교하기" : "전술 살펴보기", primary))
            {
                CancelPointer();
                if (stage == LessonStage.EvolutionReady) PrepareEvolution(ExpeditionLearning.EvolutionExample(run));
                else { selectedTacticNodeId = "bulwark"; modal = Modal.Tactics; }
            }
            if (GUI.Button(new Rect(card.xMax - 149, card.y + 90, 135, 42), "건너뛰기", button))
            { CancelPointer(); FinishLesson(LessonFor(stage)); }
            if (stage == LessonStage.Growth && lessonHoles.Count >= 2 && !pointerDragging)
            {
                Vector2 from = new Vector2(lessonHoles[0].center.x, lessonHoles[0].y), to = lessonHoles[1].center;
                for (int i = 1; i < 13; i++)
                {
                    Vector2 dot = Vector2.Lerp(from, to, i / 13f);
                    Fill(new Rect(dot.x - 2, dot.y - 2, 4, 4), Gold);
                }
            }
        }

        void DrawGuideMenu()
        {
            if (lessonText == null) lessonText = Style(17, Cream, false, TextAnchor.UpperLeft);
            Rect panel = new Rect(180, 42, 920, 636), content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 74, content.width, 45), "원정 안내 · 언제든 다시 보기", heading);
            string[] names = { "기본 조작", "성장", "진화", "원정 전술" };
            string[] descriptions = {
                "소환한 동료를 출전시키고, 눌러서 공격·지원 범위를 확인합니다.",
                "동료 위에 재료를 끌어 잠시 유지하면 성장 +1. 재료는 사라지며 준비 중 되돌릴 수 있습니다.",
                "기본은 성장 2, 분기는 성장 6에서 진화. 결과를 비교하고 확정합니다. 최종 진화는 분기 기술을 유지합니다.",
                "소환과 같은 골드를 사용하며 최대 3개 선택. 선행 노드와 단계 조건이 있고, 새 원정에서는 초기화됩니다."
            };
            ExpeditionGrowthExample example = ExpeditionLearning.GrowthExample(run);
            CatalogMember evolution = ExpeditionLearning.EvolutionExample(run);
            for (int i = 0; i < 4; i++)
            {
                Rect row = new Rect(content.x, 134 + i * 104, content.width, 94);
                ExpeditionUiTheme.PanelBox(row);
                GUI.Label(new Rect(row.x + 14, row.y + 9, 120, 32), names[i], body);
                GUI.Label(new Rect(row.x + 140, row.y + 10, row.width - 320, 76), descriptions[i], lessonText);
                bool canFollow = i == 0 || i == 1 && example != null || i == 2 && evolution != null || i == 3 && run.Wave >= 6;
                GUI.enabled = canFollow;
                if (GUI.Button(new Rect(row.xMax - 163, row.y + 22, 148, 52), canFollow ? "따라하기" : "조건 대기", button))
                {
                    modal = Modal.None; tutorialReplayWave = 0;
                    if (i == 0) { lessonReplay = ExpeditionLesson.None; tutorialReplayWave = run.Wave; tutorialRangeAcknowledged = false; }
                    else if (i == 1)
                    {
                        lessonReplay = ExpeditionLesson.Growth;
                        int index = 0;
                        foreach (CatalogMember member in run.Roster.Members) if (!IsDeployed(member.Id))
                        { if (member.Id == example.DonorId) benchPage = index / 5; index++; }
                    }
                    else if (i == 2) { lessonReplay = ExpeditionLesson.Evolution; PrepareEvolution(evolution); }
                    else { lessonReplay = ExpeditionLesson.Tactics; selectedTacticNodeId = "bulwark"; modal = Modal.Tactics; }
                }
                GUI.enabled = true;
            }
            ExpeditionUiTheme.FooterRule(panel);
            if (GUI.Button(ExpeditionUiTheme.FooterRect(panel), "닫기", button)) modal = Modal.None;
        }
    }
}
