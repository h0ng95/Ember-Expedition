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
            battleReport.Dispose();
            battleReportComplete = true;
        }

        void DrawBattleReport()
        {
            Rect panel = new Rect(112, 62, 1056, 596);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 91, content.width, 43), "전투 기여도", heading);
            GUI.Label(new Rect(content.x, 143, content.width, 53),
                "피해: 적 체력·보호막에 준 실제 피해 · 회복/보호: 실제 적용량\n공격력·공격 속도 강화의 추가 기여는 별도로 집계하지 않습니다.", body);
            ExpeditionUiTheme.Rule(new Rect(content.x, 202, content.width, 2));
            GUI.Label(new Rect(content.x + 12, 210, 330, 32), "동료", body);
            GUI.Label(new Rect(content.x + 364, 210, 182, 32), "피해", body);
            GUI.Label(new Rect(content.x + 584, 210, 182, 32), "회복", body);
            GUI.Label(new Rect(content.x + 804, 210, 182, 32), "보호", body);

            if (HasBattleReport)
            {
                int count = Mathf.Min(5, battleReport.Allies.Count);
                for (int i = 0; i < count; i++)
                {
                    CatalogBattleReport.AllyContribution row = battleReport.Allies[i];
                    float y = 248 + i * 57;
                    if (i % 2 == 0)
                        Fill(new Rect(content.x, y, content.width, 53), new Color(1f, 1f, 1f, .055f));
                    string name = database == null ? row.FormId : database.Get(row.FormId).Name;
                    GUI.Label(new Rect(content.x + 12, y + 7, 330, 39),
                        "출전 " + (i + 1) + " · " + name, body);
                    GUI.Label(new Rect(content.x + 364, y + 7, 182, 39), row.Damage.ToString("0"), body);
                    GUI.Label(new Rect(content.x + 584, y + 7, 182, 39), row.Healing.ToString("0"), body);
                    GUI.Label(new Rect(content.x + 804, y + 7, 182, 39), row.Protected.ToString("0"), body);
                }
            }
            else GUI.Label(new Rect(content.x + 12, 280, content.width - 24, 42),
                "이번 앱 실행에서 확인할 전투 기록이 없습니다.", body);

            ExpeditionUiTheme.FooterRule(panel);
            if (GUI.Button(footer, "닫기", button)) modal = Modal.None;
        }
    }
}
