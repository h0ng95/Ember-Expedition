using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        CatalogBattleReport battleReport;
        ExpeditionRun battleReportRun;
        bool battleReportComplete;

        bool HasBattleReport
        {
            get
            {
                return battleReportComplete && battleReport != null &&
                    ReferenceEquals(battleReportRun, run) && run != null &&
                    (run.Phase == ExpeditionPhase.Result || run.Phase == ExpeditionPhase.Completed ||
                     run.Phase == ExpeditionPhase.Defeated);
            }
        }

        void BeginReport(CatalogBattle battle)
        {
            if (battleReport != null) battleReport.Dispose();
            battleReport = battle == null ? null : new CatalogBattleReport(battle);
            battleReportRun = run;
            battleReportComplete = false;
        }

        void CompleteReport()
        {
            if (battleReport == null) return;
            battleReport.Complete(run.LastVictory);
            battleReportComplete = true;
        }

        void DrawBattleReport()
        {
            Rect panel = new Rect(96, 36, 1088, 648);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 65, content.width, 43), "전투 기록 · 기여도와 다음 준비", heading);
            GUI.Label(new Rect(content.x, 113, content.width, 36),
                CatalogBattleFeedback.Summary(HasBattleReport ? battleReport : null), body);
            GUI.Label(new Rect(content.x, 151, content.width, 62),
                HasBattleReport ? CatalogBattleFeedback.Evidence(battleReport, database) : "", small);
            GUI.Label(new Rect(content.x, 218, content.width, 58),
                HasBattleReport ? CatalogBattleFeedback.Suggestion(battleReport) : "", hint);
            ExpeditionUiTheme.Rule(new Rect(content.x, 282, content.width, 2));
            GUI.Label(new Rect(content.x + 12, 286, 330, 30), "동료", body);
            GUI.Label(new Rect(content.x + 364, 286, 182, 30), "피해", body);
            GUI.Label(new Rect(content.x + 584, 286, 182, 30), "회복", body);
            GUI.Label(new Rect(content.x + 804, 286, 182, 30), "보호", body);

            if (HasBattleReport)
            {
                int count = Mathf.Min(5, battleReport.Allies.Count);
                for (int i = 0; i < count; i++)
                {
                    CatalogBattleReport.AllyContribution row = battleReport.Allies[i];
                    float y = 320 + i * 42;
                    if (i % 2 == 0)
                        Fill(new Rect(content.x, y, content.width, 41), new Color(1f, 1f, 1f, .055f));
                    string name = database == null ? row.FormId : database.Get(row.FormId).Name;
                    GUI.Label(new Rect(content.x + 12, y + 2, 330, 39),
                        "출전 " + (i + 1) + " · " + name, body);
                    GUI.Label(new Rect(content.x + 364, y + 2, 182, 39), row.Damage.ToString("0"), body);
                    GUI.Label(new Rect(content.x + 584, y + 2, 182, 39), row.Healing.ToString("0"), body);
                    GUI.Label(new Rect(content.x + 804, y + 2, 182, 39), row.Protected.ToString("0"), body);
                }
            }
            else GUI.Label(new Rect(content.x + 12, 335, content.width - 24, 42),
                "이번 앱 실행에서 확인할 전투 기록이 없습니다.", body);

            GUI.Label(new Rect(content.x + 12, 536, content.width - 24, 30),
                "피해: 적 체력·보호막 피해 / 회복·보호: 실제 적용량 / 공격 강화의 추가 기여는 별도 집계하지 않음", small);

            ExpeditionUiTheme.FooterRule(panel);
            if (GUI.Button(footer, "닫기", button)) modal = Modal.None;
        }

        bool HasFailureEvidence
        {
            get { return HasBattleReport && (battleReport.Outcome == CatalogBattleOutcome.Timeout ||
                battleReport.Outcome == CatalogBattleOutcome.Defeat); }
        }
        void DrawFailureEvidence(Rect rect)
        {
            ExpeditionUiTheme.PanelBox(rect);
            GUI.Label(new Rect(rect.x + 14, rect.y + 6, rect.width - 28, 36), CatalogBattleFeedback.Summary(battleReport), body);
            GUI.Label(new Rect(rect.x + 14, rect.y + 46, rect.width - 28, 66), CatalogBattleFeedback.Suggestion(battleReport), hint);
        }
    }
}
