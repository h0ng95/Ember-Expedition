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
            return (record.FinalRun.Version < 9 ? "이전 규칙 · " : record.BalanceVersion == ExpeditionHistory.BalanceVersion ? "" :
                record.BalanceVersion.Replace("normal-0", "v0.") + " · ") +
                (record.Abandoned ? "포기" : record.FinalRun.Victories == 30 ? "완주" : "패배");
        }
        static string RecordDate(ExpeditionRecord record)
        { return DateTime.Parse(record.SavedUtc).ToLocalTime().ToString("MM/dd HH:mm"); }
        void DrawRecords()
        {
            Rect panel = new Rect(70, 35, 1140, 650), content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 66, content.width, 48), "개인 기록", title);
            if (selectedRecord != null) { DrawRecordDetail(panel, content); return; }
            if (recordHistory.ArchivedBest != null && GUI.Button(new Rect(content.xMax - 290, 72, 290, 44),
                showArchivedRecords ? "최근 원정 보기" : "이전 버전 최고 기록", button))
            { showArchivedRecords = !showArchivedRecords; recordsPage = 0; }
            ExpeditionRecord best = recordHistory.Best;
            GUI.enabled = best != null;
            if (GUI.Button(new Rect(content.x, 130, content.width, 66), best == null ?
                "첫 기록에 도전하세요 · Normal v0.48" :
                "개인 최고  " + best.Score.ToString("N0") + "점  ·  " + best.FinalRun.Victories + "/30 격파  ·  자세히", primary) && best != null)
                selectedRecord = best;
            GUI.enabled = true;
            GUI.Label(new Rect(content.x, 202, content.width, 36),
                showArchivedRecords ? "이전 밸런스별 최고 기록 · 현재 점수와 분리해서 보관" :
                "현재 밸런스의 최고 기록 · 최근 30개 보관 · 이전 버전은 별도 비교", small);
            var records = showArchivedRecords ? recordHistory.ArchivedBests : recordHistory.Recent;
            int pages = Math.Max(1, (records.Count + 4) / 5);
            recordsPage = Mathf.Clamp(recordsPage, 0, pages - 1);
            if (records.Count == 0) GUI.Label(new Rect(content.x, 270, content.width, 95),
                "아직 저장된 원정 결과가 없습니다.\n완주하거나 등불을 모두 소진하면 점수와 최종 편성을 남깁니다.", body);
            for (int slot = 0; slot < 5; slot++)
            {
                int index = recordsPage * 5 + slot;
                if (index >= records.Count) break;
                var record = records[index];
                string text = RecordDate(record) + "   " + RecordStatus(record) + "   " + record.FinalRun.Victories +
                    "/30 격파   ·   " + (record.Ranked ? record.Score.ToString("N0") + "점" : "순위 제외");
                if (GUI.Button(new Rect(content.x, 240 + slot * 55, content.width, 49), text, button)) selectedRecord = record;
            }
            GUI.enabled = recordsPage > 0;
            if (GUI.Button(new Rect(content.x, 526, 160, 40), "이전", button)) recordsPage--;
            GUI.enabled = true;
            GUI.Label(new Rect(content.x + 180, 526, content.width - 360, 40), (recordsPage + 1) + " / " + pages, centered);
            GUI.enabled = recordsPage + 1 < pages;
            if (GUI.Button(new Rect(content.xMax - 160, 526, 160, 40), "다음", button)) recordsPage++;
            GUI.enabled = true;
            ExpeditionUiTheme.FooterRule(panel);
            if (GUI.Button(ExpeditionUiTheme.FooterRect(panel), "닫기", button)) modal = Modal.None;
        }
        void DrawRecordDetail(Rect panel, Rect content)
        {
            var record = selectedRecord;
            var state = record.FinalRun;
            var recordedRun = ExpeditionRun.Restore(database, state);
            GUI.Label(new Rect(content.x, 124, content.width, 38), RecordDate(record) + "   " + RecordStatus(record) +
                "   ·   " + (record.Ranked ? record.Score.ToString("N0") + "점" : "순위 제외"), heading);
            GUI.Label(new Rect(content.x, 169, content.width, 57),
                "격파 " + state.Victories + "/30 · 남은 등불 " + state.Lantern + " · 남은 골드 " + state.Gold + "G\n" +
                (record.Ranked ? "격파 " + (state.Victories * 10000).ToString("N0") + " + 완주 " +
                    (state.Victories == 30 ? "30,000" : "0") + " + 등불 " + (state.Lantern * 1000).ToString("N0") :
                    "포기 및 이전 규칙 기록에는 점수를 부여하지 않습니다."), body);
            GUI.Label(new Rect(content.x, 237, content.width, 34), "최종 출전 편성", hint);
            var roster = recordedRun.Roster;
            for (int i = 0; i < state.DeployedIds.Length; i++)
            {
                var member = roster.Find(state.DeployedIds[i]);
                float w = content.width / 5;
                float x = content.x + i * w;
                if (art != null) art.Draw(member.FormId, new Rect(x + (w - 95) / 2, 277, 95, 112), 0);
                GUI.Label(new Rect(x + 4, 391, w - 8, 47), database.Get(member.FormId).Name, centered);
            }
            string research = "";
            foreach (string id in state.TacticIds) research += (research.Length == 0 ? "" : " · ") + CatalogResearch.Get(id).Title;
            GUI.Label(new Rect(content.x, 447, content.width, 38), "원정 연구  " + (research.Length == 0 ? "미습득" : research), body);
            GUI.Label(new Rect(content.x, 491, content.width, 67),
                "시작 편성: " + FormationNames[Array.IndexOf(FormationIds, state.StartFormationId)] +
                "  ·  원정 중 사용 " + recordedRun.GoldSpent + "G" +
                (recordedRun.UsesLegacyRules ? "" : " · 전투 " + recordedRun.CombatSeconds.ToString("0.0") + "초 (점수 제외)") + "\n" +
                "규칙 " + record.BalanceVersion + " · 점수 v" + record.ScoreVersion + " · 이 기기에 저장된 개인 기록", small);
            ExpeditionUiTheme.FooterRule(panel);
            var footer = ExpeditionUiTheme.FooterRect(panel);
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2), "기록 목록", button)) selectedRecord = null;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), "닫기", button)) modal = Modal.None;
        }
    }
}
