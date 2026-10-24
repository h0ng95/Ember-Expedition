using System;
using System.Collections.Generic;
using UnityEngine;
using Expedition.Core;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        GUIStyle runeName, runeCaption, runeBody, runeSmall;
        string lastUnlockedRune;
        float runePulseUntil;
        CatalogBattleUnit memberEffectPreview;
        static readonly Rect ResearchWindow = new Rect(44, 30, 1192, 660);
        static readonly Rect ResearchMap = new Rect(76, 159, 752, 407);

        void EnsureRuneStyles()
        {
            if (runeName != null) return;
            runeName = Style(18, Cream, true, TextAnchor.MiddleCenter);
            runeCaption = Style(14, Muted, false, TextAnchor.MiddleCenter);
            runeBody = Style(18, Cream, false, TextAnchor.UpperLeft);
            runeSmall = Style(15, Muted, false, TextAnchor.UpperLeft);
        }

        void ResearchHeader(bool tactics, bool unavailable)
        {
            EnsureRuneStyles();
            ExpeditionUiTheme.Window(ResearchWindow);
            GUI.Label(new Rect(76, 61, 600, 42), tactics ? "원정 전술" : "원정 연구", title);
            GUI.Label(new Rect(78, 111, 760, 30), tactics ?
                "TACTICS  /  이번 원정의 동료에게 맞는 길을 선택하세요" :
                "RESEARCH  /  첫 돌파의 기록으로 새로운 길을 여세요", small);
            ExpeditionUiTheme.PanelBox(new Rect(936, 65, 268, 70));
            GUI.Label(new Rect(948, 70, 244, 29), tactics ? "원정 골드" : "연구 포인트", runeCaption);
            GUI.Label(new Rect(948, 97, 244, 28), unavailable ? "기록 확인 필요" : tactics ?
                run.Gold + " G  ·  " + run.TacticIds.Count + "/3 선택" : progress.ResearchPoints +
                " PT  /  누적 " + progress.ResearchEarned, runeName);
            ExpeditionResearchBoard.Background(ResearchMap);
        }

        void DrawLegacyResearch()
        {
            bool unavailable = badProgress || progress == null;
            ResearchHeader(false, unavailable);
            int count = unavailable ? 0 : progress.ResearchIds.Count;
            DrawResearchRoutes(false, unavailable);
            ExpeditionResearchBoard.Center(count, 6, "IGNIS", runeName, runeCaption);
            for (int b = 0; b < 3; b++)
                for (int tier = 1; tier <= 2; tier++)
                {
                    CatalogLegacyResearchNode node = LegacyNode(LegacyBranches[b], tier);
                    if (node == null) continue;
                    bool owned = !unavailable && HasId(progress.ResearchIds, node.Id);
                    if (ExpeditionResearchBoard.Node(ExpeditionResearchBoard.Position(b, tier), b * 2 + tier - 1,
                        tier, ExpeditionResearchBoard.BranchColor(b), node.Title, node.Cost + " PT", owned,
                        !unavailable && progress.CanPurchaseResearch(node.Id), selectedLegacyNodeId == node.Id,
                        lastUnlockedRune == node.Id && Time.unscaledTime < runePulseUntil, runeName, runeCaption))
                        selectedLegacyNodeId = node.Id;
                }
            CatalogLegacyResearchNode selected = CatalogLegacyResearch.Get(selectedLegacyNodeId) ?? CatalogLegacyResearch.Nodes[0];
            bool learned = !unavailable && HasId(progress.ResearchIds, selected.Id);
            bool canBuy = !unavailable && progress.CanPurchaseResearch(selected.Id);
            int branch = Array.IndexOf(LegacyBranches, selected.BranchId);
            DrawRuneDetail(selected.Title, selected.Description, branch * 2 + selected.Tier - 1, branch,
                learned ? "활성화 완료" : canBuy ? "해금 가능" : "잠긴 연구");
            GUI.Label(new Rect(888, 332, 292, 24), selected.Tier == 1 ? "새로운 시작 편성" : "다음 원정의 효과", hint);
            if (selected.Tier == 1)
            {
                string[] members = CatalogLegacyResearch.GetFormationUnits(selected.BranchId);
                for (int i = 0; i < members.Length; i++)
                    if (art != null) art.Draw(members[i], new Rect(894 + i * 91, 362, 72, 88), (int)(Time.unscaledTime * 4f) % 4);
                GUI.Label(new Rect(888, 449, 292, 27), FormationUnits[branch + 1], runeSmall);
            }
            else
            {
                GUI.Label(new Rect(888, 371, 292, 85),
                    selected.Id == "ember-insight" ? "직접 피해를 주는 기술에 적용\n회복·보호막·화상은 제외" :
                    selected.Id == "frost-focus" ? "모든 동료의 기본 공격에 적용\n별도 공격 기술에는 미적용" :
                    "시작 편성과 관계없이\n모든 동료의 최대 체력에 적용", runeBody);
            }
            GUI.Label(new Rect(888, 486, 292, 61), LegacyLockReason(selected, learned, canBuy), runeSmall);
            ExpeditionUiTheme.FooterRule(ResearchWindow);
            Rect footer = ExpeditionUiTheme.FooterRect(ResearchWindow);
            bool enabled = GUI.enabled; GUI.enabled = canBuy;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2),
                learned ? "연구 완료" : "연구 해금  ·  " + selected.Cost + " PT", primary))
            {
                if (progress.PurchaseResearch(selected.Id))
                {
                    lastUnlockedRune = selected.Id; runePulseUntil = Time.unscaledTime + 1.2f;
                    progressDirty = true;
                    try { WriteProgress(); Notice("연구 완료 · 다음 새 원정부터 적용됩니다."); }
                    catch (Exception exception) { Notice("연구 저장에 실패했습니다: " + exception.Message); }
                }
            }
            GUI.enabled = enabled;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), "닫기", button)) modal = Modal.None;
        }

        void DrawTactics()
        {
            if (run == null) { modal = Modal.None; return; }
            ResearchHeader(true, false);
            DrawResearchRoutes(true, false);
            ExpeditionResearchBoard.Center(run.TacticIds.Count, 3, "TACTICS", runeName, runeCaption);
            for (int b = 0; b < 3; b++)
                for (int tier = 1; tier <= 2; tier++)
                {
                    CatalogResearchNode node = TacticNode(TacticBranches[b], tier);
                    if (node == null) continue;
                    if (ExpeditionResearchBoard.Node(ExpeditionResearchBoard.Position(b, tier), b * 2 + tier - 1,
                        tier, ExpeditionResearchBoard.BranchColor(b), node.Title, node.Cost + " G",
                        HasId(run.TacticIds, node.Id), run.CanPurchaseTactic(node.Id), selectedTacticNodeId == node.Id,
                        lastUnlockedRune == node.Id && Time.unscaledTime < runePulseUntil, runeName, runeCaption))
                        selectedTacticNodeId = node.Id;
                }
            CatalogResearchNode selected = CatalogResearch.Get(selectedTacticNodeId) ?? CatalogResearch.Nodes[0];
            bool owned = HasId(run.TacticIds, selected.Id), canBuy = run.CanPurchaseTactic(selected.Id);
            int branch = Array.IndexOf(TacticBranches, selected.BranchId);
            DrawRuneDetail(selected.Title, selected.Description, branch * 2 + selected.Tier - 1, branch,
                owned ? "선택한 전술" : canBuy ? "해금 가능" : "잠긴 전술");
            var affected = new List<CatalogMember>();
            foreach (string id in run.DeployedIds)
            {
                CatalogMember member = run.Roster.Find(id);
                if (member != null && EffectCanApply(selected.Id, member)) affected.Add(member);
            }
            GUI.Label(new Rect(888, 330, 292, 27), "현재 출전 동료  " + affected.Count + "/" + run.DeployedIds.Count, hint);
            if (affected.Count == 0)
                GUI.Label(new Rect(888, 367, 292, 60), "현재 이 효과를 받는 동료가 없습니다.\n동료를 확보한 뒤 투자하세요.", runeSmall);
            else
            {
                string names = "";
                foreach (CatalogMember member in affected)
                    names += (names.Length == 0 ? "" : " · ") + database.Get(member.FormId).Name;
                GUI.Label(new Rect(888, 366, 292, 66), names, runeSmall);
            }
            GUI.Label(new Rect(888, 433, 292, 39), EffectCondition(selected.Id), runeSmall);
            GUI.Label(new Rect(888, 480, 292, 64), TacticLockReason(selected, owned, canBuy), runeSmall);
            ExpeditionUiTheme.FooterRule(ResearchWindow);
            Rect footer = ExpeditionUiTheme.FooterRect(ResearchWindow);
            bool enabled = GUI.enabled; GUI.enabled = canBuy;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2),
                owned ? "선택 완료" : "전술 선택  ·  " + selected.Cost + " G", primary))
            {
                if (run.PurchaseTactic(selected.Id))
                { Save(); lastUnlockedRune = selected.Id; runePulseUntil = Time.unscaledTime + 1.2f; }
            }
            GUI.enabled = enabled;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), "닫기", button)) modal = Modal.None;
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

        void DrawRuneDetail(string name, string description, int glyph, int branch, string state)
        {
            ExpeditionUiTheme.PanelBox(new Rect(856, 159, 348, 407));
            Color color = ExpeditionResearchBoard.BranchColor(branch);
            ExpeditionUiTheme.Fill(new Rect(858, 160, 344, 3), color);
            ExpeditionResearchBoard.Glyph(new Rect(887, 183, 35, 35), glyph, color);
            GUI.Label(new Rect(940, 176, 239, 25), state, small);
            GUI.Label(new Rect(940, 205, 239, 40), name, heading);
            ExpeditionUiTheme.Rule(new Rect(888, 254, 292, 1));
            GUI.Label(new Rect(888, 273, 292, 54), description, runeBody);
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
            if (run.Wave < node.UnlockWave) return "전투 " + node.UnlockWave + " 준비부터 열립니다.";
            if (node.PrerequisiteId != null && !HasId(run.TacticIds, node.PrerequisiteId))
                return "먼저 연결된 전술을 여세요.\n" + CatalogResearch.Get(node.PrerequisiteId).Title;
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
                    skill != "E-PR-H" && skill != "E-BA-G" && skill != "E-BA-A" && skill != "H-05" && skill != "H-06";
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
            CatalogBattle preview = new CatalogBattle(database, run.TacticIds, run.LegacyResearchIds);
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
                EffectRow(content.x, y, content.width, "전술", node.Description, EffectCondition(id)); y += 57; shown++;
            }
            if (shown == 0) GUI.Label(new Rect(content.x, 213, content.width, 80),
                "이 동료에게 적용되는 연구·전술이 없습니다.\n고유·전승 특성은 동료 상세에서 확인할 수 있습니다.", body);
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
