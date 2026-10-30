using System;
using System.Collections.Generic;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class CatalogCombatLab
    {
        Action expeditionPresentationFinished;
        IReadOnlyList<ExpeditionDialogueLine> dialogueLines;
        CatalogBattleUnit dialogueSpeaker;
        bool dialogueActive;
        int dialogueIndex;
        float dialogueAge;
        string dialogueWrapped;
        GUIStyle dialogueText, dialogueName, dialogueCaption, dialogueControl;
        Font dialogueFont;
        static readonly Color SpeechInk = new Color32(38, 40, 53, 255);
        static readonly Color SpeechPaper = new Color32(255, 249, 228, 255);
        static readonly Color SpeechShade = new Color32(215, 196, 161, 255);
        static readonly Color SpeechGold = new Color32(255, 213, 132, 255);

        bool TryStartDialogue()
        {
            if (battle == null || !battle.Ended || battle.Winner != CatalogTeam.Ally) return false;
            dialogueLines = ExpeditionDialogue.ForBoss(expeditionWave);
            if (dialogueLines == null || ExpeditionDialogue.Speaker(battle.Units, false) == null) return false;
            dialogueActive = true;
            dialogueIndex = 0;
            BeginIgnisReveal();
            BeginForestStory();
            floating.Clear();
            SetDialogueSpeaker();
            return true;
        }

        void SetDialogueSpeaker()
        {
            dialogueSpeaker = ExpeditionDialogue.Speaker(battle.Units, dialogueLines[dialogueIndex].Observer);
            dialogueAge = 0;
            dialogueWrapped = null;
            partyFocusId = dialogueSpeaker == null ? null : dialogueSpeaker.Id;
            partyFocusUntil = float.PositiveInfinity;
        }

        void AdvanceDialogue(float dt)
        {
            AdvanceForestStory(dt);
            if (DialogueReady) dialogueAge += dt;
            foreach (var view in combatViews)
            {
                view.HitTime = view.ReleaseRemaining = 0;
                view.NewRelease = false;
                if (view.Unit.IsAlive) { if (view.State != 0) view.StateTime = 0; view.State = 0; }
                view.StateTime += dt;
            }
        }

        void NextDialogue()
        {
            if (!dialogueActive) return;
            if (!DialogueReady) { CompleteForestReveal(); return; }
            if (dialogueAge < .15f) return;
            string text = dialogueLines[dialogueIndex].Text;
            if (dialogueAge * 38 < text.Length) { dialogueAge = text.Length / 38f + .1f; return; }
            dialogueIndex++;
            if (dialogueIndex >= dialogueLines.Count) { FinishDialogue(); return; }
            SetDialogueSpeaker();
        }

        void FinishDialogue()
        {
            if (!dialogueActive) return;
            dialogueActive = false;
            forestStoryActive = false; forestStoryAge = 0;
            Action finished = expeditionPresentationFinished;
            expeditionPresentationFinished = null;
            enabled = false;
            if (finished != null) finished();
        }

        public static Rect DialogueBubbleRect(Vector2 head, float height, float width = 360)
        {
            float x = head.x - width * .5f, y = head.y - height - 20;
            if (y < 88)
            {
                x = head.x < Width * .5f ? head.x + 64 : head.x - width - 64;
                y = head.y - 48;
            }
            return new Rect(Mathf.Floor(Mathf.Clamp(x, 24, Width - width - 24) / 4) * 4,
                Mathf.Floor(Mathf.Clamp(y, 88, 610 - height) / 4) * 4, width, height);
        }

        public static string WrapDialogueText(GUIStyle style, string text, float width)
        {
            // Prefer Korean word boundaries; IMGUI otherwise splits an eojeol between syllables.
            string result = "", line = "";
            foreach (string word in text.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && style.CalcSize(new GUIContent(candidate)).x > width)
                { result += line + "\n"; line = word; }
                else line = candidate;
            }
            return result + line;
        }

        void EnsureDialogueStyles()
        {
            if (dialogueText != null) return;
            dialogueFont = Resources.Load<Font>("Fonts/Galmuri11");
            dialogueText = new GUIStyle { font = dialogueFont != null ? dialogueFont : uiFont, fontSize = 20,
                wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset() };
            dialogueText.normal.textColor = SpeechInk;
            dialogueName = new GUIStyle(dialogueText) { wordWrap = false, alignment = TextAnchor.MiddleCenter };
            dialogueName.normal.textColor = SpeechGold;
            dialogueCaption = new GUIStyle(dialogueName) { alignment = TextAnchor.MiddleLeft };
            dialogueCaption.normal.textColor = new Color32(190, 184, 166, 255);
            dialogueControl = new GUIStyle(dialogueName);
            dialogueControl.normal.textColor = SpeechPaper;
        }

        // Four-pixel steps are deliberately drawn, without smoothed corners or stretched artwork.
        static void SpeechPlate(Rect r, Color color)
        {
            Fill(new Rect(r.x + 12, r.y, r.width - 24, r.height), color);
            Fill(new Rect(r.x + 8, r.y + 4, r.width - 16, r.height - 8), color);
            Fill(new Rect(r.x + 4, r.y + 8, r.width - 8, r.height - 16), color);
            Fill(new Rect(r.x, r.y + 12, r.width, r.height - 24), color);
        }

        static void SpeechTail(Rect bubble, Vector2 head)
        {
            if (bubble.yMax <= head.y)
            {
                float x = Mathf.Round(Mathf.Clamp(head.x - 8, bubble.x + 32, bubble.xMax - 40) / 4) * 4;
                for (int row = 0; row < 5; row++)
                {
                    float width = 20 - row * 4, left = x - 8 + row * 4, y = bubble.yMax - 4 + row * 4;
                    Fill(new Rect(left + 4, y + 8, width, 4), new Color(0, 0, 0, .4f));
                    Fill(new Rect(left, y, width, 4), SpeechInk);
                    if (width > 8) Fill(new Rect(left + 4, y, width - 8, 4), SpeechPaper);
                }
                // Open the bottom border where the tail joins the body.
                Fill(new Rect(x - 4, bubble.yMax - 8, 12, 8), SpeechPaper);
            }
            else
            {
                bool right = head.x > bubble.center.x;
                float y = Mathf.Round(Mathf.Clamp(head.y, bubble.y + 32, bubble.yMax - 32) / 4) * 4;
                for (int col = 0; col < 6; col++)
                {
                    float height = 28 - col * 4;
                    float x = right ? bubble.xMax - 4 + col * 4 : bubble.x - col * 4;
                    Fill(new Rect(x, y - height / 2, 4, height), SpeechInk);
                    if (height > 8) Fill(new Rect(x, y - height / 2 + 4, 4, height - 8), SpeechPaper);
                }
            }
        }

        static void SpeechArrow(Vector2 point, Color color, bool down)
        {
            for (int i = 0; i < 3; i++)
                Fill(down ? new Rect(point.x + i * 4, point.y + i * 4, 20 - i * 8, 4) :
                    new Rect(point.x + i * 4, point.y + i * 4, 4, 20 - i * 8), color);
        }

        bool SpeechButton(Rect r, string text, bool next)
        {
            bool held = Event.current != null && r.Contains(Event.current.mousePosition) && Input.GetMouseButton(0);
            SpeechPlate(new Rect(r.x, r.y + 4, r.width, r.height), new Color(0, 0, 0, .5f));
            SpeechPlate(r, new Color32(107, 101, 91, 255));
            SpeechPlate(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8),
                held ? new Color32(63, 67, 78, 255) : SpeechInk);
            GUI.Label(new Rect(r.x + 12, r.y, r.width - (next ? 40 : 24), r.height), text, dialogueControl);
            if (next) SpeechArrow(new Vector2(r.xMax - 32, r.center.y - 10), SpeechGold, false);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        void DrawDialogue(List<DrawnCombatant> drawn)
        {
            EnsureDialogueStyles();
            if (dialogueFont != null && dialogueFont.material != null && dialogueFont.material.mainTexture != null)
                dialogueFont.material.mainTexture.filterMode = FilterMode.Point;
            if (!forestStoryActive || ForestStoryTime >= ForestIgnisDelay) DrawIgnisReveal();
            GUI.Label(new Rect(32, 16, 940, 40), expeditionEncounter, dialogueCaption);
            for (int i = 0; i < dialogueLines.Count; i++)
            {
                float x = 1152 + i * 24;
                Fill(new Rect(x, 28, 12, 12), i <= dialogueIndex ? SpeechGold : new Color32(75, 76, 83, 255));
                if (i == dialogueIndex) Fill(new Rect(x + 4, 32, 4, 4), SpeechPaper);
            }
            DrawnCombatant? speaker = null;
            foreach (var actor in drawn)
                if (actor.View.Unit == dialogueSpeaker) { speaker = actor; break; }
            if (speaker != null && DialogueReady)
            {
                string full = dialogueWrapped ?? (dialogueWrapped = WrapDialogueText(dialogueText, dialogueLines[dialogueIndex].Text, 328));
                float textHeight = dialogueText.CalcHeight(new GUIContent(full), 328);
                Vector2 head = new Vector2(speaker.Value.Point.x, speaker.Value.Point.y - AllyHudHeight(dialogueSpeaker.FormId));
                Rect bubble = DialogueBubbleRect(head, Mathf.Ceil(Mathf.Max(76, textHeight + 36) / 4) * 4);
                SpeechPlate(new Rect(bubble.x + 4, bubble.y + 8, bubble.width, bubble.height), new Color(0, 0, 0, .45f));
                SpeechPlate(bubble, SpeechInk);
                SpeechPlate(new Rect(bubble.x + 4, bubble.y + 4, bubble.width - 8, bubble.height - 8), SpeechShade);
                SpeechPlate(new Rect(bubble.x + 8, bubble.y + 8, bubble.width - 16, bubble.height - 20), SpeechPaper);
                SpeechTail(bubble, head);
                string name = UnitName(dialogueSpeaker);
                float nameWidth = Mathf.Ceil((dialogueName.CalcSize(new GUIContent(name)).x + 20) / 4) * 4;
                Rect nameplate = new Rect(bubble.x + 16, bubble.y - 16, nameWidth, 28);
                SpeechPlate(nameplate, SpeechInk);
                Fill(new Rect(nameplate.x + 12, nameplate.y + 4, nameplate.width - 24, 4), new Color32(89, 83, 75, 255));
                GUI.Label(nameplate, name, dialogueName);
                int characters = Mathf.Clamp((int)(dialogueAge * 38), 0, full.Length);
                // Transparent unrevealed glyphs retain word wrapping throughout the typewriter reveal.
                string reveal = full.Substring(0, characters) + "<color=#26283500>" + full.Substring(characters) + "</color>";
                GUI.Label(new Rect(bubble.x + 16, bubble.y + 16, 328, textHeight + 4), reveal, dialogueText);
                if (characters == full.Length && (GamePreferences.Current.ReduceMotion || (int)(dialogueAge * 2) % 2 == 0))
                    SpeechArrow(new Vector2(bubble.xMax - 36, bubble.yMax - 16), SpeechInk, true);
            }
            SpeechPlate(new Rect(452, 644, 376, 52), new Color(.09f, .1f, .14f, .84f));
            GUI.Label(new Rect(452, 644, 376, 52), DialogueReady ? "화면을 눌러 계속" :
                forestStoryActive ? "잿갈기 뒤에서 작은 기척이 난다" : "이그니스가 모습을 드러낸다", dialogueControl);
            if (SpeechButton(new Rect(24, 644, 204, 52), "건너뛰기", false)) { FinishDialogue(); return; }
            if (SpeechButton(new Rect(1020, 644, 236, 52),
                !DialogueReady ? "연출 넘기기" : dialogueIndex + 1 == dialogueLines.Count ? "결과 보기" : "다음", true)) { NextDialogue(); return; }
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0 &&
                (DialogueReady ? dialogueAge >= .15f : forestStoryActive ? forestStoryAge >= .15f : ignisAge >= .15f))
            { Event.current.Use(); NextDialogue(); }
        }
    }
}
