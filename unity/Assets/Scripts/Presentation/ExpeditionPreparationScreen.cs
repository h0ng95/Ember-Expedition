using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        static readonly Color SupportColor = new Color(.39f, .87f, .82f);
        string preparationSelectionId, preparationForm, preparationBranch;
        ExpeditionRun preparationOwner;
        bool previewFinalSkill;
        Texture2D preparationRing, preparationDisc;
        GUIStyle rangeCaption, rangeRule, supportName, supportStatus, supportNumber;
        static readonly Color OutsideColor = new Color(.72f, .60f, .51f);

        public static class SupportLayout
        {
            public static Rect Card(int index) { return new Rect(735 + index * 100, 177, 94, 84); }
            public static Rect Name(int index) { Rect r = Card(index); return new Rect(r.x + 3, r.y + 33, 88, 30); }
            public static Rect Status(int index) { Rect r = Card(index); return new Rect(r.x + 3, r.y + 63, 88, 20); }
        }

        void SelectPreparation(string id)
        {
            preparationSelectionId = id;
            preparationOwner = run;
            preparationForm = preparationBranch = null;
            showHelp = false;
        }

        CatalogMember PreparationSelection()
        {
            if (preparationOwner != run) preparationSelectionId = null;
            CatalogMember member = run == null ? null : run.Roster.Find(preparationSelectionId);
            if (member == null || !IsDeployed(member.Id))
            { preparationSelectionId = null; return null; }
            if (preparationForm != member.FormId || preparationBranch != member.BranchId)
            {
                preparationForm = member.FormId; preparationBranch = member.BranchId;
                previewFinalSkill = CatalogPreparationPreview.ForSkill(
                    CatalogPreparationPreview.PrimarySkill(member)) == null;
            }
            return member;
        }

        CatalogSupportPreview SelectedSupport(CatalogMember member)
        {
            return member == null ? null : CatalogPreparationPreview.ForSkill(previewFinalSkill ?
                member.FormId : CatalogPreparationPreview.PrimarySkill(member));
        }

        Vector2 PreparationPosition(CatalogMember member)
        {
            // Read-only drag projection. Drop uses the same clamp; cancellation restores saved positions.
            if (pointerDragging && pointerId == member.Id)
                return new Vector2(Mathf.Clamp((pointerCurrent.x - 560f) / 75f, -6.2f, -.2f),
                    Mathf.Clamp((GroundY - pointerCurrent.y) / GroundYScale, -2.5f, 2.2f));
            return new Vector2(member.X, member.Y);
        }

        bool IsSupportCandidate(CatalogMember source, CatalogMember target, CatalogSupportPreview support)
        {
            if (support == null || source == null || target == null || !IsDeployed(target.Id)) return false;
            Vector2 a = PreparationPosition(source), b = PreparationPosition(target);
            return support.Reaches(a.x, a.y, b.x, b.y);
        }

        void EnsurePreparationArt()
        {
            if (rangeCaption == null)
            {
                rangeCaption = new GUIStyle(body) { fontSize = 17, wordWrap = false,
                    alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
                rangeRule = new GUIStyle(body) { fontSize = 16, wordWrap = true,
                    alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip };
                supportName = new GUIStyle(body) { fontSize = 13, wordWrap = true,
                    alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
                supportStatus = new GUIStyle(supportName) { fontSize = 13, wordWrap = false };
                supportNumber = new GUIStyle(supportStatus) { fontStyle = FontStyle.Bold };
            }
            if (preparationRing != null) return;
            const int size = 128;
            preparationRing = new Texture2D(size, size, TextureFormat.RGBA32, false);
            preparationDisc = new Texture2D(size, size, TextureFormat.RGBA32, false);
            preparationRing.hideFlags = preparationDisc.hideFlags = HideFlags.HideAndDontSave;
            preparationRing.wrapMode = preparationDisc.wrapMode = TextureWrapMode.Clamp;
            var ring = new Color[size * size]; var disc = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x - 63.5f, y - 63.5f).magnitude / 63f;
                ring[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.Abs(distance - .975f) * 50));
                disc[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01((1f - distance) * 50));
            }
            preparationRing.SetPixels(ring); preparationRing.Apply(false, true);
            preparationDisc.SetPixels(disc); preparationDisc.Apply(false, true);
        }

        void ReleasePreparationPreview()
        {
            if (preparationRing != null) Destroy(preparationRing);
            if (preparationDisc != null) Destroy(preparationDisc);
        }

        void PreviewTexture(Texture2D texture, Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color; GUI.DrawTexture(rect, texture); GUI.color = previous;
        }

        void RangeEllipse(Vector2 center, float radius, Color color)
        {
            // World distance is circular; the preparation board projects X and Y at different scales.
            Rect bounds = new Rect(center.x - radius * 75f, center.y - radius * GroundYScale,
                radius * 150f, radius * GroundYScale * 2f);
            PreviewTexture(preparationDisc, bounds, new Color(color.r, color.g, color.b, .065f));
            PreviewTexture(preparationRing, bounds, color);
        }

        void DrawPreparationRange()
        {
            EnsurePreparationArt();
            CatalogMember member = PreparationSelection();
            if (member == null)
            {
                GUI.Label(new Rect(46, 133, 640, 28), "동료를 눌러 범위 확인 · 끌어서 배치", rangeCaption);
                return;
            }
            GUI.Label(new Rect(46, 133, 640, 28),
                SelectedSupport(member) == null ? "금색: 기본 공격 사거리 · 끌어서 배치" :
                "금색: 선택 / 공격   ·   청록: 지원 범위   ·   번호: 오른쪽 동료", rangeCaption);
            Rect clip = new Rect(32, 164, 687, 342);
            Vector2 position = PreparationPosition(member);
            Vector2 center = new Vector2(560f + position.x * 75f, GroundY - position.y * GroundYScale) - clip.position;
            CatalogSupportPreview support = SelectedSupport(member);
            GUI.BeginGroup(clip);
            RangeEllipse(center, database.Get(member.FormId).Range, new Color(Gold.r, Gold.g, Gold.b, .82f));
            if (support != null)
            {
                if (support.Global)
                {
                    Fill(new Rect(0, 0, clip.width, clip.height), new Color(SupportColor.r, SupportColor.g, SupportColor.b, .045f));
                    Fill(new Rect(0, clip.height - 2, clip.width, 2), SupportColor);
                }
                else RangeEllipse(center, support.Radius, SupportColor);
            }
            GUI.EndGroup();
        }

        void DrawPreparationMarker(CatalogMember member, Vector2 foot)
        {
            CatalogMember source = PreparationSelection();
            if (source == null) return;
            bool chosen = source.Id == member.Id;
            bool eligible = IsSupportCandidate(source, member, SelectedSupport(source));
            CatalogSupportPreview support = SelectedSupport(source);
            if (!chosen && support == null) return;
            Color color = chosen ? Gold : eligible ? SupportColor : OutsideColor;
            PreviewTexture(preparationRing, new Rect(foot.x - 37, foot.y - 14, 74, 17), color);
            // Different shapes as well as color: selection dot, candidate corner brackets.
            if (chosen)
                PreviewTexture(preparationDisc, new Rect(foot.x - 4, foot.y - 106, 8, 8), Gold);
            else if (eligible)
            {
                Fill(new Rect(foot.x - 45, foot.y - 100, 3, 16), color);
                Fill(new Rect(foot.x - 45, foot.y - 100, 13, 3), color);
                Fill(new Rect(foot.x + 42, foot.y - 100, 3, 16), color);
                Fill(new Rect(foot.x + 32, foot.y - 100, 13, 3), color);
            }
            else
            {
                // Broken brackets distinguish outside-range allies without relying only on color.
                Fill(new Rect(foot.x - 45, foot.y - 97, 3, 7), color);
                Fill(new Rect(foot.x + 42, foot.y - 97, 3, 7), color);
            }
            if (support != null && !showHelp && CurrentTutorialStep() == ExpeditionTutorialStep.None)
            {
                int index = 0;
                foreach (string id in run.DeployedIds) { if (id == member.Id) break; index++; }
                Rect badge = new Rect(foot.x - 10, foot.y - 124, 20, 20);
                Fill(badge, Panel); supportNumber.normal.textColor = color;
                GUI.Label(badge, (index + 1).ToString(), supportNumber);
            }
        }

        bool DrawPreparationSupportSummary()
        {
            CatalogMember source = PreparationSelection();
            CatalogSupportPreview support = SelectedSupport(source);
            if (source == null || support == null || showHelp || CurrentTutorialStep() != ExpeditionTutorialStep.None)
                return false;
            int inside = 0, others = run.DeployedIds.Count - 1;
            foreach (string id in run.DeployedIds)
            {
                CatalogMember target = run.Roster.Find(id);
                if (target != source && IsSupportCandidate(source, target, support)) inside++;
            }
            GUI.Label(new Rect(735, 143, 494, 31),
                "동료 범위 안 " + inside + "/" + others + "명  ·  " +
                (others == 0 ? "출전 동료 없음" : inside < others ? "범위 밖 " + (others - inside) + "명" : "모두 닿음"), rangeCaption);
            for (int i = 0; i < run.DeployedIds.Count; i++)
            {
                CatalogMember target = run.Roster.Find(run.DeployedIds[i]);
                bool self = source == target, eligible = IsSupportCandidate(source, target, support);
                Color color = self ? Gold : eligible ? SupportColor : OutsideColor;
                Rect card = SupportLayout.Card(i);
                Fill(card, new Color(color.r * .12f, color.g * .12f, color.b * .12f, .95f));
                Fill(new Rect(card.x, card.y, card.width, 2), color);
                if (art != null) art.Draw(target.FormId, new Rect(card.x + 31, card.y + 3, 32, 30), 0);
                supportNumber.normal.textColor = color;
                GUI.Label(new Rect(card.x + 4, card.y + 4, 20, 20), (i + 1).ToString(), supportNumber);
                GUI.Label(SupportLayout.Name(i), database.Get(target.FormId).Name, supportName);
                supportStatus.normal.textColor = color;
                GUI.Label(SupportLayout.Status(i), self ? "자신 포함" : eligible ? "범위 안" : "범위 밖", supportStatus);
            }
            return true;
        }

        bool DrawSelectedPreparation()
        {
            CatalogMember member = PreparationSelection();
            if (member == null || showHelp) return false;
            CatalogSupportPreview primarySupport = CatalogPreparationPreview.ForSkill(CatalogPreparationPreview.PrimarySkill(member));
            bool twoSkills = member.FormId.StartsWith("F-") && primarySupport != null &&
                CatalogPreparationPreview.ForSkill(member.FormId) != null;
            GUI.Label(new Rect(735, 326, 280, 31), database.Get(member.FormId).Name, heading);
            if (GUI.Button(new Rect(1021, 326, 100, 36), "상세", button))
            { selectedId = member.Id; modal = Modal.Detail; }
            if (GUI.Button(new Rect(1129, 326, 100, 36), "해제", button))
            { preparationSelectionId = null; return true; }
            if (twoSkills)
            {
                if (GUI.Button(new Rect(735, 364, 240, 34), "주 기술 지원" + (!previewFinalSkill ? " · 선택" : ""),
                    !previewFinalSkill ? primary : button)) previewFinalSkill = false;
                if (GUI.Button(new Rect(983, 364, 246, 34), "최종 기술 지원" + (previewFinalSkill ? " · 선택" : ""),
                    previewFinalSkill ? primary : button)) previewFinalSkill = true;
            }
            CatalogSupportPreview support = SelectedSupport(member);
            int candidates = 0;
            foreach (string id in run.DeployedIds)
            {
                CatalogMember target = run.Roster.Find(id);
                if (target != null && target.Id != member.Id && IsSupportCandidate(member, target, support)) candidates++;
            }
            string reach = support == null ? "기술 지원 없음 · 특성 효과는 별도" :
                (member.FormId.StartsWith("F-") && previewFinalSkill ? "최종 지원 " : "지원 ") +
                (support.Global ? "전체" : support.Radius.ToString("0.0")) +
                " · 동료 범위 안 " + candidates + "명";
            if (!twoSkills)
                GUI.Label(new Rect(735, 368, 494, 30),
                    "기본 " + database.Get(member.FormId).Range.ToString("0.0") + "   |   " + reach, rangeCaption);
            string rule = support == null ? "금색은 기본 사거리 기준입니다.\n근접 공격은 몸체 접촉도 고려합니다." :
                support.Rule + "\n" + (twoSkills ? (support.Global ? "전체" : "거리 " + support.Radius.ToString("0.0")) +
                    " · 동료 범위 안 " + candidates + "명 · " : "") + "현재 배치 기준 · 시전 때 대상 결정";
            GUI.Label(new Rect(735, 401, 494, 63), rule, rangeRule);
            return true;
        }
    }
}
