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
            GUI.Label(new Rect(76, 61, 600, 42), tactics ? "원정 연구" : "원정 연구", title);
            GUI.Label(new Rect(78, 111, 760, 30), tactics ?
                "숲 돌파 → 기초 · 성채 돌파 → 상위  /  새 원정에서 초기화" :
                "RESEARCH  /  첫 돌파의 기록으로 새로운 길을 여세요", small);
            ExpeditionUiTheme.PanelBox(new Rect(936, 65, 268, 70));
            GUI.Label(new Rect(948, 70, 244, 29), tactics ? "원정 골드" : "연구 포인트", runeCaption);
            GUI.Label(new Rect(948, 97, 244, 28), unavailable ? "기록 확인 필요" : tactics ?
                run.Gold + " G  ·  " + run.TacticIds.Count + "/3 선택" : progress.ResearchPoints +
                " PT  /  누적 " + progress.ResearchEarned, runeName);
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
                        !run.HasResearchRegion(node) ? node.RequiredRegion + " 돌파 필요" : "잠김"))
                        selectedTacticNodeId = node.Id;
                }
            CatalogResearchNode selected = CatalogResearch.Get(selectedTacticNodeId) ?? CatalogResearch.Nodes[0];
            bool owned = HasId(run.TacticIds, selected.Id), canBuy = run.CanPurchaseTactic(selected.Id);
            int branch = Array.IndexOf(TacticBranches, selected.BranchId);
            DrawRuneDetail(selected.Title, branch * 2 + selected.Tier - 1, branch,
                owned ? "선택한 연구" : canBuy ? "해금 가능" : "잠긴 연구");
            var affected = new List<CatalogMember>();
            foreach (string id in run.DeployedIds)
            {
                CatalogMember member = run.Roster.Find(id);
                if (member != null && EffectCanApply(selected.Id, member)) affected.Add(member);
            }
            string names = "";
            foreach (CatalogMember member in affected)
                names += (names.Length == 0 ? "" : " · ") + database.Get(member.FormId).Name;
            if (names.Length == 0) names = "적용할 동료가 없습니다.\n동료를 확보한 뒤 투자하세요.";
            if (researchDetailId != selected.Id) { researchDetailId = selected.Id; researchDetailScroll = Vector2.zero; }
            string requirement = selected.RequiredRegion + " " + selected.RequiredBossWave + "전투 돌파 " +
                (run.HasResearchRegion(selected) ? "완료" : "필요") + " · " + selected.Cost + "G";
            string[] paragraphs = { selected.DescriptionForRules(run.RulesVersion), EffectCondition(selected.Id),
                "적용 동료  " + affected.Count + " / " + run.DeployedIds.Count, names,
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
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2),
                owned ? "선택 완료" : "연구 선택  ·  " + selected.Cost + " G", primary))
            {
                if (run.PurchaseTactic(selected.Id))
                { Save("tactic_purchase", selected.Id); lastUnlockedRune = selected.Id; runePulseUntil = Time.unscaledTime + 1.2f; }
            }
            GUI.enabled = enabled;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), "닫기", button))
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
            GUI.Label(new Rect(940, 176, 239, 25), state, small);
            GUI.Label(new Rect(940, 205, 239, 40), name, heading);
            ExpeditionUiTheme.Rule(new Rect(888, 254, 292, 1));
        }

        static float ResearchParagraphs(string[] text, GUIStyle[] styles, float width, bool draw)
        {
            float y = 0;
            for (int i = 0; i < text.Length; i++)
            {
                // Measure with the actual Android font and padding instead of assuming a line count.
                float height = Mathf.Ceil(styles[i].CalcHeight(new GUIContent(text[i]), width)) + 5;
                if (draw) GUI.Label(new Rect(0, y, width, height), text[i], styles[i]);
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
            if (badProgress || progress == null) return "성과 기록을 확인하지 못했습니다. 기존 저장을 보관 중입니다.";
            if (owned) return "새 원정부터 적용됩니다.\n진행 중인 원정의 효과는 유지됩니다.";
            if (available) return "연구 " + node.Cost + " PT 사용 · 새 원정부터 적용\n첫 최고 격파 기록으로 포인트 획득";
            if (node.PrerequisiteId != null && !HasId(progress.ResearchIds, node.PrerequisiteId))
                return "먼저 연결된 연구를 여세요.\n" + CatalogLegacyResearch.Get(node.PrerequisiteId).Title;
            return "연구 포인트 " + (node.Cost - progress.ResearchPoints) + " PT가 더 필요합니다.";
        }

        string TacticLockReason(CatalogResearchNode node, bool owned, bool available)
        {
            if (owned) return "이 원정 동안 적용 · 새 원정에서 초기화\n되돌리기로 환불되지 않습니다.";
            if (available) return "소환 " + (node.Cost / (float)ExpeditionRun.SummonCost).ToString("0.#") +
                "회와 같은 비용 · 환불 불가\n동료 성장과 함께 비교하세요.";
            if (run.Phase != ExpeditionPhase.Preparation) return "전투 준비 중에만 선택할 수 있습니다.";
            if (!run.HasResearchRegion(node)) return "이번 원정에서 지역 보스를 격파하세요.\n골드만으로는 열 수 없습니다.";
            if (node.PrerequisiteId != null && !HasId(run.TacticIds, node.PrerequisiteId))
                return "먼저 연결된 연구를 여세요.\n" + CatalogResearch.Get(node.PrerequisiteId).Title;
            if (run.TacticIds.Count >= CatalogResearch.MaxNodes) return "이번 원정은 최대 3개까지 선택합니다.";
            return "골드 " + (node.Cost - run.Gold) + " G가 더 필요합니다.";
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
                case "shieldcraft": return "보호막이 남아 있는 동안 발동";
                case "exploitation": return "적에게 표식 또는 둔화가 있을 때 발동";
                case "spellcraft": case "ember-insight": return "직접 기술 피해만 적용 · 회복/보호막 제외";
                case "cinders": return "동료가 부여한 화상의 피해에 적용";
                case "marksmanship": return "원거리 기본 공격에 적용 · 기술 피해 제외";
                case "bulwark": return "근접 동료의 전투 시작 최대 체력에 적용";
                default: return "이 원정에 기록된 연구 효과";
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
            GUI.Label(new Rect(content.x, 77, 840, 43), form.Name + " · 적용 효과", heading);
            GUI.Label(new Rect(content.x, 124, 840, 55), "최대 체력  " + form.Hp.ToString("0") + " → " + memberEffectPreview.MaxHp.ToString("0") +
                "     기본 공격력  " + form.Power.ToString("0") + "\n피해 보정은 아래 조건에 따라 공격 시 적용됩니다.", body);
            float y = 193;
            int shown = 0;
            foreach (string id in run.LegacyResearchIds)
            {
                CatalogLegacyResearchNode node = CatalogLegacyResearch.Get(id);
                if (node.Tier != 2 || !EffectCanApply(id, member)) continue;
                EffectRow(content.x, y, content.width, "연구", node.Description, EffectCondition(id)); y += 57; shown++;
            }
            foreach (string id in run.TacticIds)
            {
                CatalogResearchNode node = CatalogResearch.Get(id);
                if (!EffectCanApply(id, member)) continue;
                EffectRow(content.x, y, content.width, "연구", node.DescriptionForRules(run.RulesVersion), EffectCondition(id)); y += 57; shown++;
            }
            if (shown == 0) GUI.Label(new Rect(content.x, 213, content.width, 80),
                "이 동료에게 적용되는 연구가 없습니다.\n고유·전승 특성은 동료 상세에서 확인할 수 있습니다.", body);
            GUI.Label(new Rect(content.x, 535, content.width, 23),
                "현재 원정의 효과 기준 · 전투 중 일시 강화와 적 방어력은 수치에 포함하지 않습니다.", small);
            ExpeditionUiTheme.FooterRule(panel);
            if (GUI.Button(ExpeditionUiTheme.FooterRect(panel), "동료 상세로", button)) modal = Modal.Detail;
        }

        void EffectRow(float x, float y, float width, string kind, string effect, string condition)
        {
            ExpeditionUiTheme.Fill(new Rect(x, y, width, 51), new Color(.07f, .08f, .12f, .7f));
            GUI.Label(new Rect(x + 12, y + 8, 59, 29), kind, hint);
            GUI.Label(new Rect(x + 80, y + 3, width - 90, 26), effect, body);
            GUI.Label(new Rect(x + 80, y + 28, width - 90, 21), condition, small);
        }
    }
}
