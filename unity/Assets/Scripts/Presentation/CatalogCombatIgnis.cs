using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class CatalogCombatLab
    {
        const float IgnisRevealDuration = .9f;
        float ignisAge;
        Vector2 ignisGround;
        Color ignisColor;
        Texture2D ignisArtwork;
        bool IgnisReady { get { return GamePreferences.Current.ReduceMotion || ignisAge >= IgnisRevealDuration; } }

        void BeginIgnisReveal()
        {
            ignisAge = 0;
            Vector2 ground = ExpeditionPoint(new Vector2(2.5f, 0), HudRules.BattleArena);
            foreach (var unit in battle.Units)
                if (unit.Team == CatalogTeam.Enemy && unit.IsBoss)
                { ground = ExpeditionPoint(new Vector2(unit.X, unit.Y), HudRules.BattleArena); break; }
            // Keep the core beside the fallen guardian, away from the overlapping survivors.
            ignisGround = new Vector2(Mathf.Clamp(ground.x + 88, 660, 1140), Mathf.Clamp(ground.y, 360, 590));
            ignisColor = IgnisCoreArt.RegionColor(expeditionWave);
            LoadIgnisArtwork();
        }

        void LoadIgnisArtwork()
        {
            ReleaseIgnisArtwork();
            ignisArtwork = IgnisCoreArt.Create(expeditionWave);
        }

        void ReleaseIgnisArtwork()
        {
            if (ignisArtwork == null) return;
            if (Application.isPlaying) Destroy(ignisArtwork); else DestroyImmediate(ignisArtwork);
            ignisArtwork = null;
        }

        static void IgnisPixel(float x, float y, float width, float height, Color color, float alpha = 1)
        {
            color.a *= alpha;
            Fill(new Rect(Mathf.Round(x / 2) * 2, Mathf.Round(y / 2) * 2, width, height), color);
        }

        void DrawIgnisReveal()
        {
            bool quiet = GamePreferences.Current.ReduceMotion;
            float progress = quiet ? 1 : Mathf.Clamp01(ignisAge / IgnisRevealDuration);
            float lift = 1 - Mathf.Pow(1 - progress, 3);
            Vector2 core = ignisGround + new Vector2(0, -22 - lift * 48 +
                (quiet ? 0 : Mathf.Sin(ignisAge * 2.2f) * 2));
            float glow = GamePreferences.Current.ReduceFlashes ? .07f : .12f;
            // Small stepped pools of light and a halo; no fullscreen flash or collection flight.
            for (int row = -2; row <= 2; row++)
            {
                float width = 80 - Mathf.Abs(row) * 16;
                IgnisPixel(ignisGround.x - width / 2, ignisGround.y + row * 2, width, 2, ignisColor, glow * progress);
            }
            for (int row = -9; row <= 9; row++)
            {
                float width = 88 - Mathf.Abs(row) * 4;
                IgnisPixel(core.x - width / 2, core.y + row * 4, width, 4, ignisColor, glow * progress);
            }
            if (!quiet && progress < 1)
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4;
                    float radius = (1 - progress) * 60 + 8;
                    IgnisPixel(core.x + Mathf.Cos(angle) * radius, core.y + Mathf.Sin(angle) * radius * .65f,
                        4, 4, ignisColor, Mathf.Sin(progress * Mathf.PI));
                }
            float visible = Mathf.Clamp01((progress - .15f) / .6f);
            if (ignisArtwork != null)
            {
                Color prior = GUI.color;
                float pulse = quiet || GamePreferences.Current.ReduceFlashes ? 1 : .97f + Mathf.Sin(ignisAge * 3) * .03f;
                GUI.color = new Color(pulse, pulse, pulse, visible);
                GUI.DrawTexture(new Rect(Mathf.Round(core.x - 46), Mathf.Round(core.y - 64), 92, 116),
                    ignisArtwork, ScaleMode.ScaleToFit, true);
                GUI.color = prior;
            }
            if (!quiet && progress >= 1)
                for (int i = 0; i < 4; i++)
                {
                    float phase = ignisAge * .55f + i * Mathf.PI / 2;
                    IgnisPixel(core.x + Mathf.Cos(phase) * 40, core.y + Mathf.Sin(phase) * 22 - 8,
                        2, 4, Color.Lerp(ignisColor, Color.white, .4f), .6f);
                }
            if (progress >= 1)
            {
                GUIStyle caption = dialogueName;
                Rect name = new Rect(core.x - 54, core.y + 54, 108, 28);
                SpeechPlate(name, new Color(.09f, .1f, .14f, .88f));
                GUI.Label(name, "IGNIS", caption);
            }
        }
    }
}
