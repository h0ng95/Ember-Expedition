using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class CatalogCombatLab
    {
        // A short, unscaled vignette. The cub is a stage prop, never a battle unit.
        public const float ForestStoryDuration = 3.2f;
        const float ForestIgnisDelay = 1.5f;
        bool forestStoryActive;
        float forestStoryAge;
        Vector2 forestGuardianGround;
        float ForestStoryTime { get { return GamePreferences.Current.ReduceMotion ? ForestStoryDuration : forestStoryAge; } }
        bool DialogueReady { get { return IgnisReady && (!forestStoryActive || ForestStoryTime >= ForestStoryDuration); } }

        bool HoldForestGuardian(CatalogBattleUnit unit)
        {
            return expeditionMode && expeditionWave == 5 && !unit.IsAlive && unit.Team == CatalogTeam.Enemy &&
                unit.IsBoss && unit.EnemyRole == CatalogEnemyRole.Beast && expeditionPresentationFinished != null;
        }

        void BeginForestStory()
        {
            forestStoryActive = false; forestStoryAge = 0;
            if (expeditionWave != 5) return;
            foreach (var unit in battle.Units)
                if (HoldForestGuardian(unit))
                {
                    forestStoryActive = true;
                    forestGuardianGround = ExpeditionPoint(new Vector2(unit.X, unit.Y), HudRules.BattleArena);
                    // The light and growing shoots share the ground plane with the guardian.
                    ignisGround = new Vector2(Mathf.Clamp(forestGuardianGround.x + 238, 700, 1150),
                        Mathf.Clamp(forestGuardianGround.y + 18, 380, 590));
                    break;
                }
        }

        void AdvanceForestStory(float dt)
        {
            if (!forestStoryActive) { ignisAge += dt; return; }
            forestStoryAge += Mathf.Max(0, dt);
            ignisAge = Mathf.Max(0, ForestStoryTime - ForestIgnisDelay);
        }

        void CompleteForestReveal()
        {
            if (forestStoryActive) forestStoryAge = Mathf.Max(forestStoryAge, ForestStoryDuration);
            ignisAge = Mathf.Max(ignisAge, IgnisRevealDuration);
        }

        Vector2 ForestStoryAllyPoint(CatalogBattleUnit unit, Vector2 point)
        {
            if (!forestStoryActive || unit.Team != CatalogTeam.Ally || !unit.IsAlive) return point;
            // A restrained step back, without moving combat coordinates or changing equipment.
            if (point.x < forestGuardianGround.x && forestGuardianGround.x - point.x < 250)
                point.x -= 16 * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, 1.15f, ForestStoryTime));
            return point;
        }

        void DrawForestStoryGround()
        {
            if (!forestStoryActive) return;
            // Existing dry shoots outside the small living patch keep the clue local.
            DrawForestSprout(ignisGround + new Vector2(-102, 10), 1, false);
            DrawForestSprout(ignisGround + new Vector2(82, -18), 1, false);
            for (int i = 0; i < 5; i++)
            {
                float grow = Mathf.Clamp01((ForestStoryTime - 1.75f - i * .12f) / .65f);
                // In front of the core's label, rather than directly underneath it.
                Vector2 spot = ignisGround + new Vector2((i - 2) * 22, i % 2 == 0 ? 38 : 32);
                DrawForestSprout(spot, grow, true);
            }
        }

        static void DrawForestSprout(Vector2 foot, float growth, bool living)
        {
            if (growth <= 0) return;
            float height = 4 + Mathf.Floor(growth * (living ? 9 : 5)) * 2;
            Color dark = living ? new Color32(37, 76, 55, 255) : new Color32(69, 51, 42, 255);
            Color stem = living ? new Color32(115, 167, 87, 255) : new Color32(130, 101, 69, 255);
            Color leaf = new Color32(169, 210, 116, 255);
            IgnisPixel(foot.x - 5, foot.y, 12, 2, new Color(0, 0, 0, .3f));
            IgnisPixel(foot.x - 2, foot.y - height, 4, height, dark);
            IgnisPixel(foot.x, foot.y - height, 2, height - 2, stem);
            if (growth < .45f) return;
            IgnisPixel(foot.x - 8, foot.y - height + 4, 8, living ? 6 : 4, dark);
            IgnisPixel(foot.x + 2, foot.y - height, 8, living ? 6 : 4, dark);
            IgnisPixel(foot.x - 6, foot.y - height + 4, 6, 2, living ? leaf : stem);
            IgnisPixel(foot.x + 2, foot.y - height, 6, 2, living ? leaf : stem);
        }

        void DrawForestStoryCub()
        {
            if (!forestStoryActive || forestEnemyArt == null || ForestStoryTime < .35f) return;
            float approach = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1.65f, ForestStoryTime));
            Vector2 foot = forestGuardianGround + new Vector2(Mathf.Lerp(224, 160, approach), 12);
            foot.x = Mathf.Min(foot.x, 1204);
            int frame = approach < 1 ? 6 + (int)(ForestStoryTime * 8) % 6 : 19;
            var matrix = GUI.matrix;
            var color = GUI.color;
            try
            {
                // Keep the existing complete sprite and its equipment/body together.
                GUIUtility.ScaleAroundPivot(new Vector2(.66f, .66f), foot);
                GUI.color = Color.white;
                IgnisPixel(foot.x - 24, foot.y + 1, 48, 4, new Color(0, 0, 0, .3f));
                Rect bounds;
                forestEnemyArt.Draw("forest-cub", frame, foot, out bounds);
            }
            finally { GUI.matrix = matrix; GUI.color = color; }
        }
    }
}
