using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        PlaytestRecorder playtest;

        void DrawPlaytestButton()
        {
            if (playtest != null && playtest.Log != null &&
                GUI.Button(new Rect(785, 650, 220, 48), "테스트 기록", button)) modal = Modal.Playtest;
        }

        void DrawPlaytest()
        {
            Rect panel = new Rect(180, 130, 920, 460);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, content.y, content.width, 45), "플레이테스트 기록", heading);
            GUI.Label(new Rect(content.x, content.y + 64, content.width, 110),
                "편성 · 성장 · 골드 사용 · 연구 · 전투 결과를 이 기기에 기록합니다.\n" +
                "이름이나 계정 정보는 수집하지 않으며 자동으로 전송하지 않습니다.\n" +
                "앱 실행별 최대 2 MB, 최근 8개 기록을 보관합니다.", dialogBody);
            PlaytestLog log = playtest == null ? null : playtest.Log;
            GUI.Label(new Rect(content.x, content.y + 190, content.width, 70),
                log == null ? "기록을 사용할 수 없습니다." : log.Error ??
                "이번 실행에서 " + log.Count + "개 행동 기록\n복사: 최근 최대 60개 행동 · 전체 파일: 앱 저장소의 PlaytestLogs", body);
            GUI.Label(new Rect(content.x, content.y + 270, content.width, 44),
                "복사한 JSON을 테스트 메모와 함께 전달하면 됩니다. 이전 실행의 기록은 파일에 남습니다.", small);
            ExpeditionUiTheme.FooterRule(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2), "닫기", button)) modal = Modal.None;
            GUI.enabled = log != null && log.Count > 0;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), "최근 기록 복사", primary))
            {
                GUIUtility.systemCopyBuffer = log.ExportRecent();
                Notice("최근 테스트 기록을 복사했습니다.");
            }
            GUI.enabled = true;
        }
    }
}
