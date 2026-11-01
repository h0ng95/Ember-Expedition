using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        PlaytestRecorder playtest;

        void DrawPlaytestButton()
        {
            if (playtest != null && playtest.Log != null &&
                LocalizedGui.Button(new Rect(785, 650, 220, 48), CatalogText.Get("text.0443"), button)) modal = Modal.Playtest;
        }

        void DrawPlaytest()
        {
            Rect panel = new Rect(180, 130, 920, 460);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            LocalizedGui.Label(new Rect(content.x, content.y, content.width, 45), CatalogText.Get("text.0444"), heading);
            LocalizedGui.Label(new Rect(content.x, content.y + 64, content.width, 110),
                CatalogText.Get("text.0445") +
                CatalogText.Get("text.0446") +
                CatalogText.Get("text.0447"), dialogBody);
            PlaytestLog log = playtest == null ? null : playtest.Log;
            LocalizedGui.Label(new Rect(content.x, content.y + 190, content.width, 70),
                log == null ? CatalogText.Get("text.0448") : log.Error ??
                CatalogText.Get("text.0449") + log.Count + CatalogText.Get("text.0450"), body);
            LocalizedGui.Label(new Rect(content.x, content.y + 270, content.width, 44),
                CatalogText.Get("text.0451"), small);
            ExpeditionUiTheme.FooterRule(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2), CatalogText.Get("text.0400"), button)) modal = Modal.None;
            GUI.enabled = log != null && log.Count > 0;
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), CatalogText.Get("text.0452"), primary))
            {
                GUIUtility.systemCopyBuffer = log.ExportRecent();
                Notice(CatalogText.Get("text.0453"));
            }
            GUI.enabled = true;
        }
    }
}
