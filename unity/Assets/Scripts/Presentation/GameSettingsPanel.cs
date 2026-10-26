using UnityEngine;

namespace Expedition.Presentation
{
    public static class GameSettingsPanel
    {
        public static readonly Rect Window = new Rect(260, 112, 760, 496);
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
            GUI.Label(new Rect(content.x, content.y, content.width, 42), "설정  ·  SETTINGS", title);
            ExpeditionUiTheme.Rule(new Rect(content.x, content.y + 54, content.width, 2));
            var options = GamePreferences.Current;
            float y = content.y + 70;
            GUI.Label(new Rect(content.x, y, 430, 32), "효과음 음량", body);
            GUI.Label(new Rect(content.xMax - 80, y, 80, 32), Mathf.RoundToInt(options.SoundVolume * 100) + "%", body);
            if (GUI.Button(new Rect(content.x, y + 38, 58, 48), "−", button)) options.SoundVolume = Mathf.Max(0, options.SoundVolume - .1f);
            Rect track = new Rect(content.x + 80, y + 42, content.width - 160, 44);
            options.SoundVolume = GUI.HorizontalSlider(track, options.SoundVolume, 0, 1, slider, thumb);
            float handleX = track.x + 16 + (track.width - 32) * options.SoundVolume;
            ExpeditionUiTheme.Fill(new Rect(track.x + 16, track.center.y - 5, track.width - 32, 10), ExpeditionUiTheme.PanelLight);
            ExpeditionUiTheme.Fill(new Rect(track.x + 16, track.center.y - 3, handleX - track.x - 16, 6), ExpeditionUiTheme.Gold);
            ExpeditionUiTheme.PanelBox(new Rect(handleX - 10, track.y + 7, 20, 30), true);
            ExpeditionUiTheme.Fill(new Rect(handleX - 3, track.y + 15, 6, 14), ExpeditionUiTheme.Gold);
            if (GUI.Button(new Rect(content.xMax - 58, y + 38, 58, 48), "+", button)) options.SoundVolume = Mathf.Min(1, options.SoundVolume + .1f);
            GamePreferences.Apply();
            y += 111;
            Toggle(content, y, "피격 흔들림 줄이기", "공격에 맞았을 때 유닛이 흔들리는 움직임을 줄입니다.", ref options.ReduceMotion);
            y += 84;
            Toggle(content, y, "번쩍임 줄이기", "피격 번쩍임과 공격 효과 밝기를 낮춥니다.\n공격 범위 예고와 상태 표시는 유지합니다.", ref options.ReduceFlashes);
            ExpeditionUiTheme.FooterRule(Window);
            Rect footer = ExpeditionUiTheme.FooterRect(Window);
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2), "기본값", button)) GamePreferences.Defaults();
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), "적용하고 닫기", primary))
            { failed = !GamePreferences.TrySave(); if (!failed) return true; }
            if (failed) GUI.Label(new Rect(content.x, Window.yMax + 6, content.width, 32), "설정을 저장하지 못했습니다. 다시 시도해 주세요.", small);
            return false;
        }
        static void Toggle(Rect content, float y, string name, string description, ref bool value)
        {
            GUI.Label(new Rect(content.x, y, content.width - 156, 30), name, body);
            GUI.Label(new Rect(content.x, y + 30, content.width - 156, 48), description, small);
            if (GUI.Button(new Rect(content.xMax - 132, y + 6, 132, 52), value ? "켜짐" : "꺼짐", button)) value = !value;
        }
    }
}
