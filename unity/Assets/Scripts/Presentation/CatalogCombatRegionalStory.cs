using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class CatalogCombatLab
    {
        public const float RegionalStoryDuration = 3.6f;
        const float RegionalIgnisDelay = 1.1f;
        bool regionalStoryActive;
        float regionalStoryAge;
        Vector2 regionalBossGround;
        StoryPixelArt storyProps;
        float RegionalStoryTime { get { return GamePreferences.Current.ReduceMotion ? RegionalStoryDuration : regionalStoryAge; } }

        void LoadRegionalScenery() { if (expeditionWave >= 10 && expeditionWave % 5 == 0) storyProps = new StoryPixelArt(); }
        void ReleaseRegionalScenery() { if (storyProps != null) storyProps.Dispose(); storyProps = null; regionalStoryActive = false; regionalStoryAge = 0; }
        bool HoldRegionalGuardian(CatalogBattleUnit unit)
        {
            return expeditionMode && expeditionWave >= 15 && expeditionWave % 5 == 0 &&
                unit.Team == CatalogTeam.Enemy && unit.IsBoss && !unit.IsAlive && expeditionPresentationFinished != null;
        }
        void BeginRegionalStory()
        {
            regionalStoryActive = false; regionalStoryAge = 0;
            if (expeditionWave < 15) return;
            foreach (var unit in battle.Units) if (HoldRegionalGuardian(unit))
            {
                regionalStoryActive = true;
                regionalBossGround = ExpeditionPoint(new Vector2(unit.X, unit.Y), HudRules.BattleArena);
                ignisGround = BossStoryReveal.CoreGround(regionalBossGround);
                break;
            }
        }
        void AdvanceRegionalStory(float dt)
        {
            regionalStoryAge += Mathf.Max(0, dt);
            ignisAge = Mathf.Max(0, RegionalStoryTime - RegionalIgnisDelay);
        }
        void CompleteRegionalReveal() { if (regionalStoryActive) regionalStoryAge = RegionalStoryDuration; }
        string RegionalStoryHint { get { return expeditionWave == 15 ? "동력관의 열기가 잦아든다" :
            expeditionWave == 20 ? "얼음 아래 작은 흐름이 돌아온다" :
            expeditionWave == 25 ? "빛이 다시 뿌리 쪽으로 흐른다" : "수호룡을 묶던 빛이 끊어진다"; } }

        void DrawRegionalScenery()
        {
            if (!regionalStoryActive || storyProps == null || expeditionWave < 15) return;
            float t = RegionalStoryTime, progress = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .5f) / 2f));
            Vector2 clue = BossStoryReveal.ClueGround(ignisGround);
            if (expeditionWave == 15)
            {
                // Heat drains out of the old conduit; it is not an item reward.
                for (int i = 0; i < 16; i++)
                    IgnisPixel(regionalBossGround.x - 30 + i * 4, regionalBossGround.y - 12 + i / 4 * 2,
                        4, 4, new Color(1, .4f, .12f, (1 - progress) * .5f));
            }
            else if (expeditionWave == 20)
            {
                float revealed = BossStoryReveal.Alpha(true, ignisAge, GamePreferences.Current.ReduceMotion);
                for (int i = 0; i < 16 * revealed; i++)
                {
                    float x = clue.x - i * 7, y = clue.y + 4 + i % 3 * 2;
                    IgnisPixel(x, y, 12, 6, new Color32(27, 67, 86, 255));
                    if ((i + (GamePreferences.Current.ReduceMotion ? 0 : (int)(t * 4))) % 4 == 0)
                        IgnisPixel(x, y, 8, 2, new Color32(125, 204, 218, 255));
                }
                // A small matching snowflake links the ancient altar and the core.
                DrawCoreSnowflake(ignisGround + new Vector2(0, -68), progress);
            }
            else if (expeditionWave == 25)
            {
                for (int i = 0; i < 7; i++)
                    DrawForestSprout(new Vector2(ignisGround.x - 68 + i * 16, ignisGround.y + 26 + i % 2 * 6),
                        Mathf.Clamp01((t - 1.6f - i * .12f) / .65f), true);
                // The old outward stream fades; a few quiet motes return towards living roots.
                if (!GamePreferences.Current.ReduceMotion)
                    for (int i = 0; i < 4; i++)
                    {
                        float p = Mathf.Repeat(t * .3f + i * .25f, 1);
                        IgnisPixel(Mathf.Lerp(ignisGround.x, ignisGround.x - 48, p),
                            Mathf.Lerp(ignisGround.y - 52, ignisGround.y + 28, p), 4, 4,
                            new Color(.63f, .73f, .62f, .5f * progress));
                    }
            }
            else if (expeditionWave == 30)
            {
                Vector2 foot = regionalBossGround;
                // Restraint light breaks away from the whole guardian; no separated body parts.
                for (int i = 0; i < 10; i++)
                {
                    float side = i < 5 ? -1 : 1;
                    IgnisPixel(foot.x + side * (76 + progress * 36), foot.y - 98 + i % 5 * 12 + progress * 48,
                        8, 6, new Color(.94f, .77f, .43f, (1 - progress) * .7f));
                }
            }
        }
        void DrawRegionalClue()
        {
            if (!regionalStoryActive || storyProps == null) return;
            float show = BossStoryReveal.Alpha(regionalStoryActive, ignisAge, GamePreferences.Current.ReduceMotion);
            int index = BossStoryReveal.ClueIndex(expeditionWave);
            if (index >= 0) storyProps.Draw(index, BossStoryReveal.ClueGround(ignisGround),
                BossStoryReveal.ClueWidth(expeditionWave), show, .9f);
        }
        static void DrawCoreSnowflake(Vector2 center, float alpha)
        {
            Color c = new Color(.72f, .94f, 1, alpha * .8f);
            for (int i = -3; i <= 3; i++)
            {
                IgnisPixel(center.x, center.y + i * 3, 2, 2, c);
                IgnisPixel(center.x + i * 3, center.y + i * 2, 2, 2, c);
                IgnisPixel(center.x + i * 3, center.y - i * 2, 2, 2, c);
            }
        }
    }
}
