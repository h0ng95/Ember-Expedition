using System.Collections.Generic;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public enum ExpeditionTutorialStep { None, Summon, Deploy, Select, Range, Start, RangeReplay }

    public static class ExpeditionTutorialRules
    {
        public static int InitialMask(bool existingPlayer) { return existingPlayer ? 3 : 0; }
        public static int RestoreMask(bool hasKey, int storedMask, bool existingPlayer)
        {
            if (!hasKey) return InitialMask(existingPlayer);
            return storedMask >= 0 && storedMask <= 3 ? storedMask : 3;
        }

        public static ExpeditionTutorialStep Step(int wave, int completedMask, bool replay,
            int gold, int rosterCount, int benchCount, int deployedCount, bool canSummon,
            bool selected, bool rangeAcknowledged)
        {
            if (wave < 1) return ExpeditionTutorialStep.None;
            if (wave > 2) return replay ? ExpeditionTutorialStep.RangeReplay : ExpeditionTutorialStep.None;
            if (!replay && (completedMask & (1 << (wave - 1))) != 0)
                return ExpeditionTutorialStep.None;
            if (wave == 1)
            {
                if (benchCount > 0 && deployedCount < 5) return ExpeditionTutorialStep.Deploy;
                if (rosterCount == 3 && gold >= 40 && canSummon && deployedCount < 5)
                    return ExpeditionTutorialStep.Summon;
                return ExpeditionTutorialStep.Start;
            }
            if (rangeAcknowledged) return ExpeditionTutorialStep.Start;
            return selected ? ExpeditionTutorialStep.Range : ExpeditionTutorialStep.Select;
        }
    }

    public static class ExpeditionTutorialMask
    {
        public static bool AllowsPointer(Rect card, IList<Rect> holes, Vector2 point)
        {
            if (card.Contains(point)) return true;
            foreach (Rect hole in holes) if (hole.Contains(point)) return true;
            return false;
        }

        public static bool AllowsDeployRelease(IList<Rect> holes, Vector2 point)
        {
            return holes.Count > 1 && holes[1].Contains(point);
        }

        public static void DarkRects(Rect screen, IList<Rect> holes, List<Rect> output)
        {
            output.Clear();
            var xs = new List<float> { screen.xMin, screen.xMax };
            var ys = new List<float> { screen.yMin, screen.yMax };
            foreach (Rect hole in holes)
            {
                float left = Mathf.Clamp(hole.xMin, screen.xMin, screen.xMax);
                float right = Mathf.Clamp(hole.xMax, screen.xMin, screen.xMax);
                float top = Mathf.Clamp(hole.yMin, screen.yMin, screen.yMax);
                float bottom = Mathf.Clamp(hole.yMax, screen.yMin, screen.yMax);
                if (right <= left || bottom <= top) continue;
                xs.Add(left); xs.Add(right); ys.Add(top); ys.Add(bottom);
            }
            xs.Sort(); ys.Sort();
            for (int yi = 0; yi < ys.Count - 1; yi++)
                for (int xi = 0; xi < xs.Count - 1; xi++)
                {
                    float left = xs[xi], right = xs[xi + 1];
                    float top = ys[yi], bottom = ys[yi + 1];
                    if (right <= left || bottom <= top) continue;
                    Vector2 center = new Vector2((left + right) * .5f, (top + bottom) * .5f);
                    bool clear = false;
                    foreach (Rect hole in holes) if (hole.Contains(center)) { clear = true; break; }
                    if (!clear) output.Add(Rect.MinMaxRect(left, top, right, bottom));
                }
        }
    }

    public sealed partial class ExpeditionScreen
    {
        const string TutorialKey = "ember.expedition.tutorial.v036";
        static readonly Vector2[] TutorialEmptyChoices = { new Vector2(386f, 448f),
            new Vector2(535f, 450f), new Vector2(386f, 285f), new Vector2(535f, 285f),
            new Vector2(140f, 450f), new Vector2(140f, 285f) };
        int tutorialMask, tutorialReplayWave, tutorialViewWave;
        bool tutorialRangeAcknowledged;
        readonly List<Rect> tutorialHoles = new List<Rect>(3);
        readonly List<Rect> tutorialDarkRects = new List<Rect>(36);
        static readonly Rect TutorialCard = new Rect(724, 183, 523, 81);

        void LoadTutorial()
        {
            if (loadedAccount != null) { tutorialMask = loadedAccount.TutorialMask; return; }
            if (storageBlocked) { tutorialMask = 3; return; }
            try
            {
                bool existing = PlayerPrefs.HasKey(SaveKey) || PlayerPrefs.HasKey(LegacySaveKey) ||
                    PlayerPrefs.HasKey(ProgressSaveKey) || PlayerPrefs.HasKey(JournalSaveKey);
                bool hasKey = PlayerPrefs.HasKey(TutorialKey);
                tutorialMask = ExpeditionTutorialRules.RestoreMask(hasKey,
                    PlayerPrefs.GetInt(TutorialKey, 3), existing);
                // FinalizeAccountLoad persists this together with the whole account.
            }
            catch { tutorialMask = 3; }
        }

        void FinishTutorialWave(int wave)
        {
            tutorialReplayWave = 0;
            if (wave < 1 || wave > 2) return;
            int next = tutorialMask | (1 << (wave - 1));
            if (next == tutorialMask) return;
            tutorialMask = next;
            SaveGuidePreferences();
        }

        ExpeditionTutorialStep CurrentTutorialStep()
        {
            if (home || run == null || run.Phase != ExpeditionPhase.Preparation || modal != Modal.None)
                return ExpeditionTutorialStep.None;
            if (tutorialViewWave != run.Wave)
            { tutorialViewWave = run.Wave; tutorialRangeAcknowledged = false; }
            int benchCount = run.Roster.Members.Count - run.DeployedIds.Count;
            return ExpeditionTutorialRules.Step(run.Wave, tutorialMask, tutorialReplayWave == run.Wave,
                run.Gold, run.Roster.Members.Count, benchCount, run.DeployedIds.Count,
                run.CanSummon, PreparationSelection() != null, tutorialRangeAcknowledged);
        }

        void DrawTutorialAdvice()
        {
            ExpeditionTutorialStep step = CurrentTutorialStep();
            if (step == ExpeditionTutorialStep.None)
            {
                GUI.Label(new Rect(735, 193, 490, 65), PreparationAdvice(), body);
            }
        }

        string TutorialMessage(ExpeditionTutorialStep step)
        {
            return step == ExpeditionTutorialStep.Summon ?
                "① 동료를 소환하세요.\n20G 사용 · 결과는 무작위" :
                step == ExpeditionTutorialStep.Deploy ?
                "② 아래 동료를 끌어서\n전장의 빈 곳에 놓으세요." :
                step == ExpeditionTutorialStep.Select ?
                "① 출전 동료를 누르세요.\n공격·지원 범위를 확인하세요." :
                step == ExpeditionTutorialStep.Range ?
                "② 금색: 기본 공격 범위\n청록: 지원 기술이 있을 때" :
                step == ExpeditionTutorialStep.RangeReplay ?
                "동료를 누르면 금색 공격 범위\n지원 기술이 있으면 청록 범위" :
                "③ 전투 시작을 누르세요.\n전투는 자동으로 진행됩니다.";
        }

        void DrawTutorialCard(ExpeditionTutorialStep step)
        {
            ExpeditionUiTheme.PanelBox(TutorialCard, true);
            GUI.Label(new Rect(738, 193, 301, 62), TutorialMessage(step), body);
            if (step == ExpeditionTutorialStep.Range)
            {
                if (GUI.Button(new Rect(1045, 205, 78, 42), "다음", button))
                    tutorialRangeAcknowledged = true;
            }
            if (GUI.Button(new Rect(step == ExpeditionTutorialStep.Range ? 1130 : 1094,
                205, step == ExpeditionTutorialStep.Range ? 99 : 135, 42),
                step == ExpeditionTutorialStep.RangeReplay ? "닫기" : "건너뛰기", button))
            {
                if (run.Wave <= 2) FinishTutorialWave(run.Wave);
                else tutorialReplayWave = 0;
            }
        }

        void DrawTutorialReplayButton()
        {
            if (GUI.Button(new Rect(554, 100, 150, 34), "조작 안내", button))
            { CancelPointer(); modal = Modal.GuideMenu; }
        }

        void BuildTutorialHoles(ExpeditionTutorialStep step)
        {
            tutorialHoles.Clear();
            if (step == ExpeditionTutorialStep.Summon)
                tutorialHoles.Add(new Rect(939, 569, 320, 68));
            else if (step == ExpeditionTutorialStep.Deploy)
            {
                if (benchPage * 5 >= run.Roster.Members.Count - run.DeployedIds.Count) return;
                tutorialHoles.Add(BenchRect(0));
                Vector2 empty;
                if (!TryTutorialEmptyField(out empty)) return;
                tutorialHoles.Add(new Rect(empty.x - 36f, empty.y - 35f, 72f, 62f));
                if (pointerDragging && pointerId != null)
                    tutorialHoles.Add(new Rect(pointerCurrent.x - 55f, pointerCurrent.y - 106f,
                        110f, 115f));
            }
            else if (step == ExpeditionTutorialStep.Start)
                tutorialHoles.Add(new Rect(939, 636, 320, 68));
            else if (step == ExpeditionTutorialStep.Select || step == ExpeditionTutorialStep.RangeReplay &&
                PreparationSelection() == null)
            {
                CatalogMember member = TutorialSelectionTarget();
                if (member == null) return;
                Vector2 foot = ArenaPoint(member);
                // Frame the feet/name; the complete sprite is redrawn above the scrim.
                // A frame through the torso would cross the ally's face.
                tutorialHoles.Add(new Rect(foot.x - 59f, foot.y - 5f, 118f, 39f));
            }
            else if (step == ExpeditionTutorialStep.Range || step == ExpeditionTutorialStep.RangeReplay)
                tutorialHoles.Add(new Rect(32, 133, 687, 373));
        }

        bool TutorialAllowed(Vector2 point)
        {
            if (TutorialCard.Contains(point)) return true;
            ExpeditionTutorialStep step = CurrentTutorialStep();
            if (step == ExpeditionTutorialStep.Select || step == ExpeditionTutorialStep.RangeReplay &&
                PreparationSelection() == null)
            {
                CatalogMember member = TutorialSelectionTarget();
                if (member == null) return false;
                Vector2 foot = ArenaPoint(member);
                return new Rect(foot.x - 54f, foot.y - 113f, 108f, 143f).Contains(point) &&
                    MemberAt(point) == member.Id;
            }
            return ExpeditionTutorialMask.AllowsPointer(TutorialCard, tutorialHoles, point);
        }

        void FilterTutorialGuiEvent()
        {
            ExpeditionTutorialStep step = CurrentTutorialStep();
            if (step == ExpeditionTutorialStep.None) return;
            Event current = Event.current;
            // Let MouseUp reach the GUI control that captured MouseDown so hotControl clears.
            // The Update pointer path independently cancels invalid drag releases.
            if (current.type != EventType.MouseDown) return;
            BuildTutorialHoles(step);
            Vector2 screen = GUIUtility.GUIToScreenPoint(current.mousePosition);
            Vector2 point = new Vector2((screen.x - viewport.x) * Width / viewport.width,
                (screen.y - viewport.y) * Height / viewport.height);
            if (!TutorialAllowed(point)) current.Use();
        }

        bool TutorialDeployReleaseAllowed(Vector2 point)
        {
            return ExpeditionTutorialMask.AllowsDeployRelease(tutorialHoles, point) &&
                MemberAt(point, pointerId) == null;
        }

        bool TutorialPointerAllowed(Vector2 point, bool down, bool up)
        {
            ExpeditionTutorialStep step = CurrentTutorialStep();
            if (step == ExpeditionTutorialStep.None) return true;
            BuildTutorialHoles(step);
            if (down) return TutorialAllowed(point);
            if (up && pointerDragging && step == ExpeditionTutorialStep.Deploy)
                return TutorialDeployReleaseAllowed(point);
            return !up || TutorialAllowed(point);
        }

        void DrawTutorialSpotlight()
        {
            ExpeditionTutorialStep step = CurrentTutorialStep();
            if (step == ExpeditionTutorialStep.None) return;
            BuildTutorialHoles(step);
            ExpeditionTutorialMask.DarkRects(new Rect(0, 0, Width, Height),
                tutorialHoles, tutorialDarkRects);
            foreach (Rect dark in tutorialDarkRects)
                Fill(dark, new Color(0f, 0f, 0f, .76f));
            if (step == ExpeditionTutorialStep.Select || step == ExpeditionTutorialStep.RangeReplay &&
                PreparationSelection() == null)
            {
                CatalogMember member = TutorialSelectionTarget();
                if (member != null)
                {
                    Vector2 foot = ArenaPoint(member);
                    DrawUnit(member, new Rect(foot.x - 48f, foot.y - 113f, 96f, 110f),
                        new Rect(foot.x - 64f, foot.y, 128f, 31f));
                }
            }
            foreach (Rect hole in tutorialHoles) DrawTutorialFrame(hole);
            if (step == ExpeditionTutorialStep.Deploy && tutorialHoles.Count > 1 && !pointerDragging)
            {
                Vector2 source = new Vector2(tutorialHoles[0].center.x, tutorialHoles[0].y);
                Vector2 target = tutorialHoles[1].center;
                for (int i = 1; i <= 12; i++)
                {
                    Vector2 dot = Vector2.Lerp(source, target, i / 13f);
                    Fill(new Rect(dot.x - 2f, dot.y - 2f, 4f, 4f), Gold);
                }
                GUI.Label(new Rect(target.x - 45f, tutorialHoles[1].y - 32f, 90f, 27f), "+ 배치", centered);
            }
            DrawTutorialCard(step);
        }

        bool TryTutorialEmptyField(out Vector2 empty)
        {
            foreach (Vector2 point in TutorialEmptyChoices)
                if (TutorialPointFree(point)) { empty = point; return true; }
            for (float y = 280f; y <= 480f; y += 50f)
                for (float x = 125f; x <= 525f; x += 50f)
                {
                    Vector2 point = new Vector2(x, y);
                    if (TutorialPointFree(point)) { empty = point; return true; }
                }
            empty = Vector2.zero;
            return false;
        }

        bool TutorialPointFree(Vector2 point)
        {
            // These are screen positions that map to themselves after the drop clamp.
            if (point.x < 95f || point.x > 545f || point.y < 270f || point.y > 505f)
                return false;
            Rect target = new Rect(point.x - 36f, point.y - 35f, 72f, 62f);
            foreach (string id in run.DeployedIds)
            {
                CatalogMember member = run.Roster.Find(id);
                if (member == null) continue;
                Vector2 foot = ArenaPoint(member);
                if (target.Overlaps(new Rect(foot.x - 54f, foot.y - 113f, 108f, 143f)))
                    return false;
            }
            return true;
        }

        CatalogMember TutorialSelectionTarget()
        {
            CatalogMember first = null;
            foreach (string id in run.DeployedIds)
            {
                CatalogMember member = run.Roster.Find(id);
                if (member == null) continue;
                if (first == null) first = member;
                if (CatalogPreparationPreview.ForSkill(CatalogPreparationPreview.PrimarySkill(member)) != null ||
                    CatalogPreparationPreview.ForSkill(member.FormId) != null) return member;
            }
            return first;
        }

        void DrawTutorialFrame(Rect target)
        {
            Color edge = new Color(Gold.r, Gold.g, Gold.b,
                .74f + .22f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 5f)));
            const float thick = 3f;
            Fill(new Rect(target.x, target.y, target.width, thick), edge);
            Fill(new Rect(target.x, target.yMax - thick, target.width, thick), edge);
            Fill(new Rect(target.x, target.y, thick, target.height), edge);
            Fill(new Rect(target.xMax - thick, target.y, thick, target.height), edge);
            for (int i = 0; i < 5; i++)
                Fill(new Rect(target.x - 10f + i * 2f, target.center.y - 5f + i,
                    2f, 10f - i * 2f), edge);
        }
    }
}
