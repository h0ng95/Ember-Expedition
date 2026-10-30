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
                ignisGround = new Vector2(Mathf.Clamp(regionalBossGround.x + 180, 980, 1120), 510);
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
            if (storyProps == null || expeditionWave < 15) return;
            // The props sit at the distant edge of the ground, with restrained contrast and scale.
            if (expeditionWave == 15) storyProps.Draw(5, new Vector2(1120, 420), 74, 1, .7f);
            if (expeditionWave == 20) storyProps.Draw(4, new Vector2(1116, 440), 108, 1, .8f);
            if (expeditionWave == 25) storyProps.Draw(5, new Vector2(1120, 430), 88, 1, .72f);
            if (!regionalStoryActive) return;
            float t = RegionalStoryTime, progress = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .5f) / 2f));
            if (expeditionWave == 15)
            {
                // Heat drains out of the old conduit; it is not an item reward.
                for (int i = 0; i < 16; i++)
                    IgnisPixel(1096 + i * 6, 426 + i / 4 * 2, 6, 4, new Color(1, .4f, .12f, (1 - progress) * .5f));
            }
            else if (expeditionWave == 20)
            {
                for (int i = 0; i < 22 * progress; i++)
                {
                    float x = 1112 - i * 5, y = 443 + i * 2;
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
                    DrawForestSprout(new Vector2(1030 + i * 16, 548 + i % 2 * 6),
                        Mathf.Clamp01((t - 1.6f - i * .12f) / .65f), true);
                // The old outward stream fades; a few quiet motes return towards living roots.
                if (!GamePreferences.Current.ReduceMotion)
                    for (int i = 0; i < 4; i++)
                    {
                        float p = Mathf.Repeat(t * .3f + i * .25f, 1);
                        IgnisPixel(Mathf.Lerp(1120, 1040, p), Mathf.Lerp(430, 540, p), 4, 4,
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
                storyProps.Draw(6, foot + new Vector2(108, 20), 54, progress, .7f);
            }
        }
        void DrawRegionalClue()
        {
            if (!regionalStoryActive || storyProps == null) return;
            float show = Mathf.Clamp01((RegionalStoryTime - 1.2f) / .7f);
            // Small objects near the ground only; avoid large front-facing exhibits and floating labels.
            if (expeditionWave == 15) storyProps.Draw(3, new Vector2(1000, 570), 48, show, .85f);
            if (expeditionWave == 25 && dialogueIndex >= 1) storyProps.Draw(7, new Vector2(940, 568), 46, show, .8f);
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
