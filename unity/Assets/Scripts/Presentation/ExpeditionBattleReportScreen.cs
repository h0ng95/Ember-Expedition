using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        CatalogBattleReport battleReport;
        ExpeditionRun battleReportRun;
        bool battleReportComplete;
        ExpeditionRun retryReportRun;
        int retryReportWave;
        GUIStyle retryRowButton, retryRowPrimary, retryLinkButton;

        void EnsureRetryStyles()
        {
            if (retryRowButton != null) return;
            retryRowButton = new GUIStyle(button) { fontSize = 17, wordWrap = false, padding = new RectOffset(8, 8, 4, 4) };
            retryRowPrimary = new GUIStyle(primary) { fontSize = 17, wordWrap = false, padding = new RectOffset(8, 8, 4, 4) };
            retryLinkButton = new GUIStyle(button) { fontSize = 16, wordWrap = false, padding = new RectOffset(8, 8, 3, 3) };
        }

        bool RetryReportActive
        {
            get { return run != null && ReferenceEquals(retryReportRun, run) && run.Wave == retryReportWave &&
                run.Phase == ExpeditionPhase.Preparation && !run.LastVictory && !run.HasEnded; }
        }

        bool HasBattleReport
        {
            get
            {
                return battleReportComplete && battleReport != null &&
                    ReferenceEquals(battleReportRun, run) && run != null &&
                    (run.Phase == ExpeditionPhase.Result || run.Phase == ExpeditionPhase.Completed ||
                     run.Phase == ExpeditionPhase.Defeated || RetryReportActive);
            }
        }

        void BeginReport(CatalogBattle battle)
        {
            retryReportRun = null;
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
            EnsureRetryStyles();
            bool canRetry = CanReviewRetry;
            Rect panel = new Rect(96, 36, 1088, 648);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            ExpeditionUiTheme.Window(panel);
            LocalizedGui.Label(new Rect(content.x, 65, content.width, 43), CatalogText.Get("text.0392"), heading);
            LocalizedGui.Label(new Rect(content.x, 113, content.width, 36),
                CatalogBattleFeedback.Summary(HasBattleReport ? battleReport : null), body);
            LocalizedGui.Label(new Rect(content.x, 151, content.width, 62),
                HasBattleReport ? CatalogBattleFeedback.Evidence(battleReport, database) : "", small);
            LocalizedGui.Label(new Rect(content.x, 218, content.width, 58),
                HasBattleReport ? CatalogBattleFeedback.Suggestion(battleReport) : "", hint);
            ExpeditionUiTheme.Rule(new Rect(content.x, 282, content.width, 2));
            LocalizedGui.Label(new Rect(content.x + 12, 286, 330, 30), canRetry ? CatalogText.Get("text.0393") : CatalogText.Get("text.0394"), body);
            LocalizedGui.Label(new Rect(content.x + 364, 286, 182, 30), CatalogText.Get("text.0395"), body);
            LocalizedGui.Label(new Rect(content.x + 584, 286, 182, 30), CatalogText.Get("text.0396"), body);
            LocalizedGui.Label(new Rect(content.x + 804, 286, 182, 30), CatalogText.Get("text.0397"), body);

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
                    Rect nameRect = new Rect(content.x + 8, y, 334, 41);
                    string label = CatalogText.Format("report.ally", i + 1, name);
                    if (canRetry && run.Roster.Find(row.Id) != null && IsDeployed(row.Id))
                    {
                        if (LocalizedGui.Button(nameRect, label + "  ›", row.Id == battleReport.FirstLostUnitId ? retryRowPrimary : retryRowButton))
                        { PrepareRetryFor(row.Id); return; }
                    }
                    else LocalizedGui.Label(new Rect(content.x + 12, y + 2, 330, 39), label, body);
                    LocalizedGui.Label(new Rect(content.x + 364, y + 2, 182, 39), row.Damage.ToString("0"), body);
                    LocalizedGui.Label(new Rect(content.x + 584, y + 2, 182, 39), row.Healing.ToString("0"), body);
                    LocalizedGui.Label(new Rect(content.x + 804, y + 2, 182, 39), row.Protected.ToString("0"), body);
                }
            }
            else LocalizedGui.Label(new Rect(content.x + 12, 335, content.width - 24, 42),
                CatalogText.Get("text.0398"), body);

            LocalizedGui.Label(new Rect(content.x + 12, 536, content.width - 24, 30),
                CatalogText.Get("text.0399"), small);

            ExpeditionUiTheme.FooterRule(panel);
            string firstLost = HasBattleReport ? battleReport.FirstLostUnitId : null;
            bool quick = canRetry && firstLost != null && run.Roster.Find(firstLost) != null && IsDeployed(firstLost);
            if (LocalizedGui.Button(quick ? ExpeditionUiTheme.FooterButton(footer, 0, 2) : footer, CatalogText.Get("text.0400"), button)) modal = Modal.None;
            if (quick && LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), CatalogText.Get("text.0401"), primary)) PrepareRetryFor(firstLost);
        }

        bool CanReviewRetry
        {
            get { return HasFailureEvidence && !run.LastVictory && !run.HasEnded &&
                (run.Phase == ExpeditionPhase.Result || RetryReportActive); }
        }

        bool PrepareRetryFor(string memberId)
        {
            if (!CanReviewRetry || string.IsNullOrEmpty(memberId) || run.Roster.Find(memberId) == null || !IsDeployed(memberId)) return false;
            bool recorded = false;
            foreach (var row in battleReport.Allies) if (row.Id == memberId) { recorded = true; break; }
            if (!recorded) return false;
            if (run.Phase == ExpeditionPhase.Result && !run.Continue()) return false;
            CancelPointer(); retryReportRun = run; retryReportWave = run.Wave;
            selectedId = memberId; SelectPreparation(memberId); home = false; modal = Modal.None;
            Save("retry_focus", memberId);
            return true;
        }

        void DrawRetryReviewLink()
        {
            if (!RetryReportActive || !HasBattleReport) return;
            EnsureRetryStyles();
            if (LocalizedGui.Button(new Rect(706, 158, 166, 34), CatalogText.Get("text.0402"), retryLinkButton)) modal = Modal.BattleReport;
        }

        bool ContinueResultPreparation()
        {
            bool retainReport = CanReviewRetry;
            if (run == null || run.Phase != ExpeditionPhase.Result || !run.Continue()) return false;
            retryReportRun = retainReport && run.Phase == ExpeditionPhase.Preparation ? run : null;
            retryReportWave = run.Wave;
            selectedId = null; SelectPreparation(null);
            Save("advance");
            return true;
        }

        bool HasFailureEvidence
        {
            get { return HasBattleReport && (battleReport.Outcome == CatalogBattleOutcome.Timeout ||
                battleReport.Outcome == CatalogBattleOutcome.Defeat); }
        }
        void DrawFailureEvidence(Rect rect)
        {
            ExpeditionUiTheme.PanelBox(rect);
            LocalizedGui.Label(new Rect(rect.x + 14, rect.y + 6, rect.width - 28, 36), CatalogBattleFeedback.Summary(battleReport), body);
            LocalizedGui.Label(new Rect(rect.x + 14, rect.y + 46, rect.width - 28, 66), CatalogBattleFeedback.Suggestion(battleReport), hint);
        }
    }
}
