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
                result += CatalogText.Get("text.0837") + database.Get(member.BranchId).Name + "\n" + CatalogUnitInfo.Skill(member.BranchId, run.RulesVersion);
            return result + CatalogText.Get("text.0838") + CatalogUnitInfo.Placement(member.FormId) + "\n\n" + CatalogUnitInfo.Keywords;
        }

        string UnitEffects(CatalogMember member)
        {
            string innate = database.Get(member.FormId).InnateTrait;
            var result = new StringBuilder(CatalogText.Get("text.0839") + TraitName(innate) + "\n" + CatalogUnitInfo.Trait(innate));
            result.Append(CatalogText.Get("text.0840")).Append(TraitName(member.InheritedTraitId)).Append('\n').Append(CatalogUnitInfo.Trait(member.InheritedTraitId));
            if (innate == member.InheritedTraitId) result.Append(CatalogText.Get("text.0841"));
            bool any = false;
            foreach (string id in run.LegacyResearchIds)
            {
                CatalogLegacyResearchNode node = CatalogLegacyResearch.Get(id);
                if (node.Tier != 2 || !EffectCanApply(id, member)) continue;
                result.Append(CatalogText.Get("text.0842")).Append(node.Description).Append('\n').Append(EffectCondition(id)); any = true;
            }
            foreach (string id in run.TacticIds)
            {
                if (!EffectCanApply(id, member)) continue;
                result.Append(CatalogText.Get("text.0842")).Append(CatalogResearch.Get(id).DescriptionForRules(run.RulesVersion)).Append('\n').Append(EffectCondition(id)); any = true;
            }
            if (!any) result.Append(CatalogText.Get("text.0843"));
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
                StatLine(CatalogText.Get("text.0844"), stats.MaxHp, before.MaxHp, "", compare),
                StatLine(CatalogText.Get("text.0845"), stats.Power, before.Power, "", compare),
                StatLine(CatalogText.Get("text.0846"), stats.Defense, before.Defense, "", compare),
                StatLine(CatalogText.Get("text.0847"), stats.AttackInterval, before.AttackInterval, CatalogText.Get("text.0011"), compare),
                StatLine(CatalogText.Get("text.0848"), stats.Range, before.Range, "", compare)
            };
            for (int i = 0; i < rows.Length; i++)
                LocalizedGui.Label(new Rect(area.x + 16, sy + i * 30, leftWidth - 30, 30), rows[i], infoText);
            LocalizedGui.Label(new Rect(area.x + 16, sy + 152, leftWidth - 30, 24), CatalogText.Get("text.0849"), infoCaption);

            float x = area.x + leftWidth + 24, w = area.xMax - x;
            LocalizedGui.Label(new Rect(x, area.y, w, 40), database.Get(member.FormId).Name, infoName);
            LocalizedGui.Label(new Rect(x, area.y + 44, w, 30), CatalogUnitInfo.Role(member.FormId), infoText);
            Rect tabs = new Rect(x, area.y + 82, w, 44);
            for (int i = 0; i < 3; i++)
                if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(tabs, i, 3),
                    (infoTabIndex == i ? "✓  " : "") + (i == 0 ? CatalogText.Get("text.0850") : i == 1 ? CatalogText.Get("text.0851") : CatalogText.Get("text.0852")), infoTab))
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
            LocalizedGui.Label(new Rect(0, 0, textWidth, textHeight), text, infoText);
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
            LocalizedGui.Label(new Rect(content.x, 60, 780, 40), CatalogText.Get("text.0853"), heading);
            if (LocalizedGui.Button(new Rect(content.xMax - 62, 60, 62, 48), "×", button)) { modal = Modal.None; return; }
            DrawUnitProfile(new Rect(content.x, 120, content.width, 326), UnitPreview(member), false);
            int target = member.FormId.StartsWith("B-", StringComparison.Ordinal) ? 2 : member.FormId.StartsWith("E-", StringComparison.Ordinal) ? 6 : 0;
            LocalizedGui.Label(new Rect(content.x, 463, content.width, 27),
                (target == 0 ? CatalogText.Get("text.0854") : CatalogText.Get("text.0785") + member.Growth + " / " + target) +
                CatalogText.Get("text.0855"), infoCaption);
            Rect row = new Rect(content.x, 512, content.width, 58);
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(row, 0, 3), IsDeployed(member.Id) ? CatalogText.Get("text.0856") : CatalogText.Get("text.0857"), button))
            {
                if (run.SetDeployed(member.Id, !IsDeployed(member.Id))) { Save("deployment", member.Id); modal = Modal.None; }
                else Notice(CatalogText.Get("text.0858"));
            }
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(row, 1, 3), CatalogText.Get("text.0859"), button))
            { inheritTrait = false; choosePage = 0; modal = Modal.ChooseDonor; }
            GUI.enabled = target > 0 && member.Growth >= target;
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(row, 2, 3), CatalogText.Get("text.0860"), primary)) { infoTabIndex = 0; PrepareEvolution(member); }
            GUI.enabled = true;
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 0, 3), member.MaterialLocked ? CatalogText.Get("text.0861") : CatalogText.Get("text.0862"), button))
            {
                if (run.SetMaterialLocked(member.Id, !member.MaterialLocked)) Save("material_lock", member.Id);
                else Notice(CatalogText.Get("text.0863"));
            }
            GUI.enabled = member.FormId.StartsWith("E-", StringComparison.Ordinal);
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 1, 3), CatalogText.Get("text.0864"), button)) { choosePage = 0; modal = Modal.ChoosePartner; infoTabIndex = 0; }
            GUI.enabled = true;
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 2, 3), CatalogText.Get("text.0865"), button)) OpenMemberEffects(member);
        }

        bool UnitChoiceCard(Rect rect, string formId, string recipe, bool selected = false)
        {
            EnsureInfoStyles();
            bool clicked = LocalizedGui.Button(rect, GUIContent.none, button);
            if (selected) ExpeditionUiTheme.Rule(new Rect(rect.x + 5, rect.y + 5, 4, rect.height - 10));
            if (art != null) art.Draw(formId, new Rect(rect.x + 16, rect.y + 8, 78, rect.height - 16), (int)(Time.unscaledTime * 5) % 4);
            float x = rect.x + 110, width = rect.width - 128;
            LocalizedGui.Label(new Rect(x, rect.y + 6, width, 40), (selected ? "✓ " : "") + database.Get(formId).Name, infoName);
            LocalizedGui.Label(new Rect(x, rect.y + 48, width, 31), CatalogUnitInfo.Summary(formId), infoText);
            if (!string.IsNullOrEmpty(recipe)) LocalizedGui.Label(new Rect(x, rect.y + 81, width, 27), recipe, infoCaption);
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
            LocalizedGui.Label(new Rect(content.x, 77, content.width - 72, 44), CatalogText.Get("text.0866") + options.Count + CatalogText.Get("text.0867"), heading);
            if (LocalizedGui.Button(new Rect(content.xMax - 62, 77, 62, 48), "×", button)) { modal = partner ? Modal.Detail : Modal.None; return; }
            LocalizedGui.Label(new Rect(content.x, 135, content.width, 28), CatalogText.Get("text.0868"), small);
            const int pageSize = 3;
            int page = partner ? choosePage : combinationPage;
            int pages = Mathf.Max(1, (options.Count + pageSize - 1) / pageSize);
            page = Mathf.Clamp(page, 0, pages - 1);
            for (int row = 0; row < pageSize && page * pageSize + row < options.Count; row++)
            {
                int index = page * pageSize + row;
                CatalogCombinationOption option = options[index];
                CatalogMember recipient = run.Roster.Find(option.RecipientId), donor = run.Roster.Find(option.DonorId);
                string recipe = CatalogText.Get("text.0869") + MemberPositionLabel(recipient.Id) + " " + database.Get(recipient.FormId).Name +
                    CatalogText.Get("text.0870") + MemberPositionLabel(donor.Id) + " " + database.Get(donor.FormId).Name;
                if (UnitChoiceCard(new Rect(content.x, 181 + row * 124, content.width, 112), option.ResultId, recipe))
                {
                    if (!partner) ChooseCombinationOption(index);
                    else { growthPreviewKey = null; pendingOtherId = donor.Id; pendingFormId = option.ResultId; pendingAction = GrowthAction.Combine; combinationFromList = false; modal = Modal.Confirm; }
                    infoTabIndex = 0;
                }
            }
            if (options.Count == 0) LocalizedGui.Label(new Rect(content.x, 220, content.width, 80), CatalogText.Get("text.0724"), body);
            ExpeditionUiTheme.FooterRule(panel);
            GUI.enabled = page > 0;
            if (LocalizedGui.Button(new Rect(footer.x, footer.y, 140, 64), CatalogText.Get("text.0510"), button)) page--;
            GUI.enabled = true;
            LocalizedGui.Label(new Rect(footer.x + 154, footer.y + 17, 110, 30), (page + 1) + " / " + pages, centered);
            GUI.enabled = page < pages - 1;
            if (LocalizedGui.Button(new Rect(footer.x + 280, footer.y, 140, 64), CatalogText.Get("text.0330"), button)) page++;
            GUI.enabled = true;
            if (LocalizedGui.Button(new Rect(footer.xMax - 210, footer.y, 210, 64), CatalogText.Get("text.0871"), button)) modal = partner ? Modal.Detail : Modal.None;
            if (partner) choosePage = page; else combinationPage = page;
        }

        void DrawEvolutionConfirmation() { DrawGrowthPreview(false); }
        void DrawCombinationConfirmation() { DrawGrowthPreview(true); }
        void DrawGrowthPreview(bool combine)
        {
            CatalogMember member = run.Roster.Find(selectedId), donor = combine ? run.Roster.Find(pendingOtherId) : null;
            if (member == null || combine && donor == null) { modal = Modal.None; return; }
            ExpeditionUnitPreview preview = UnitPreview(member, pendingFormId, combine ? pendingOtherId : null);
            if (preview == null) { modal = Modal.Detail; Notice(CatalogText.Get("text.0872")); return; }
            EnsureInfoStyles();
            string viewKey = selectedId + ":" + pendingOtherId + ":" + pendingAction;
            if (growthPreviewKey != viewKey) { growthPreviewKey = viewKey; growthDetails = false; }
            Rect panel = new Rect(130, 28, 1020, 664), content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            LocalizedGui.Label(new Rect(content.x, 60, content.width, 42), combine ? CatalogText.Get("text.0873") : CatalogText.Get("text.0874"), heading);
            // The introduction occupies this header; its acknowledgement must receive the click.
            if (!GrowthPreviewHeaderCovered() && LocalizedGui.Button(new Rect(954,62,164,42),growthDetails?CatalogText.Get("text.0875"):CatalogText.Get("text.0876"),button))
            { growthDetails=!growthDetails; infoScroll=Vector2.zero; }
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
                LocalizedGui.Label(new Rect(content.x, 118, content.width, 37), recipe, infoText);
                LocalizedGui.Label(new Rect(content.x, 163, content.width, 28), combine ?
                    CatalogText.Get("text.0877") :
                    CatalogText.Get("text.0878"), infoCaption);
            }
            if(growthDetails) DrawUnitProfile(new Rect(content.x, 212, content.width, 278), preview, true);
            else DrawGrowthOverview(new Rect(content.x,212,content.width,278),preview,donor);
            string note;
            if (combine)
                note = CatalogText.Get("text.0879") + database.Get(donor.FormId).Name + CatalogText.Get("text.0880") + TraitName(donor.InheritedTraitId) + CatalogText.Get("text.0881") +
                    CatalogText.Get("text.0882") + run.DeployedIds.Count + " → " + preview.DeployedCount + CatalogText.Get("text.0883") +
                    CatalogText.Get("text.0869") + database.Get(member.FormId).Name + CatalogText.Get("text.0884") + TraitName(member.InheritedTraitId) +
                    CatalogText.Get("text.0885");
            else note = CatalogText.Get("text.0886") + TraitName(database.Get(preview.Member.FormId).InnateTrait) +
                    CatalogText.Get("text.0880") + TraitName(member.InheritedTraitId) + CatalogText.Get("text.0887") +
                    (branch ? CatalogText.Get("text.0888") : CatalogText.Get("text.0889")) +
                    CatalogText.Get("text.0890");
            if (!growthDetails)
            {
                note = combine ? CatalogText.Get("text.0213") + run.DeployedIds.Count + " → " + preview.DeployedCount + CatalogText.Get("text.0891") + database.Get(donor.FormId).Name + CatalogText.Get("text.0892") +
                    CatalogText.Get("text.0893") + TraitName(member.InheritedTraitId) + CatalogText.Get("text.0894") + TraitName(donor.InheritedTraitId) :
                    CatalogText.Get("text.0895") + TraitName(member.InheritedTraitId) + CatalogText.Get("text.0896") + (branch ? CatalogText.Get("text.0888") : CatalogText.Get("text.0897"));
            }
            LocalizedGui.Label(new Rect(content.x,506,content.width,76),note,infoCaption);
            ExpeditionUiTheme.FooterRule(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2), CatalogText.Get("text.0898"), button))
                modal = combine ? combinationFromList ? Modal.CombinationOptions : Modal.Detail : evolutionFromDrag ? Modal.None : Modal.Detail;
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), combine ? CatalogText.Get("text.0899") : CatalogText.Get("text.0900"), primary)) ExecuteGrowth();
        }
    }
}
