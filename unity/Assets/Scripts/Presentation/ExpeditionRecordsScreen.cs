using System;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        int recordsPage;
        bool showArchivedRecords;
        ExpeditionRecord selectedRecord;
        static string RecordStatus(ExpeditionRecord record)
        {
            return (record.FinalRun.Version < 9 ? CatalogText.Get("text.0493") : record.BalanceVersion == ExpeditionHistory.BalanceVersion ? "" :
                record.BalanceVersion.Replace("normal-0", "v0.") + " · ") +
                (record.Abandoned ? CatalogText.Get("text.0494") : record.FinalRun.Victories == 30 ? CatalogText.Get("text.0495") : CatalogText.Get("text.0496"));
        }
        static string RecordDate(ExpeditionRecord record)
        { return DateTime.Parse(record.SavedUtc).ToLocalTime().ToString("MM/dd HH:mm"); }
        void DrawRecords()
        {
            Rect panel = new Rect(70, 35, 1140, 650), content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            LocalizedGui.Label(new Rect(content.x, 66, content.width, 48), CatalogText.Get("text.0497"), title);
            if (selectedRecord != null) { DrawRecordDetail(panel, content); return; }
            if (recordHistory.ArchivedBest != null && LocalizedGui.Button(new Rect(content.xMax - 290, 72, 290, 44),
                showArchivedRecords ? CatalogText.Get("text.0498") : CatalogText.Get("text.0499"), button))
            { showArchivedRecords = !showArchivedRecords; recordsPage = 0; }
            ExpeditionRecord best = recordHistory.Best;
            GUI.enabled = best != null;
            if (LocalizedGui.Button(new Rect(content.x, 130, content.width, 66), best == null ?
                CatalogText.Get("text.0500") :
                CatalogText.Get("text.0501") + best.Score.ToString("N0") + CatalogText.Get("text.0502") + best.FinalRun.Victories + CatalogText.Get("text.0503"), primary) && best != null)
                selectedRecord = best;
            GUI.enabled = true;
            LocalizedGui.Label(new Rect(content.x, 202, content.width, 36),
                showArchivedRecords ? CatalogText.Get("text.0504") :
                CatalogText.Get("text.0505"), small);
            var records = showArchivedRecords ? recordHistory.ArchivedBests : recordHistory.Recent;
            int pages = Math.Max(1, (records.Count + 4) / 5);
            recordsPage = Mathf.Clamp(recordsPage, 0, pages - 1);
            if (records.Count == 0) LocalizedGui.Label(new Rect(content.x, 270, content.width, 95),
                CatalogText.Get("text.0506"), body);
            for (int slot = 0; slot < 5; slot++)
            {
                int index = recordsPage * 5 + slot;
                if (index >= records.Count) break;
                var record = records[index];
                string text = RecordDate(record) + "   " + RecordStatus(record) + "   " + record.FinalRun.Victories +
                    CatalogText.Get("text.0507") + (record.Ranked ? record.Score.ToString("N0") + CatalogText.Get("text.0508") : CatalogText.Get("text.0509"));
                if (LocalizedGui.Button(new Rect(content.x, 240 + slot * 55, content.width, 49), text, button)) selectedRecord = record;
            }
            GUI.enabled = recordsPage > 0;
            if (LocalizedGui.Button(new Rect(content.x, 526, 160, 40), CatalogText.Get("text.0510"), button)) recordsPage--;
            GUI.enabled = true;
            LocalizedGui.Label(new Rect(content.x + 180, 526, content.width - 360, 40), (recordsPage + 1) + " / " + pages, centered);
            GUI.enabled = recordsPage + 1 < pages;
            if (LocalizedGui.Button(new Rect(content.xMax - 160, 526, 160, 40), CatalogText.Get("text.0330"), button)) recordsPage++;
            GUI.enabled = true;
            ExpeditionUiTheme.FooterRule(panel);
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterRect(panel), CatalogText.Get("text.0400"), button)) modal = Modal.None;
        }
        void DrawRecordDetail(Rect panel, Rect content)
        {
            var record = selectedRecord;
            var state = record.FinalRun;
            var recordedRun = ExpeditionRun.Restore(database, state);
            LocalizedGui.Label(new Rect(content.x, 124, content.width, 38), RecordDate(record) + "   " + RecordStatus(record) +
                "   ·   " + (record.Ranked ? record.Score.ToString("N0") + CatalogText.Get("text.0508") : CatalogText.Get("text.0509")), heading);
            LocalizedGui.Label(new Rect(content.x, 169, content.width, 57),
                CatalogText.Get("text.0511") + state.Victories + CatalogText.Get("text.0512") + state.Lantern + CatalogText.Get("text.0513") + state.Gold + "G\n" +
                (record.Ranked ? CatalogText.Get("text.0511") + (state.Victories * 10000).ToString("N0") + CatalogText.Get("text.0514") +
                    (state.Victories == 30 ? "30,000" : "0") + CatalogText.Get("text.0515") + (state.Lantern * 1000).ToString("N0") :
                    CatalogText.Get("text.0516")), body);
            LocalizedGui.Label(new Rect(content.x, 237, content.width, 34), CatalogText.Get("text.0517"), hint);
            var roster = recordedRun.Roster;
            for (int i = 0; i < state.DeployedIds.Length; i++)
            {
                var member = roster.Find(state.DeployedIds[i]);
                float w = content.width / 5;
                float x = content.x + i * w;
                if (art != null) art.Draw(member.FormId, new Rect(x + (w - 95) / 2, 277, 95, 112), 0);
                LocalizedGui.Label(new Rect(x + 4, 391, w - 8, 47), database.Get(member.FormId).Name, centered);
            }
            string research = "";
            foreach (string id in state.TacticIds) research += (research.Length == 0 ? "" : " · ") + CatalogResearch.Get(id).Title;
            LocalizedGui.Label(new Rect(content.x, 447, content.width, 38), CatalogText.Get("text.0518") + (research.Length == 0 ? CatalogText.Get("text.0519") : research), body);
            LocalizedGui.Label(new Rect(content.x, 491, content.width, 67),
                CatalogText.Get("text.0520") + FormationNames[Array.IndexOf(FormationIds, state.StartFormationId)] +
                CatalogText.Get("text.0521") + recordedRun.GoldSpent + "G" +
                (recordedRun.UsesLegacyRules ? "" : CatalogText.Get("text.0522") + recordedRun.CombatSeconds.ToString("0.0") + CatalogText.Get("text.0523")) + "\n" +
                CatalogText.Get("text.0524") + record.BalanceVersion + CatalogText.Get("text.0525") + record.ScoreVersion + CatalogText.Get("text.0526"), small);
            ExpeditionUiTheme.FooterRule(panel);
            var footer = ExpeditionUiTheme.FooterRect(panel);
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2), CatalogText.Get("text.0527"), button)) selectedRecord = null;
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), CatalogText.Get("text.0400"), button)) modal = Modal.None;
        }
    }
}
