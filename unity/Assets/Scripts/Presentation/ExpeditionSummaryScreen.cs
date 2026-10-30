using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        void DrawExpeditionSummary()
        {
            bool cleared = run.LastVictory && run.Wave == ExpeditionRun.TotalWaves;
            DrawHeader("원정 결과");
            Rect panel = new Rect(150, 60, 980, 600), content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 92, content.width, 59), cleared ? "원정 완료" : "원정 종료", title);
            GUI.Label(new Rect(content.x, 157, content.width, 45), cleared ?
                ExpeditionStory.CompletionBrief :
                "남은 기회를 모두 소진했습니다. 거점에서 준비해 새 원정에 도전하세요.", body);
            string[] captions = { "도달 전투", "승리한 전투", "격파한 보스" };
            string[] values = { run.Wave + " / " + ExpeditionRun.TotalWaves, run.Victories + "승", run.Victories / 5 + " / 6" };
            Rect cards = new Rect(content.x, 217, content.width, 90);
            for (int i = 0; i < 3; i++)
            {
                Rect card = ExpeditionUiTheme.FooterButton(cards, i, 3);
                ExpeditionUiTheme.PanelBox(card);
                GUI.Label(new Rect(card.x + 18, card.y + 10, card.width - 36, 29), captions[i], small);
                GUI.Label(new Rect(card.x + 18, card.y + 40, card.width - 36, 42), values[i], heading);
            }
            GUI.Label(new Rect(content.x, 328, content.width, 38), "이번 원정에서 번 골드  " + run.VictoryGoldEarned + " G", heading);
            GUI.Label(new Rect(content.x, 370, content.width, 62),
                "사용 " + run.GoldSpent + " G (시작 자금 40 G 포함)  ·  남은 골드 " + run.Gold + " G\n남은 골드는 새 원정으로 이월되지 않습니다.", body);
            GUI.Label(new Rect(content.x, 446, content.width, 42), run.UsesLegacyRules ?
                "이전 규칙 원정 · 새 점수 순위 제외" : "원정 점수  " + run.Score.ToString("N0"), heading);
            GUI.Label(new Rect(content.x, 490, content.width, 64), run.UsesLegacyRules ?
                "다음 새 원정부터 영구 강화 없이 시작합니다.\n도감·이야기·성과 기록은 유지됩니다." :
                "격파 " + (run.Victories * 10000).ToString("N0") + " + 완주 " + (cleared ? "30,000" : "0") +
                " + 남은 기회 " + (run.Lantern * 1000).ToString("N0") + "\n새 원정에서는 동료·골드·연구가 초기화됩니다.", small);
            ExpeditionUiTheme.FooterRule(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            int footerButtons = cleared ? 4 : 2;
            GUI.enabled = HasBattleReport;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 0, footerButtons), "전투 기록", button)) modal = Modal.BattleReport;
            GUI.enabled = true;
            if (cleared && GUI.Button(ExpeditionUiTheme.FooterButton(footer, 1, 4), "결말 읽기", button))
                OpenJournal("palace-clear");
            if (cleared && GUI.Button(ExpeditionUiTheme.FooterButton(footer, 2, 4), "엔딩 다시 보기", button)) OpenEnding(true);
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, footerButtons - 1, footerButtons), "거점으로", primary))
            {
                if (run.Phase == ExpeditionPhase.Result && run.Continue()) Save("advance");
                home = true;
            }
        }
    }
}
