using System;
using System.Collections.Generic;
using System.Text;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        ExpeditionRun infoRun;
        string infoKey;
        ExpeditionUnitPreview infoCurrent, infoResult;
        GUIStyle infoText, infoCaption, infoName, infoTab;
        Vector2 infoScroll;
        int infoTabIndex;
        bool infoDragging;

        void EnsureInfoStyles()
        {
            if (infoText != null) return;
            infoText = new GUIStyle(body) { fontSize = 19, wordWrap = true, alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(0, 0, 0, 0) };
            infoCaption = new GUIStyle(infoText) { fontSize = 16 };
            infoCaption.normal.textColor = ExpeditionUiTheme.Muted;
            infoName = new GUIStyle(infoText) { fontSize = 25, fontStyle = FontStyle.Bold };
            infoName.normal.textColor = Gold;
            infoTab = new GUIStyle(button) { fontSize = 18 };
        }

        ExpeditionUnitPreview UnitPreview(CatalogMember member, string result = null, string donor = null)
        {
            var key = new StringBuilder();
            key.Append(member.Id).Append('|').Append(result).Append('|').Append(donor).Append('|').Append(run.Phase);
            foreach (CatalogMember item in run.Roster.Members)
                key.Append('|').Append(item.Id).Append(':').Append(item.FormId).Append(':').Append(item.BranchId)
                    .Append(':').Append(item.Growth).Append(':').Append(item.InheritedTraitId).Append(':').Append(item.MaterialLocked);
            foreach (string id in run.DeployedIds) key.Append('|').Append(id);
            foreach (string id in run.TacticIds) key.Append('|').Append(id);
            foreach (string id in run.LegacyResearchIds) key.Append('|').Append(id);
            string current = key.ToString();
            if (infoRun != run || infoKey != current)
            {
                infoRun = run; infoKey = current;
                infoCurrent = ExpeditionUnitPreview.Create(database, run, member.Id);
                infoResult = result == null ? infoCurrent : ExpeditionUnitPreview.Create(database, run, member.Id, result, donor);
                infoScroll = Vector2.zero; infoDragging = false;
            }
            return infoResult;
        }

        static string InfoNumber(float value) { return value.ToString("0.##"); }
        string StatLine(string label, float value, float before, string suffix, bool compare)
        {
            return label + "  " + (compare && Math.Abs(value - before) > .001f ?
                InfoNumber(before) + " → " : "") + InfoNumber(value) + suffix;
        }

        string UnitSkills(CatalogMember member)
        {
            string result = CatalogUnitInfo.Skill(member.FormId, run.RulesVersion);
            if (member.FormId.StartsWith("F-", StringComparison.Ordinal))
                result += "\n\n유지되는 분기 기술 · " + database.Get(member.BranchId).Name + "\n" + CatalogUnitInfo.Skill(member.BranchId, run.RulesVersion);
            return result + "\n\n배치: " + CatalogUnitInfo.Placement(member.FormId) + "\n\n" + CatalogUnitInfo.Keywords;
        }

        string UnitEffects(CatalogMember member)
        {
            string innate = database.Get(member.FormId).InnateTrait;
            var result = new StringBuilder("고유 · " + TraitName(innate) + "\n" + CatalogUnitInfo.Trait(innate));
            result.Append("\n\n전승 · ").Append(TraitName(member.InheritedTraitId)).Append('\n').Append(CatalogUnitInfo.Trait(member.InheritedTraitId));
            if (innate == member.InheritedTraitId) result.Append("\n동일 특성은 중복 적용되지 않습니다.");
            bool any = false;
            foreach (string id in run.LegacyResearchIds)
            {
                CatalogLegacyResearchNode node = CatalogLegacyResearch.Get(id);
                if (node.Tier != 2 || !EffectCanApply(id, member)) continue;
                result.Append("\n\n연구 · ").Append(node.Description).Append('\n').Append(EffectCondition(id)); any = true;
            }
            foreach (string id in run.TacticIds)
            {
                if (!EffectCanApply(id, member)) continue;
                result.Append("\n\n연구 · ").Append(CatalogResearch.Get(id).DescriptionForRules(run.RulesVersion)).Append('\n').Append(EffectCondition(id)); any = true;
            }
            if (!any) result.Append("\n\n적용 중인 전투 연구 없음");
            return result.ToString();
        }

        void DrawUnitProfile(Rect area, ExpeditionUnitPreview preview, bool compare)
        {
            EnsureInfoStyles();
            CatalogMember member = preview.Member;
            CatalogBattleUnit stats = preview.Stats, before = infoCurrent.Stats;
            const float leftWidth = 248;
            float portraitHeight = Mathf.Min(178, area.height - 178);
            ExpeditionUiTheme.PanelBox(new Rect(area.x, area.y, leftWidth, area.height));
            if (art != null) art.DrawPreview(member.FormId,
                new Rect(area.x + 36, area.y + 8, leftWidth - 72, portraitHeight - 10), Time.unscaledTime);
            float sy = area.y + portraitHeight;
            string[] rows = {
                StatLine("최대 체력", stats.MaxHp, before.MaxHp, "", compare),
                StatLine("공격력", stats.Power, before.Power, "", compare),
                StatLine("방어력", stats.Defense, before.Defense, "", compare),
                StatLine("공격 간격", stats.AttackInterval, before.AttackInterval, "초", compare),
                StatLine("사거리", stats.Range, before.Range, "", compare)
            };
            for (int i = 0; i < rows.Length; i++)
                GUI.Label(new Rect(area.x + 16, sy + i * 30, leftWidth - 30, 30), rows[i], infoText);
            GUI.Label(new Rect(area.x + 16, sy + 152, leftWidth - 30, 24), "전투 시작 기준 · 체력 효과 반영", infoCaption);

            float x = area.x + leftWidth + 24, w = area.xMax - x;
            GUI.Label(new Rect(x, area.y, w, 40), database.Get(member.FormId).Name, infoName);
            GUI.Label(new Rect(x, area.y + 44, w, 30), CatalogUnitInfo.Role(member.FormId), infoText);
            Rect tabs = new Rect(x, area.y + 82, w, 44);
            for (int i = 0; i < 3; i++)
                if (GUI.Button(ExpeditionUiTheme.FooterButton(tabs, i, 3),
                    (infoTabIndex == i ? "✓  " : "") + (i == 0 ? "기술" : i == 1 ? "특성 · 연구" : "강점 · 주의점"), infoTab))
                { infoTabIndex = i; infoScroll = Vector2.zero; }
            string text = infoTabIndex == 0 ? UnitSkills(member) : infoTabIndex == 1 ? UnitEffects(member) : CatalogUnitInfo.Decision(member.FormId);
            Rect viewport = new Rect(x, area.y + 138, w, area.height - 138);
            float textWidth = w - 34;
            float textHeight = infoText.CalcHeight(new GUIContent(text), textWidth) + 8;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && viewport.Contains(e.mousePosition)) infoDragging = true;
            if (e.type == EventType.MouseDrag && infoDragging)
            { infoScroll.y = Mathf.Clamp(infoScroll.y - e.delta.y, 0, Mathf.Max(0, textHeight - viewport.height)); e.Use(); }
            if (e.rawType == EventType.MouseUp) infoDragging = false;
            infoScroll = GUI.BeginScrollView(viewport, infoScroll, new Rect(0, 0, textWidth, textHeight), false, true);
            GUI.Label(new Rect(0, 0, textWidth, textHeight), text, infoText);
            GUI.EndScrollView();
        }

        void DrawDetail()
        {
            CatalogMember member = run == null ? null : run.Roster.Find(selectedId);
            if (member == null) { modal = Modal.None; return; }
            EnsureInfoStyles();
            Rect panel = new Rect(130, 28, 1020, 664);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 60, 780, 40), "동료 정보", heading);
            if (GUI.Button(new Rect(content.xMax - 62, 60, 62, 48), "×", button)) { modal = Modal.None; return; }
            DrawUnitProfile(new Rect(content.x, 120, content.width, 326), UnitPreview(member), false);
            int target = member.FormId.StartsWith("B-", StringComparison.Ordinal) ? 2 : member.FormId.StartsWith("E-", StringComparison.Ordinal) ? 6 : 0;
            GUI.Label(new Rect(content.x, 463, content.width, 27),
                (target == 0 ? "최종 형태" : "진화 진행 " + member.Growth + " / " + target) +
                "  ·  조건부 피해 보너스는 특성·연구 효과에서 확인", infoCaption);
            Rect row = new Rect(content.x, 512, content.width, 58);
            if (GUI.Button(ExpeditionUiTheme.FooterButton(row, 0, 3), IsDeployed(member.Id) ? "대기석으로" : "출전시키기", button))
            {
                if (run.SetDeployed(member.Id, !IsDeployed(member.Id))) { Save("deployment", member.Id); modal = Modal.None; }
                else Notice("출전 1~5명 · 대기석 최대 7명을 유지해야 합니다.");
            }
            if (GUI.Button(ExpeditionUiTheme.FooterButton(row, 1, 3), "전승", button))
            { inheritTrait = false; choosePage = 0; modal = Modal.ChooseDonor; }
            GUI.enabled = target > 0 && member.Growth >= target;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(row, 2, 3), "진화 미리보기", primary)) { infoTabIndex = 0; PrepareEvolution(member); }
            GUI.enabled = true;
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 0, 3), member.MaterialLocked ? "재료 보호 해제" : "재료 보호", button))
            {
                if (run.SetMaterialLocked(member.Id, !member.MaterialLocked)) Save("material_lock", member.Id);
                else Notice("재료 보호 상태를 바꾸지 못했습니다.");
            }
            GUI.enabled = member.FormId.StartsWith("E-", StringComparison.Ordinal);
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 1, 3), "결합 미리보기", button)) { choosePage = 0; modal = Modal.ChoosePartner; infoTabIndex = 0; }
            GUI.enabled = true;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 2, 3), "적용 효과", button)) OpenMemberEffects(member);
        }

        bool UnitChoiceCard(Rect rect, string formId, string recipe, bool selected = false)
        {
            EnsureInfoStyles();
            bool clicked = GUI.Button(rect, GUIContent.none, button);
            if (selected) ExpeditionUiTheme.Rule(new Rect(rect.x + 5, rect.y + 5, 4, rect.height - 10));
            if (art != null) art.Draw(formId, new Rect(rect.x + 16, rect.y + 8, 78, rect.height - 16), (int)(Time.unscaledTime * 5) % 4);
            float x = rect.x + 110, width = rect.width - 128;
            GUI.Label(new Rect(x, rect.y + 6, width, 40), (selected ? "✓ " : "") + database.Get(formId).Name, infoName);
            GUI.Label(new Rect(x, rect.y + 48, width, 31), CatalogUnitInfo.Summary(formId), infoText);
            if (!string.IsNullOrEmpty(recipe)) GUI.Label(new Rect(x, rect.y + 81, width, 27), recipe, infoCaption);
            return clicked;
        }

        void DrawCombinationOptions() { DrawCombinationChoices(false); }
        void DrawPartnerChoices() { DrawCombinationChoices(true); }
        void DrawCombinationChoices(bool partner)
        {
            List<CatalogCombinationOption> options = new List<CatalogCombinationOption>();
            if (partner)
            {
                CatalogMember selected = run.Roster.Find(selectedId);
                if (selected == null) { modal = Modal.None; return; }
                foreach (CatalogMember donor in run.Roster.Members)
                {
                    string result = run.PreviewCombination(donor.Id, selectedId);
                    if (result != null) options.Add(new CatalogCombinationOption(selectedId, donor.Id, result));
                }
            }
            else options = run.GetCombinationOptions();
            Rect panel = new Rect(160, 45, 960, 630), content = ExpeditionUiTheme.ContentRect(panel), footer = ExpeditionUiTheme.FooterRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 77, content.width - 72, 44), "가능한 결합 · " + options.Count + "쌍", heading);
            if (GUI.Button(new Rect(content.xMax - 62, 77, 62, 48), "×", button)) { modal = partner ? Modal.Detail : Modal.None; return; }
            GUI.Label(new Rect(content.x, 135, content.width, 28), "결과를 누르면 상세 능력과 소모되는 동료를 확인합니다.", small);
            const int pageSize = 3;
            int page = partner ? choosePage : combinationPage;
            int pages = Mathf.Max(1, (options.Count + pageSize - 1) / pageSize);
            page = Mathf.Clamp(page, 0, pages - 1);
            for (int row = 0; row < pageSize && page * pageSize + row < options.Count; row++)
            {
                int index = page * pageSize + row;
                CatalogCombinationOption option = options[index];
                CatalogMember recipient = run.Roster.Find(option.RecipientId), donor = run.Roster.Find(option.DonorId);
                string recipe = "유지: " + MemberPositionLabel(recipient.Id) + " " + database.Get(recipient.FormId).Name +
                    "  +  소모: " + MemberPositionLabel(donor.Id) + " " + database.Get(donor.FormId).Name;
                if (UnitChoiceCard(new Rect(content.x, 181 + row * 124, content.width, 112), option.ResultId, recipe))
                {
                    if (!partner) ChooseCombinationOption(index);
                    else { growthPreviewKey = null; pendingOtherId = donor.Id; pendingFormId = option.ResultId; pendingAction = GrowthAction.Combine; combinationFromList = false; modal = Modal.Confirm; }
                    infoTabIndex = 0;
                }
            }
            if (options.Count == 0) GUI.Label(new Rect(content.x, 220, content.width, 80), "지금 결합할 수 있는 동료가 없습니다.", body);
            ExpeditionUiTheme.FooterRule(panel);
            GUI.enabled = page > 0;
            if (GUI.Button(new Rect(footer.x, footer.y, 140, 64), "이전", button)) page--;
            GUI.enabled = true;
            GUI.Label(new Rect(footer.x + 154, footer.y + 17, 110, 30), (page + 1) + " / " + pages, centered);
            GUI.enabled = page < pages - 1;
            if (GUI.Button(new Rect(footer.x + 280, footer.y, 140, 64), "다음", button)) page++;
            GUI.enabled = true;
            if (GUI.Button(new Rect(footer.xMax - 210, footer.y, 210, 64), "나중에", button)) modal = partner ? Modal.Detail : Modal.None;
            if (partner) choosePage = page; else combinationPage = page;
        }

        void DrawEvolutionConfirmation() { DrawGrowthPreview(false); }
        void DrawCombinationConfirmation() { DrawGrowthPreview(true); }
        void DrawGrowthPreview(bool combine)
        {
            CatalogMember member = run.Roster.Find(selectedId), donor = combine ? run.Roster.Find(pendingOtherId) : null;
            if (member == null || combine && donor == null) { modal = Modal.None; return; }
            ExpeditionUnitPreview preview = UnitPreview(member, pendingFormId, combine ? pendingOtherId : null);
            if (preview == null) { modal = Modal.Detail; Notice("지금 진행할 수 없는 성장입니다."); return; }
            EnsureInfoStyles();
            string viewKey = selectedId + ":" + pendingOtherId + ":" + pendingAction;
            if (growthPreviewKey != viewKey) { growthPreviewKey = viewKey; growthDetails = false; }
            Rect panel = new Rect(130, 28, 1020, 664), content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 60, content.width, 42), combine ? "결합 결과 확인" : "진화 결과 선택", heading);
            if (GUI.Button(new Rect(954,62,164,42),growthDetails?"모습 비교":"상세 능력",button)) { growthDetails=!growthDetails; infoScroll=Vector2.zero; }
            bool branch = !combine && member.FormId.StartsWith("B-", StringComparison.Ordinal);
            if (branch)
            {
                int family = Array.IndexOf(Families, CatalogRoster.FamilyCode(member.FormId));
                for (int i = 0; i < 2; i++)
                {
                    string id = Branches[family][i];
                    if (UnitChoiceCard(new Rect(content.x + i * 486, 112, 470, 88), id, null, pendingFormId == id))
                    { pendingFormId = id; preview = UnitPreview(member, id); infoTabIndex = 0; }
                }
            }
            else
            {
                string recipe = database.Get(member.FormId).Name +
                    (combine ? " + " + database.Get(donor.FormId).Name : "") + " → " + database.Get(pendingFormId).Name;
                GUI.Label(new Rect(content.x, 118, content.width, 37), recipe, infoText);
                GUI.Label(new Rect(content.x, 163, content.width, 28), combine ?
                    "수치 비교는 남는 동료 1명 기준입니다. 두 명의 전투력을 합산한 값이 아닙니다." :
                    "선택했던 분기 기술을 유지하고 최종 기술을 추가합니다.", infoCaption);
            }
            if(growthDetails) DrawUnitProfile(new Rect(content.x, 212, content.width, 278), preview, true);
            else DrawGrowthOverview(new Rect(content.x,212,content.width,278),preview,donor);
            string note;
            if (combine)
                note = "소모: " + database.Get(donor.FormId).Name + " · 전승 " + TraitName(donor.InheritedTraitId) + "도 사라짐" +
                    "  |  출전 " + run.DeployedIds.Count + " → " + preview.DeployedCount + "명\n" +
                    "유지: " + database.Get(member.FormId).Name + "의 전승 " + TraitName(member.InheritedTraitId) +
                    "  |  두 동료의 기존 기술·고유 특성은 결과 형태로 교체\n준비 중 되돌리기 가능 · 현재 공격력에 대한 계수이며 실제 피해는 적 방어와 조건에 따라 달라집니다.";
            else note = "고유 특성 " + TraitName(database.Get(preview.Member.FormId).InnateTrait) +
                    " · 전승 " + TraitName(member.InheritedTraitId) + " 유지  |  출전 인원 유지\n" +
                    (branch ? "기본 기술이 선택한 분기 기술로 바뀝니다." : "선택한 분기 기술과 새 최종 기술을 함께 사용합니다.") +
                    " 준비 중 되돌리기 가능.\n피해 계수는 현재 공격력 기준 · 일시 강화와 적 방어력은 스탯에 포함하지 않습니다.";
            if (!growthDetails)
            {
                note = combine ? "출전 " + run.DeployedIds.Count + " → " + preview.DeployedCount + "명  ·  " + database.Get(donor.FormId).Name + " 소모  ·  빈자리는 대기 동료로 보충 가능\n" +
                    "두 동료의 기존 기술·고유 특성은 교체됩니다. 유지 전승: " + TraitName(member.InheritedTraitId) + " / 사라지는 전승: " + TraitName(donor.InheritedTraitId) :
                    "출전 인원 유지 · 전승 " + TraitName(member.InheritedTraitId) + " 유지\n" + (branch ? "기본 기술이 선택한 분기 기술로 바뀝니다." : "분기 기술을 유지하고 최종 기술을 추가합니다.");
            }
            GUI.Label(new Rect(content.x,506,content.width,76),note,infoCaption);
            ExpeditionUiTheme.FooterRule(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2), "취소", button))
                modal = combine ? combinationFromList ? Modal.CombinationOptions : Modal.Detail : evolutionFromDrag ? Modal.None : Modal.Detail;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), combine ? "결합하기" : "이 형태로 진화", primary)) ExecuteGrowth();
        }
    }
}
