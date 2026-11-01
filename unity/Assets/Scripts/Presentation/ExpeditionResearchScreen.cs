using System;
using System.Collections.Generic;
using UnityEngine;
using Expedition.Core;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        GUIStyle runeName, runeCaption, runeBody, runeSmall;
        GUIStyle runeSection;
        Vector2 researchDetailScroll;
        string researchDetailId;
        bool researchDetailDragging;
        string lastUnlockedRune;
        float runePulseUntil;
        CatalogBattleUnit memberEffectPreview;
        static readonly Rect ResearchWindow = new Rect(44, 30, 1192, 660);
        static readonly Rect ResearchMap = new Rect(76, 159, 752, 413);
        static readonly Rect ResearchDetailViewport = new Rect(880, 267, 300, 283);

        void EnsureRuneStyles()
        {
            if (runeName != null) return;
            runeName = Style(18, Cream, true, TextAnchor.MiddleCenter);
            runeCaption = Style(14, Muted, false, TextAnchor.MiddleCenter);
            runeBody = Style(18, Cream, false, TextAnchor.UpperLeft);
            runeSmall = Style(15, Muted, false, TextAnchor.UpperLeft);
            runeSection = Style(16, Gold, false, TextAnchor.UpperLeft);
            foreach (GUIStyle style in new[] { runeName, runeCaption, runeBody, runeSmall, runeSection })
            {
                style.padding = new RectOffset(0, 0, 0, 0);
                style.margin = new RectOffset(0, 0, 0, 0);
                style.clipping = TextClipping.Overflow;
            }
        }

        void ResearchHeader(bool tactics, bool unavailable)
        {
            EnsureRuneStyles();
            ExpeditionUiTheme.Window(ResearchWindow);
            LocalizedGui.Label(new Rect(76, 61, 600, 42), tactics ? CatalogText.Get("text.0427") : CatalogText.Get("text.0427"), title);
            LocalizedGui.Label(new Rect(78, 111, 760, 30), tactics ?
                CatalogText.Get("text.0532") :
                CatalogText.Get("text.0533"), small);
            ExpeditionUiTheme.PanelBox(new Rect(936, 65, 268, 70));
            LocalizedGui.Label(new Rect(948, 70, 244, 29), tactics ? CatalogText.Get("text.0534") : CatalogText.Get("text.0535"), runeCaption);
            LocalizedGui.Label(new Rect(948, 97, 244, 28), unavailable ? CatalogText.Get("text.0536") : tactics ?
                run.Gold + " G  ·  " + run.TacticIds.Count + CatalogText.Get("text.0537") : progress.ResearchPoints +
                CatalogText.Get("text.0538") + progress.ResearchEarned, runeName);
            ExpeditionResearchBoard.Background(ResearchMap);
        }

        void DrawTactics()
        {
            if (run == null) { modal = Modal.None; return; }
            ResearchHeader(true, false);
            DrawResearchRoutes(true, false);
            ExpeditionResearchBoard.Center(run.TacticIds.Count, 3, "RESEARCH", runeName, runeCaption);
            for (int b = 0; b < 3; b++)
                for (int tier = 1; tier <= 2; tier++)
                {
                    CatalogResearchNode node = TacticNode(TacticBranches[b], tier);
                    if (node == null) continue;
                    if (ExpeditionResearchBoard.Node(ExpeditionResearchBoard.Position(b, tier), b * 2 + tier - 1,
                        tier, ExpeditionResearchBoard.BranchColor(b), node.Title, node.Cost + " G",
                        HasId(run.TacticIds, node.Id), run.CanPurchaseTactic(node.Id), selectedTacticNodeId == node.Id,
                        lastUnlockedRune == node.Id && Time.unscaledTime < runePulseUntil, runeName, runeCaption,
                        !run.HasResearchRegion(node) ? node.RequiredRegion + CatalogText.Get("text.0539") : CatalogText.Get("text.0529")))
                        selectedTacticNodeId = node.Id;
                }
            CatalogResearchNode selected = CatalogResearch.Get(selectedTacticNodeId) ?? CatalogResearch.Nodes[0];
            bool owned = HasId(run.TacticIds, selected.Id), canBuy = run.CanPurchaseTactic(selected.Id);
            int branch = Array.IndexOf(TacticBranches, selected.BranchId);
            DrawRuneDetail(selected.Title, branch * 2 + selected.Tier - 1, branch,
                owned ? CatalogText.Get("text.0540") : canBuy ? CatalogText.Get("text.0541") : CatalogText.Get("text.0542"));
            var affected = new List<CatalogMember>();
            foreach (string id in run.DeployedIds)
            {
                CatalogMember member = run.Roster.Find(id);
                if (member != null && EffectCanApply(selected.Id, member)) affected.Add(member);
            }
            string names = "";
            foreach (CatalogMember member in affected)
                names += (names.Length == 0 ? "" : " · ") + database.Get(member.FormId).Name;
            if (names.Length == 0) names = CatalogText.Get("text.0543");
            if (researchDetailId != selected.Id) { researchDetailId = selected.Id; researchDetailScroll = Vector2.zero; }
            string requirement = CatalogText.Format("research.requirement", selected.RequiredRegion, selected.RequiredBossWave,
                run.HasResearchRegion(selected) ? CatalogText.Get("text.0545") : CatalogText.Get("text.0546"), selected.Cost);
            string[] paragraphs = { selected.DescriptionForRules(run.RulesVersion), EffectCondition(selected.Id),
                CatalogText.Get("text.0547") + affected.Count + " / " + run.DeployedIds.Count, names,
                requirement, TacticLockReason(selected, owned, canBuy) };
            GUIStyle[] styles = { runeBody, runeSmall, runeSection, runeSmall, runeSection, runeSmall };
            float textWidth = ResearchDetailViewport.width - 24;
            float totalHeight = ResearchParagraphs(paragraphs, styles, textWidth, false);
            HandleResearchDetailDrag(totalHeight);
            researchDetailScroll = GUI.BeginScrollView(ResearchDetailViewport, researchDetailScroll,
                new Rect(0, 0, textWidth, totalHeight), false, totalHeight > ResearchDetailViewport.height);
            ResearchParagraphs(paragraphs, styles, textWidth, true);
            GUI.EndScrollView();
            ExpeditionUiTheme.FooterRule(ResearchWindow);
            Rect footer = ExpeditionUiTheme.FooterRect(ResearchWindow);
            bool enabled = GUI.enabled; GUI.enabled = canBuy;
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2),
                owned ? CatalogText.Get("text.0548") : CatalogText.Get("text.0549") + selected.Cost + " G", primary))
            {
                if (run.PurchaseTactic(selected.Id))
                { Save("tactic_purchase", selected.Id); lastUnlockedRune = selected.Id; runePulseUntil = Time.unscaledTime + 1.2f; }
            }
            GUI.enabled = enabled;
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), CatalogText.Get("text.0400"), button))
            { FinishLesson(ExpeditionLesson.Tactics); modal = Modal.None; }
        }

        void DrawResearchRoutes(bool tactics, bool unavailable)
        {
            for (int b = 0; b < 3; b++)
            {
                string parentId = tactics ? TacticNode(TacticBranches[b], 1).Id : LegacyNode(LegacyBranches[b], 1).Id;
                string childId = tactics ? TacticNode(TacticBranches[b], 2).Id : LegacyNode(LegacyBranches[b], 2).Id;
                bool parentOwned = !unavailable && HasId(tactics ? run.TacticIds : progress.ResearchIds, parentId);
                bool childOwned = !unavailable && HasId(tactics ? run.TacticIds : progress.ResearchIds, childId);
                string selected = tactics ? selectedTacticNodeId : selectedLegacyNodeId;
                Color color = ExpeditionResearchBoard.BranchColor(b);
                Vector2 first = ExpeditionResearchBoard.Position(b, 1), last = ExpeditionResearchBoard.Position(b, 2);
                ExpeditionResearchBoard.Path(ExpeditionResearchBoard.Core, first, color, parentOwned,
                    selected == parentId || selected == childId);
                ExpeditionResearchBoard.Path(first, last, color, childOwned, selected == childId);
            }
        }

        void DrawRuneDetail(string name, int glyph, int branch, string state)
        {
            ExpeditionUiTheme.PanelBox(new Rect(856, 159, 348, 413));
            Color color = ExpeditionResearchBoard.BranchColor(branch);
            ExpeditionUiTheme.Fill(new Rect(858, 160, 344, 3), color);
            ExpeditionResearchBoard.Glyph(new Rect(884, 181, 44, 44), glyph, Color.white);
            LocalizedGui.Label(new Rect(940, 176, 239, 25), state, small);
            LocalizedGui.Label(new Rect(940, 205, 239, 40), name, heading);
            ExpeditionUiTheme.Rule(new Rect(888, 254, 292, 1));
        }

        static float ResearchParagraphs(string[] text, GUIStyle[] styles, float width, bool draw)
        {
            float y = 0;
            for (int i = 0; i < text.Length; i++)
            {
                // Measure with the actual Android font and padding instead of assuming a line count.
                float height = Mathf.Ceil(styles[i].CalcHeight(new GUIContent(text[i]), width)) + 5;
                if (draw) LocalizedGui.Label(new Rect(0, y, width, height), text[i], styles[i]);
                y += height + (i == 1 || i == 3 ? 12 : 5);
            }
            return y;
        }

        void HandleResearchDetailDrag(float contentHeight)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown)
                researchDetailDragging = ResearchDetailViewport.Contains(e.mousePosition) &&
                    e.mousePosition.x < ResearchDetailViewport.xMax - 20;
            if (e.type == EventType.MouseDrag && researchDetailDragging && contentHeight > ResearchDetailViewport.height)
            {
                researchDetailScroll.y = Mathf.Clamp(researchDetailScroll.y - e.delta.y,
                    0, contentHeight - ResearchDetailViewport.height);
                e.Use();
            }
            if (e.rawType == EventType.MouseUp) researchDetailDragging = false;
        }

        string LegacyLockReason(CatalogLegacyResearchNode node, bool owned, bool available)
        {
            if (badProgress || progress == null) return CatalogText.Get("text.0550");
            if (owned) return CatalogText.Get("text.0551");
            if (available) return CatalogText.Get("text.0552") + node.Cost + CatalogText.Get("text.0553");
            if (node.PrerequisiteId != null && !HasId(progress.ResearchIds, node.PrerequisiteId))
                return CatalogText.Get("text.0554") + CatalogLegacyResearch.Get(node.PrerequisiteId).Title;
            return CatalogText.Get("text.0555") + (node.Cost - progress.ResearchPoints) + CatalogText.Get("text.0556");
        }

        string TacticLockReason(CatalogResearchNode node, bool owned, bool available)
        {
            if (owned) return CatalogText.Get("text.0557");
            if (available) return CatalogText.Get("text.0558") + (node.Cost / (float)ExpeditionRun.SummonCost).ToString("0.#") +
                CatalogText.Get("text.0559");
            if (run.Phase != ExpeditionPhase.Preparation) return CatalogText.Get("text.0560");
            if (!run.HasResearchRegion(node)) return CatalogText.Get("text.0561");
            if (node.PrerequisiteId != null && !HasId(run.TacticIds, node.PrerequisiteId))
                return CatalogText.Get("text.0554") + CatalogResearch.Get(node.PrerequisiteId).Title;
            if (run.TacticIds.Count >= CatalogResearch.MaxNodes) return CatalogText.Get("text.0562");
            return CatalogText.Get("text.0563") + (node.Cost - run.Gold) + CatalogText.Get("text.0564");
        }

        bool EffectCanApply(string id, CatalogMember member)
        {
            CatalogForm form = database.Get(member.FormId);
            if (id == "bulwark") return form.Range <= 1.5f;
            if (id == "marksmanship") return form.Range > 2.1f;
            if (id == "cinders") return form.InnateTrait == "T-FI" || member.InheritedTraitId == "T-FI" ||
                CatalogRoster.FamilyCode(member.FormId) == "FI" || member.FormId == "H-01" || member.FormId == "H-04";
            if (id == "spellcraft" || id == "ember-insight")
            {
                string skill = member.FormId.StartsWith("F-", StringComparison.Ordinal) ? member.BranchId : member.FormId;
                return skill != "B-PR" && skill != "B-BA" && skill != "E-KN-G" && skill != "E-PR-G" &&
                    skill != "E-PR-H" && skill != "E-BA-G" && skill != "E-BA-A" && skill != "H-05";
            }
            return true;
        }

        static string EffectCondition(string id)
        {
            switch (id)
            {
                case "shieldcraft": return CatalogText.Get("text.0565");
                case "exploitation": return CatalogText.Get("text.0566");
                case "spellcraft": case "ember-insight": return CatalogText.Get("text.0567");
                case "cinders": return CatalogText.Get("text.0568");
                case "marksmanship": return CatalogText.Get("text.0569");
                case "bulwark": return CatalogText.Get("text.0570");
                default: return CatalogText.Get("text.0571");
            }
        }

        void OpenMemberEffects(CatalogMember member)
        {
            CatalogBattle preview = new CatalogBattle(database, run.TacticIds, run.LegacyResearchIds, run.RulesVersion);
            memberEffectPreview = preview.AddUnit("preview", CatalogTeam.Ally, member.FormId, member.BranchId,
                member.InheritedTraitId, 0, 0);
            modal = Modal.Effects;
        }

        void DrawMemberEffects()
        {
            CatalogMember member = run == null ? null : run.Roster.Find(selectedId);
            if (member == null || memberEffectPreview == null) { modal = Modal.None; return; }
            EnsureRuneStyles();
            Rect panel = new Rect(180, 45, 920, 630);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            CatalogForm form = database.Get(member.FormId);
            LocalizedGui.Label(new Rect(content.x, 77, 840, 43), form.Name + CatalogText.Get("text.0572"), heading);
            LocalizedGui.Label(new Rect(content.x, 124, 840, 55), CatalogText.Get("text.0573") + form.Hp.ToString("0") + " → " + memberEffectPreview.MaxHp.ToString("0") +
                CatalogText.Get("text.0574") + form.Power.ToString("0") + CatalogText.Get("text.0575"), body);
            float y = 193;
            int shown = 0;
            foreach (string id in run.LegacyResearchIds)
            {
                CatalogLegacyResearchNode node = CatalogLegacyResearch.Get(id);
                if (node.Tier != 2 || !EffectCanApply(id, member)) continue;
                EffectRow(content.x, y, content.width, CatalogText.Get("text.0576"), node.Description, EffectCondition(id)); y += 57; shown++;
            }
            foreach (string id in run.TacticIds)
            {
                CatalogResearchNode node = CatalogResearch.Get(id);
                if (!EffectCanApply(id, member)) continue;
                EffectRow(content.x, y, content.width, CatalogText.Get("text.0576"), node.DescriptionForRules(run.RulesVersion), EffectCondition(id)); y += 57; shown++;
            }
            if (shown == 0) LocalizedGui.Label(new Rect(content.x, 213, content.width, 80),
                CatalogText.Get("text.0577"), body);
            LocalizedGui.Label(new Rect(content.x, 535, content.width, 23),
                CatalogText.Get("text.0578"), small);
            ExpeditionUiTheme.FooterRule(panel);
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterRect(panel), CatalogText.Get("text.0579"), button)) modal = Modal.Detail;
        }

        void EffectRow(float x, float y, float width, string kind, string effect, string condition)
        {
            ExpeditionUiTheme.Fill(new Rect(x, y, width, 51), new Color(.07f, .08f, .12f, .7f));
            LocalizedGui.Label(new Rect(x + 12, y + 8, 59, 29), kind, hint);
            LocalizedGui.Label(new Rect(x + 80, y + 3, width - 90, 26), effect, body);
            LocalizedGui.Label(new Rect(x + 80, y + 28, width - 90, 21), condition, small);
        }
    }
}
