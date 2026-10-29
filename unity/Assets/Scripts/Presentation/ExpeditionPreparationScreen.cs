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
            public static Rect Card(int index) { return new Rect(924,328+index*35,312,32); }
            public static Rect Name(int index) { Rect r=Card(index);return new Rect(r.x+66,r.y+2,170,28); }
            public static Rect Status(int index) { Rect r=Card(index);return new Rect(r.x+239,r.y+4,70,24); }
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
                return PreparationLayout.World(pointerCurrent);
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
            Rect bounds = new Rect(center.x - radius * PreparationLayout.ScaleX, center.y - radius * GroundYScale,
                radius * PreparationLayout.ScaleX * 2f, radius * GroundYScale * 2f);
            PreviewTexture(preparationDisc, bounds, new Color(color.r, color.g, color.b, .065f));
            PreviewTexture(preparationRing, bounds, color);
        }

        void DrawPreparationRange()
        {
            EnsurePreparationArt();
            CatalogMember member = PreparationSelection();
            if (member == null)
            {
                GUI.Label(new Rect(40, 159, 826, 28), "동료를 눌러 범위 확인 · 끌어서 배치", rangeCaption);
                return;
            }
            GUI.Label(new Rect(40, 159, 826, 28),
                SelectedSupport(member) == null ? "금색: 기본 공격 사거리 · 끌어서 배치" :
                "금색: 선택 / 공격   ·   청록: 지원 범위   ·   번호: 동료 목록", rangeCaption);
            Rect clip = PreparationLayout.Board;
            Vector2 position = PreparationPosition(member);
            Vector2 center = PreparationLayout.Point(position.x, position.y) - clip.position;
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
                PreviewTexture(preparationDisc, new Rect(foot.x - 4, foot.y - 140, 8, 8), Gold);
            else if (eligible)
            {
                Fill(new Rect(foot.x - 45, foot.y - 136, 3, 16), color);
                Fill(new Rect(foot.x - 45, foot.y - 136, 13, 3), color);
                Fill(new Rect(foot.x + 42, foot.y - 136, 3, 16), color);
                Fill(new Rect(foot.x + 32, foot.y - 136, 13, 3), color);
            }
            else
            {
                // Broken brackets distinguish outside-range allies without relying only on color.
                Fill(new Rect(foot.x - 45, foot.y - 133, 3, 7), color);
                Fill(new Rect(foot.x + 42, foot.y - 133, 3, 7), color);
            }
            if (support != null && !showHelp && CurrentTutorialStep() == ExpeditionTutorialStep.None)
            {
                int index = 0;
                foreach (string id in run.DeployedIds) { if (id == member.Id) break; index++; }
                Rect badge = new Rect(foot.x - 10, foot.y + 5, 20, 20);
                Fill(badge, Panel); supportNumber.normal.textColor = color;
                GUI.Label(badge, (index + 1).ToString(), supportNumber);
            }
        }

    }
}
