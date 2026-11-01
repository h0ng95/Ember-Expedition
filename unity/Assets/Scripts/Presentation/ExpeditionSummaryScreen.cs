using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        void DrawExpeditionSummary()
        {
            bool cleared = run.LastVictory && run.Wave == ExpeditionRun.TotalWaves;
            DrawHeader(CatalogText.Get("text.0671"));
            Rect panel = new Rect(150, 60, 980, 600), content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            LocalizedGui.Label(new Rect(content.x, 92, content.width, 59), cleared ? CatalogText.Get("text.0672") : CatalogText.Get("text.0673"), title);
            LocalizedGui.Label(new Rect(content.x, 157, content.width, 45), cleared ?
                ExpeditionStory.CompletionBrief :
                CatalogText.Get("text.0815"), body);
            string[] captions = { CatalogText.Get("text.0816"), CatalogText.Get("text.0817"), CatalogText.Get("text.0818") };
            string[] values = { run.Wave + " / " + ExpeditionRun.TotalWaves, run.Victories + CatalogText.Get("text.0819"), run.Victories / 5 + " / 6" };
            Rect cards = new Rect(content.x, 217, content.width, 90);
            for (int i = 0; i < 3; i++)
            {
                Rect card = ExpeditionUiTheme.FooterButton(cards, i, 3);
                ExpeditionUiTheme.PanelBox(card);
                LocalizedGui.Label(new Rect(card.x + 18, card.y + 10, card.width - 36, 29), captions[i], small);
                LocalizedGui.Label(new Rect(card.x + 18, card.y + 40, card.width - 36, 42), values[i], heading);
            }
            LocalizedGui.Label(new Rect(content.x, 328, content.width, 38), CatalogText.Get("text.0820") + run.VictoryGoldEarned + " G", heading);
            LocalizedGui.Label(new Rect(content.x, 370, content.width, 62),
                CatalogText.Get("text.0821") + run.GoldSpent + CatalogText.Get("text.0822") + run.Gold + CatalogText.Get("text.0823"), body);
            LocalizedGui.Label(new Rect(content.x, 446, content.width, 42), run.UsesLegacyRules ?
                CatalogText.Get("text.0824") : CatalogText.Get("text.0825") + run.Score.ToString("N0"), heading);
            LocalizedGui.Label(new Rect(content.x, 490, content.width, 64), run.UsesLegacyRules ?
                CatalogText.Get("text.0826") :
                CatalogText.Get("text.0511") + (run.Victories * 10000).ToString("N0") + CatalogText.Get("text.0514") + (cleared ? "30,000" : "0") +
                CatalogText.Get("text.0515") + (run.Lantern * 1000).ToString("N0") + CatalogText.Get("text.0827"), small);
            ExpeditionUiTheme.FooterRule(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            int footerButtons = cleared ? 4 : 2;
            GUI.enabled = HasBattleReport;
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 0, footerButtons), CatalogText.Get("text.0683"), button)) modal = Modal.BattleReport;
            GUI.enabled = true;
            if (cleared && LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 1, 4), CatalogText.Get("text.0828"), button))
                OpenJournal("palace-clear");
            if (cleared && LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 2, 4), CatalogText.Get("text.0692"), button)) OpenEnding(true);
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, footerButtons - 1, footerButtons), CatalogText.Get("text.0365"), primary))
            {
                if (run.Phase == ExpeditionPhase.Result && run.Continue()) Save("advance");
                home = true;
            }
        }
    }
}
