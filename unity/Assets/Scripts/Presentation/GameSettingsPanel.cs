using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public static class GameSettingsPanel
    {
        public static readonly Rect Window = new Rect(260, 46, 760, 628);
        static GUIStyle title, body, small, button, primary, slider, thumb;
        static Font styledFont;
        static bool failed;
        static GUIStyle Label(Font font, int size, Color color)
        { return new GUIStyle { font = font, fontSize = size, alignment = TextAnchor.MiddleLeft, wordWrap = true, normal = { textColor = color } }; }
        public static bool Draw(Font font)
        {
            GamePreferences.Load();
            if (title == null || styledFont != font)
            {
                styledFont = font;
                title = Label(font, 28, ExpeditionUiTheme.Cream);
                body = Label(font, 21, ExpeditionUiTheme.Cream);
                small = Label(font, 16, ExpeditionUiTheme.Muted);
                button = ExpeditionUiTheme.CreateButtonStyle(font, 18);
                primary = ExpeditionUiTheme.CreateButtonStyle(font, 18, true);
                slider = new GUIStyle { fixedHeight = 44 };
                thumb = new GUIStyle { fixedWidth = 32, fixedHeight = 44 };
            }
            ExpeditionUiTheme.Fill(new Rect(0, 0, 1280, 720), new Color(.005f, .008f, .015f, .84f));
            ExpeditionUiTheme.Window(Window);
            Rect content = ExpeditionUiTheme.ContentRect(Window);
            LocalizedGui.Label(new Rect(content.x, content.y, content.width, 42), CatalogText.Get("text.0901"), title);
            ExpeditionUiTheme.Rule(new Rect(content.x, content.y + 54, content.width, 2));
            var options = GamePreferences.Current;
            float y = content.y + 70;
            // Both native language names stay visible, even when the player cannot read the current UI.
            LocalizedGui.Label(new Rect(content.x, y, 280, 32), "Language · 언어", body);
            if (LocalizedGui.Button(new Rect(content.x + 300, y - 4, 182, 48),
                "한국어", CatalogText.Language == "ko" ? primary : button))
            { options.Language = "ko"; GamePreferences.Apply(); }
            if (LocalizedGui.Button(new Rect(content.x + 498, y - 4, 198, 48),
                "English", CatalogText.Language == "en" ? primary : button))
            { options.Language = "en"; GamePreferences.Apply(); }
            y += 86;
            LocalizedGui.Label(new Rect(content.x, y, 430, 32), CatalogText.Get("text.0902"), body);
            LocalizedGui.Label(new Rect(content.xMax - 80, y, 80, 32), Mathf.RoundToInt(options.SoundVolume * 100) + "%", body);
            if (LocalizedGui.Button(new Rect(content.x, y + 38, 58, 48), "−", button)) options.SoundVolume = Mathf.Max(0, options.SoundVolume - .1f);
            Rect track = new Rect(content.x + 80, y + 42, content.width - 160, 44);
            options.SoundVolume = GUI.HorizontalSlider(track, options.SoundVolume, 0, 1, slider, thumb);
            float handleX = track.x + 16 + (track.width - 32) * options.SoundVolume;
            ExpeditionUiTheme.Fill(new Rect(track.x + 16, track.center.y - 5, track.width - 32, 10), ExpeditionUiTheme.PanelLight);
            ExpeditionUiTheme.Fill(new Rect(track.x + 16, track.center.y - 3, handleX - track.x - 16, 6), ExpeditionUiTheme.Gold);
            ExpeditionUiTheme.PanelBox(new Rect(handleX - 10, track.y + 7, 20, 30), true);
            ExpeditionUiTheme.Fill(new Rect(handleX - 3, track.y + 15, 6, 14), ExpeditionUiTheme.Gold);
            if (LocalizedGui.Button(new Rect(content.xMax - 58, y + 38, 58, 48), "+", button)) options.SoundVolume = Mathf.Min(1, options.SoundVolume + .1f);
            GamePreferences.Apply();
            y += 111;
            Toggle(content, y, CatalogText.Get("text.0903"), CatalogText.Get("text.0904"), ref options.ReduceMotion);
            y += 84;
            Toggle(content, y, CatalogText.Get("text.0905"), CatalogText.Get("text.0906"), ref options.ReduceFlashes);
            ExpeditionUiTheme.FooterRule(Window);
            Rect footer = ExpeditionUiTheme.FooterRect(Window);
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2), CatalogText.Get("text.0907"), button)) GamePreferences.Defaults();
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), CatalogText.Get("text.0908"), primary))
            { failed = !GamePreferences.TrySave(); if (!failed) return true; }
            if (failed) LocalizedGui.Label(new Rect(content.x, Window.yMax + 6, content.width, 32), CatalogText.Get("text.0909"), small);
            return false;
        }
        static void Toggle(Rect content, float y, string name, string description, ref bool value)
        {
            LocalizedGui.Label(new Rect(content.x, y, content.width - 156, 30), name, body);
            LocalizedGui.Label(new Rect(content.x, y + 30, content.width - 156, 48), description, small);
            if (LocalizedGui.Button(new Rect(content.xMax - 132, y + 6, 132, 52), value ? CatalogText.Get("text.0910") : CatalogText.Get("text.0911"), button)) value = !value;
        }
    }
}
