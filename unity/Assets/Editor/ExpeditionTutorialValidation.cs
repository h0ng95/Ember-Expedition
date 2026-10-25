using System;
using System.Collections.Generic;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class ExpeditionTutorialValidation
    {
        static int assertions;
        static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("Expedition tutorial: " + message);
        }

        static ExpeditionTutorialStep Step(int wave, int mask, bool replay, int gold,
            int roster, int bench, int deployed, bool canSummon, bool selected = false,
            bool acknowledged = false)
        {
            return ExpeditionTutorialRules.Step(wave, mask, replay, gold, roster, bench,
                deployed, canSummon, selected, acknowledged);
        }

        [MenuItem("Expedition/Validate Tutorial")]
        public static void Validate()
        {
            assertions = 0;
            Check(ExpeditionTutorialRules.RestoreMask(false, 0, false) == 0,
                "new player begins with both guides available");
            Check(ExpeditionTutorialRules.RestoreMask(false, 0, true) == 3,
                "existing run or account does not receive forced guidance");
            Check(ExpeditionTutorialRules.RestoreMask(true, 0, true) == 0,
                "saved new-player preference survives a later run save");
            Check(ExpeditionTutorialRules.RestoreMask(true, 99, false) == 3,
                "invalid tutorial-only preference fails closed");

            Check(Step(1, 0, false, 40, 3, 0, 3, true) == ExpeditionTutorialStep.Summon,
                "first preparation points to summon");
            Check(Step(1, 0, false, 20, 4, 1, 3, true) == ExpeditionTutorialStep.Deploy,
                "reconnected preparation points to bench after summon");
            Check(Step(1, 0, false, 20, 4, 0, 4, true) == ExpeditionTutorialStep.Start,
                "deployed recruit moves guide to battle");
            Check(Step(1, 0, false, 0, 3, 0, 3, false) == ExpeditionTutorialStep.Start,
                "no affordable summon never blocks battle");
            Check(Step(1, 0, false, 0, 6, 1, 5, false) == ExpeditionTutorialStep.Start,
                "full field never demands another placement");
            Check(Step(1, 1, false, 40, 3, 0, 3, true) == ExpeditionTutorialStep.None &&
                Step(2, 1, false, 0, 3, 0, 3, false) == ExpeditionTutorialStep.Select,
                "skipping first guide retains second guide");
            Check(Step(1, 1, true, 20, 4, 0, 4, true) == ExpeditionTutorialStep.Start,
                "manual replay reflects current actions rather than demanding a second summon");

            Check(Step(2, 0, false, 0, 3, 0, 3, false) == ExpeditionTutorialStep.Select &&
                Step(2, 0, false, 0, 3, 0, 3, false, true) == ExpeditionTutorialStep.Range &&
                Step(2, 0, false, 0, 3, 0, 3, false, true, true) == ExpeditionTutorialStep.Start,
                "second guide advances selection, explanation, battle");
            Check(Step(2, 3, false, 0, 3, 0, 3, false) == ExpeditionTutorialStep.None,
                "completed guidance stays silent");
            Check(Step(12, 3, false, 200, 5, 0, 5, true) == ExpeditionTutorialStep.None &&
                Step(12, 3, true, 200, 5, 0, 5, true) == ExpeditionTutorialStep.RangeReplay,
                "late replay only describes current range");

            TextAsset content = Resources.Load<TextAsset>("Content/unit-catalog-v010");
            Check(content != null, "catalog exists for read-only state check");
            var run = ExpeditionRun.New(CatalogDatabase.FromJson(content.text), 23);
            string before = JsonUtility.ToJson(run.Capture());
            Step(run.Wave, 0, false, run.Gold, run.Roster.Members.Count,
                run.Roster.Members.Count - run.DeployedIds.Count, run.DeployedIds.Count,
                run.CanSummon);
            Check(JsonUtility.ToJson(run.Capture()) == before,
                "guide selection leaves gold, RNG, roster and run save unchanged");

            var holes = new List<Rect> { new Rect(10, 10, 40, 40), new Rect(30, 30, 40, 40) };
            var dark = new List<Rect>();
            ExpeditionTutorialMask.DarkRects(new Rect(0, 0, 100, 100), holes, dark);
            float darkArea = 0f;
            foreach (Rect tile in dark)
            {
                darkArea += tile.width * tile.height;
                foreach (Rect hole in holes)
                    Check(!tile.Overlaps(hole), "dark tile covers a spotlight opening");
            }
            Check(Mathf.Abs(darkArea - 7200f) < .01f,
                "overlapping openings are subtracted once without double-dimming");
            var card = new Rect(72, 2, 26, 20);
            Check(!ExpeditionTutorialMask.AllowsPointer(card, holes, new Vector2(5, 5)) &&
                ExpeditionTutorialMask.AllowsPointer(card, holes, new Vector2(15, 15)) &&
                ExpeditionTutorialMask.AllowsPointer(card, holes, new Vector2(80, 10)),
                "dark input is blocked while target and guide controls remain usable");
            Check(ExpeditionTutorialMask.AllowsDeployRelease(holes, new Vector2(60, 60)) &&
                !ExpeditionTutorialMask.AllowsDeployRelease(holes, new Vector2(15, 15)),
                "drag release applies only at the empty deployment opening");
            Debug.Log("Expedition tutorial validated: " + assertions + " assertions.");
        }
    }
}
