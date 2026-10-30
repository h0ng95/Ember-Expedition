using System;
using System.Collections.Generic;
using UnityEngine;

namespace Expedition.Presentation
{
    // Presentation only: no expedition/account writes or combat advancement.
    public sealed class SeasonOneEndingView : IDisposable
    {
        public const string TownPath = "Art/Story/v062/kingdom-return";
        public static readonly string[] Titles = { "돌아온 빛", "남겨진 왕국", "다시 시작하는 서약" };
        public static readonly string[] Captions = {
            "돌려놓은 빛 곁에서, 다시 살아가는 것들이 있었다.",
            "모든 불을 다시 켤 수는 없었다.\n사람들은 남은 온기와 식량을 나누었다.",
            "우리는 무엇을 지켜야 하는지부터 다시 배웠다.\n회복은, 함께 시작할 일이었다."
        };
        public bool Active { get; private set; }
        public int Page { get; private set; }
        public float Age { get; private set; }
        public readonly List<string> Actors = new List<string>();
        Texture2D forest, town, core;
        ForestEnemyArt animals;
        CatalogArtView art;
        GUIStyle text, title, control;
        Action finished;

        public static bool ShouldAutoPlay(bool victory, int wave, bool endingUnlocked)
        { return victory && wave == 30 && !endingUnlocked; }
        public SeasonOneEndingView(IEnumerable<string> actors, Action onFinished)
        {
            foreach (string actor in actors) if (!string.IsNullOrEmpty(actor) && Actors.Count < 5) Actors.Add(actor);
            finished = onFinished; Active = true;
            forest = Resources.Load<Texture2D>("Art/forest-battlefield");
            town = Resources.Load<Texture2D>(TownPath);
            core = IgnisCoreArt.Create(5);
            animals = new ForestEnemyArt(); art = new CatalogArtView();
            Font font = Resources.Load<Font>("Fonts/Galmuri11");
            text = new GUIStyle { font = font, fontSize = 23, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            text.normal.textColor = new Color32(255, 246, 221, 255);
            title = new GUIStyle(text) { fontSize = 28 };
            title.normal.textColor = new Color32(255, 211, 137, 255);
            control = new GUIStyle(text) { fontSize = 20 };
        }
        public void Tick(float dt) { if (Active) Age += Mathf.Max(0, dt); }
        public void Next()
        {
            if (!Active || Age < .2f) return;
            if (Page < 2) { Page++; Age = 0; } else Finish();
        }
        public void Finish()
        {
            if (!Active) return;
            Active = false; Action callback = finished; finished = null;
            if (callback != null) callback();
        }
        public void Draw()
        {
            if (!Active) return;
            bool quiet = GamePreferences.Current.ReduceMotion;
            Texture2D backdrop = Page == 0 ? forest : town;
            if (backdrop != null) GUI.DrawTexture(new Rect(0, 0, 1280, 720), backdrop, ScaleMode.ScaleAndCrop);
            if (Page == 0)
            {
                Rect bounds;
                animals.Draw("forest-cub", quiet ? 0 : (int)(Age * 5) % 4, new Vector2(720, 510), out bounds);
                if (core != null) GUI.DrawTexture(new Rect(792, 400 + (quiet ? 0 : Mathf.Round(Mathf.Sin(Age * 2) * 2)), 66, 90), core, ScaleMode.ScaleToFit);
                for (int i = 0; i < 10; i++)
                {
                    float grow = quiet ? 1 : Mathf.Clamp01((Age - i * .08f) / .8f);
                    float x = 680 + i * 22, y = 534 + i % 3 * 6;
                    Paint(new Rect(x, y - 12 * grow, 3, 12 * grow), new Color32(64, 112, 70, 255));
                    if (grow > .5f) { Paint(new Rect(x - 6, y - 10, 7, 4), new Color32(142, 187, 97, 255)); Paint(new Rect(x + 2, y - 14, 7, 4), new Color32(168, 201, 115, 255)); }
                }
            }
            if (Page == 2)
            {
                float approach = quiet ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(Age / 2.4f));
                for (int i = 0; i < Actors.Count; i++)
                {
                    float x = Mathf.Lerp(-90 - i * 50, 525 - i * 70, approach);
                    float y = 540 + i % 2 * 12;
                    Paint(new Rect(x - 16, y - 2, 32, 4), new Color(0, 0, 0, .24f));
                    art.DrawPeaceful(Actors[i], new Rect(x - 100, y - 200, 200, 200), quiet ? 0 : Age, approach < 1 && !quiet);
                }
            }
            if (Page > 0 && !quiet)
                for (int i = 0; i < 3; i++)
                {
                    float p = Mathf.Repeat(Age * .3f + i * .33f, 1);
                    Paint(new Rect(979 + i * 4, 480 - p * 16, 2, 2), new Color(1, .67f, .29f, (1 - p) * .5f));
                }
            Paint(new Rect(0, 0, 1280, 80), new Color(.035f, .045f, .065f, .8f));
            GUI.Label(new Rect(80, 16, 1120, 44), Titles[Page], title);
            Panel(new Rect(160, 562, 960, 76));
            GUI.Label(new Rect(180, 568, 920, 64), Captions[Page], text);
            if (Button(new Rect(24, 650, 210, 48), "건너뛰기")) { Finish(); return; }
            GUI.Label(new Rect(470, 650, 340, 48), Page == 2 ? "IGNIS: ASHEN OATH" : (Page + 1) + " / 3", control);
            if (Button(new Rect(1046, 650, 210, 48), Page == 2 ? "결과로 돌아가기" : "다음")) Next();
            if (!quiet && Age < .35f) Paint(new Rect(0, 80, 1280, 478), new Color(0, 0, 0, 1 - Age / .35f));
        }
        bool Button(Rect r, string label) { Panel(r); GUI.Label(r, label, control); return GUI.Button(r, GUIContent.none, GUIStyle.none); }
        static void Panel(Rect r)
        {
            Paint(new Rect(r.x + 4, r.y, r.width - 8, r.height), new Color32(111, 101, 83, 255));
            Paint(new Rect(r.x, r.y + 4, r.width, r.height - 8), new Color32(111, 101, 83, 255));
            Paint(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), new Color(.07f, .08f, .11f, .96f));
        }
        static void Paint(Rect r, Color c) { Color before = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = before; }
        public void Dispose()
        {
            Active = false; finished = null;
            if (animals != null) animals.Release(); animals = null;
            if (art != null) art.Dispose(); art = null;
            if (core != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(core); else UnityEngine.Object.DestroyImmediate(core); } core = null;
            if (town != null) Resources.UnloadAsset(town); town = null;
            if (forest != null) Resources.UnloadAsset(forest); forest = null;
        }
    }
}
