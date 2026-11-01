using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class CatalogCombatLab
    {
        public const string CastleStoryArtPath = StoryPixelArt.ResourcePath;
        public const float CastleStoryDuration = 2.8f;
        const float CastleIgnisDelay = 1.25f;
        bool castleStoryActive;
        float castleStoryAge;
        Vector2 castleTabletGround;
        float CastleStoryTime { get { return GamePreferences.Current.ReduceMotion ? CastleStoryDuration : castleStoryAge; } }
        bool HoldCastleGolem(CatalogBattleUnit unit)
        {
            return expeditionMode && expeditionWave == 10 && !unit.IsAlive && unit.Team == CatalogTeam.Enemy &&
                CastleEnemyArt.SpeciesFor(unit) == "castle-boss" && expeditionPresentationFinished != null;
        }
        bool HoldStoryBoss(CatalogBattleUnit unit)
        { return HoldForestGuardian(unit) || HoldCastleGolem(unit) || HoldRegionalGuardian(unit); }
        void BeginCastleStory()
        {
            castleStoryActive = false; castleStoryAge = 0;
            if (expeditionWave != 10) return;
            foreach (var unit in battle.Units) if (HoldCastleGolem(unit))
            {
                castleStoryActive = true;
                Vector2 ground = ExpeditionPoint(new Vector2(unit.X, unit.Y), HudRules.BattleArena);
                ignisGround = BossStoryReveal.CoreGround(ground);
                castleTabletGround = BossStoryReveal.ClueGround(ignisGround);
                break;
            }
        }
        void AdvanceCastleStory(float dt)
        { castleStoryAge += Mathf.Max(0, dt); ignisAge = Mathf.Max(0, CastleStoryTime - CastleIgnisDelay); }
        void CompleteCastleReveal()
        { if (castleStoryActive) castleStoryAge = Mathf.Max(castleStoryAge, CastleStoryDuration); }
        void DrawCastleStoryScenery()
        {
            if (!castleStoryActive) return;
            float fade = 1 - Mathf.Clamp01(CastleStoryTime / 1.4f);
            // The old guard circuit gutters out beside the stopped golem.
            for (int i = 0; i < 8; i++)
                IgnisPixel(castleTabletGround.x - 90 + i * 12, castleTabletGround.y - 2, 6, 2,
                    new Color(1, .64f, .18f, fade * .6f));
        }
        void DrawCastleStoryClue()
        {
            if (!castleStoryActive || storyProps == null) return;
            storyProps.Draw(1, castleTabletGround, BossStoryReveal.ClueWidth(10),
                BossStoryReveal.Alpha(castleStoryActive, ignisAge, GamePreferences.Current.ReduceMotion), .9f);
        }
    }
}
