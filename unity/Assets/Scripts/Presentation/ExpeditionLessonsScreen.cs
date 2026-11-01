using System.Collections.Generic;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        const string LessonsKey = "ember.expedition.lessons.v042";
        enum LessonStage { None, Growth, EvolutionReady, EvolutionChoice, TacticsReady, TacticsBoard, CombinationReady, CombinationChoice }
        int lessonMask;
        ExpeditionLesson lessonReplay;
        GUIStyle lessonText;
        readonly List<Rect> lessonHoles = new List<Rect>(5);
        readonly List<Rect> lessonDark = new List<Rect>(50);
        static readonly Rect PreparationLessonCard = new Rect(908, 208, 344, 286);
        static readonly Rect EvolutionLessonCard = new Rect(162, 30, 956, 76);
        static readonly Rect TacticsLessonCard = new Rect(76, 42, 1128, 111);

        bool GrowthPreviewHeaderCovered()
        {
            LessonStage stage = CurrentLessonStage();
            return stage == LessonStage.EvolutionChoice || stage == LessonStage.CombinationChoice;
        }

        void LoadLessons()
        {
            if (loadedAccount != null) { lessonMask = loadedAccount.LessonMask | (loadedAccount.CombinationGuideSeen ? 8 : 0); return; }
            if (storageBlocked) { lessonMask = 15; return; }
            try
            {
            lessonMask = PlayerPrefs.HasKey(LessonsKey) ? ExpeditionLearning.RestoreMask(PlayerPrefs.GetInt(LessonsKey, 7)) :
                ExpeditionLearning.ExistingKnowledge(run);
            }
            catch { lessonMask = 15; }
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
            if (action == "combine") FinishLesson(ExpeditionLesson.Combination);
        }
        LessonStage CurrentLessonStage()
        {
            if (home || run == null || run.Phase != ExpeditionPhase.Preparation || CurrentTutorialStep() != ExpeditionTutorialStep.None)
                return LessonStage.None;
            if (modal == Modal.Confirm && pendingAction == GrowthAction.Evolve && WantsLesson(ExpeditionLesson.Evolution))
                return LessonStage.EvolutionChoice;
            if (modal == Modal.Confirm && pendingAction == GrowthAction.Combine && WantsLesson(ExpeditionLesson.Combination))
                return LessonStage.CombinationChoice;
            if (modal == Modal.Tactics && WantsLesson(ExpeditionLesson.Tactics)) return LessonStage.TacticsBoard;
            if (modal != Modal.None) return LessonStage.None;
            if (WantsLesson(ExpeditionLesson.Evolution) && ExpeditionLearning.EvolutionExample(run) != null)
                return LessonStage.EvolutionReady;
            if (WantsLesson(ExpeditionLesson.Combination) && run.GetCombinationOptions().Count > 0)
                return LessonStage.CombinationReady;
            if (WantsLesson(ExpeditionLesson.Growth) && (run.Wave >= 3 || lessonReplay == ExpeditionLesson.Growth) &&
                ExpeditionLearning.GrowthExample(run, benchPage) != null) return LessonStage.Growth;
            if (WantsLesson(ExpeditionLesson.Tactics) &&
                (ExpeditionLearning.CanExploreTactics(run) || lessonReplay == ExpeditionLesson.Tactics)) return LessonStage.TacticsReady;
            return LessonStage.None;
        }
        static ExpeditionLesson LessonFor(LessonStage stage)
        {
            return stage == LessonStage.Growth ? ExpeditionLesson.Growth :
                stage == LessonStage.CombinationReady || stage == LessonStage.CombinationChoice ? ExpeditionLesson.Combination :
                stage == LessonStage.EvolutionReady || stage == LessonStage.EvolutionChoice ? ExpeditionLesson.Evolution : ExpeditionLesson.Tactics;
        }
        static Rect LessonCard(LessonStage stage)
        { return stage == LessonStage.EvolutionChoice || stage == LessonStage.CombinationChoice ? EvolutionLessonCard : stage == LessonStage.TacticsBoard ? TacticsLessonCard : PreparationLessonCard; }
        Rect LessonMemberRect(CatalogMember member)
        {
            Vector2 foot = ArenaPoint(member);
            return PreparationLayout.Hit(foot);
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
                if (pointerDragging) lessonHoles.Add(PreparationLayout.Hit(pointerCurrent));
            }
            else if (stage == LessonStage.EvolutionReady)
            {
                CatalogMember member = ExpeditionLearning.EvolutionExample(run);
                if (member != null && IsDeployed(member.Id)) lessonHoles.Add(LessonMemberRect(member));
            }
            else if (stage == LessonStage.CombinationReady) lessonHoles.Add(new Rect(506, 110, 204, 44));
            else if (stage == LessonStage.EvolutionChoice || stage == LessonStage.CombinationChoice)
            {
                lessonHoles.Add(new Rect(162, 112, 956, 88));
                lessonHoles.Add(new Rect(954, 62, 164, 42));
                lessonHoles.Add(new Rect(162, 212, 956, 278));
                lessonHoles.Add(new Rect(162, 506, 956, 154));
            }
            else if (stage == LessonStage.TacticsReady)
            {
                lessonHoles.Add(PreparationLayout.Research);
                lessonHoles.Add(PreparationLayout.Summon);
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
            if (stage == LessonStage.EvolutionChoice || stage == LessonStage.CombinationChoice)
            {
                LocalizedGui.Label(new Rect(card.x + 12, card.y + 6, card.width - 168, 64),
                    stage == LessonStage.CombinationChoice ?
                    CatalogText.Get("text.0410") :
                    CatalogText.Get("text.0411"), lessonText);
                if (LocalizedGui.Button(new Rect(card.xMax - 144, card.y + 17, 132, 42), CatalogText.Get("text.0412"), button)) FinishLesson(LessonFor(stage));
                return;
            }
            if (stage == LessonStage.TacticsBoard)
            {
                LocalizedGui.Label(new Rect(card.x + 14, card.y + 8, card.width - 190, 96),
                    CatalogText.Get("text.0413") + run.Gold + CatalogText.Get("text.0414"), lessonText);
                if (LocalizedGui.Button(new Rect(card.xMax - 162, card.y + 33, 148, 44), CatalogText.Get("text.0412"), button)) FinishLesson(ExpeditionLesson.Tactics);
                return;
            }
            string message = stage == LessonStage.Growth ?
                CatalogText.Get("text.0415") :
                stage == LessonStage.EvolutionReady ?
                CatalogText.Get("text.0416") :
                stage == LessonStage.CombinationReady ?
                CatalogText.Get("text.0417") :
                CatalogText.Get("text.0418");
            LocalizedGui.Label(new Rect(card.x + 14, card.y + 16, card.width - 28, 126), message, lessonText);
            if (stage != LessonStage.Growth && LocalizedGui.Button(new Rect(card.x + 14, card.y + 160, card.width - 28, 44),
                stage == LessonStage.EvolutionReady ? CatalogText.Get("text.0419") : stage == LessonStage.CombinationReady ? CatalogText.Get("text.0420") : CatalogText.Get("text.0421"), primary))
            {
                CancelPointer();
                if (stage == LessonStage.EvolutionReady) PrepareEvolution(ExpeditionLearning.EvolutionExample(run));
                else if (stage == LessonStage.CombinationReady) OpenCombinationOptions();
                else { selectedTacticNodeId = "bulwark"; modal = Modal.Tactics; }
            }
            if (LocalizedGui.Button(new Rect(card.x + 14, card.y + 220, card.width - 28, 44), CatalogText.Get("text.0327"), button))
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
            LocalizedGui.Label(new Rect(content.x, 74, content.width, 45), CatalogText.Get("text.0422"), heading);
            string[] names = { CatalogText.Get("text.0423"), CatalogText.Get("text.0424"), CatalogText.Get("text.0425"), CatalogText.Get("text.0426"), CatalogText.Get("text.0576") };
            string[] descriptions = {
                CatalogText.Get("text.0428"),
                CatalogText.Get("text.0429"),
                CatalogText.Get("text.0430"),
                CatalogText.Get("text.0431"),
                CatalogText.Get("text.0432") + CatalogResearch.MaxNodes + CatalogText.Get("text.0433")
            };
            ExpeditionGrowthExample example = ExpeditionLearning.GrowthExample(run);
            CatalogMember evolution = ExpeditionLearning.EvolutionExample(run);
            for (int i = 0; i < 5; i++)
            {
                Rect row = new Rect(content.x, 128 + i * 91, content.width, 84);
                ExpeditionUiTheme.PanelBox(row);
                LocalizedGui.Label(new Rect(row.x + 14, row.y + 9, 120, 32), names[i], body);
                LocalizedGui.Label(new Rect(row.x + 140, row.y + 8, row.width - 320, 68), descriptions[i], lessonText);
                bool canFollow = i == 0 || i == 1 && example != null || i == 2 && evolution != null ||
                    i == 3 && run.GetCombinationOptions().Count > 0 || i == 4 && run.Wave >= 6;
                GUI.enabled = canFollow;
                if (LocalizedGui.Button(new Rect(row.xMax - 163, row.y + 16, 148, 52), canFollow ? CatalogText.Get("text.0434") : CatalogText.Get("text.0435"), button))
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
                    else if (i == 3) { lessonReplay = ExpeditionLesson.Combination; modal = Modal.None; }
                    else { lessonReplay = ExpeditionLesson.Tactics; selectedTacticNodeId = "bulwark"; modal = Modal.Tactics; }
                }
                GUI.enabled = true;
            }
            ExpeditionUiTheme.FooterRule(panel);
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterRect(panel), CatalogText.Get("text.0400"), button)) modal = Modal.None;
        }
    }
}
