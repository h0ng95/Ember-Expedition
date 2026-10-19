using System;
using System.Collections.Generic;
using Expedition.Core;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    // Exercises the real Unity serializer, which the pure C# tests cannot cover.
    public static class ExpeditionRunValidation
    {
        static int assertions;

        static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("Run validation: " + message);
        }

        static ExpeditionRun RoundTrip(CatalogDatabase database, ExpeditionRun run)
        {
            string json = JsonUtility.ToJson(run.Capture());
            ExpeditionRun restored = ExpeditionRun.Restore(database, JsonUtility.FromJson<ExpeditionRunSave>(json));
            Check(JsonUtility.ToJson(restored.Capture()) == json, "JSON round trip changed run state");
            return restored;
        }

        [MenuItem("Expedition/Validate Saved Expedition")]
        public static void Validate()
        {
            assertions = 0;
            TextAsset json = Resources.Load<TextAsset>("Content/unit-catalog-v010");
            var database = CatalogDatabase.FromJson(json.text);
            var run = RoundTrip(database, ExpeditionRun.New(database, 75));
            var copy = RoundTrip(database, run);
            string id = run.Summon(), other = copy.Summon();
            Check(run.Roster.Find(id).FormId == copy.Roster.Find(other).FormId, "Reload rerolled summon");
            run.Move("ally-01", -1.8f, .2f);
            run.Transfer(id, "ally-01", true);
            run = RoundTrip(database, run);
            Check(run.Undo() && run.Roster.Find(id) != null && run.SummonsRemaining == 0,
                "Saved undo lost a confirmed summon or refunded its allowance");

            int completions = 0;
            for (uint seed = 1; seed <= 8; seed++)
            {
                run = ExpeditionRun.New(database, seed);
                while (run.Phase == ExpeditionPhase.Preparation)
                {
                    while (run.SummonsRemaining > 0)
                    {
                        id = run.Summon();
                        if (run.DeployedIds.Count < 5) run.SetDeployed(id, true);
                        else
                        {
                            string recipientId = run.Roster.Find("ally-01").FormId == "F-KN" ? "ally-02" : "ally-01";
                            run.Transfer(id, recipientId, false);
                            CatalogMember recipient = run.Roster.Find(recipientId);
                            if (recipient.FormId == "B-KN" && recipient.Growth == 2) run.Evolve(recipientId, "E-KN-G");
                            if (recipient.FormId == "E-KN-G" && recipient.Growth == 6) run.Evolve(recipientId, "F-KN");
                            if (recipient.FormId == "B-AR" && recipient.Growth == 2) run.Evolve(recipientId, "E-AR-P");
                        }
                    }
                    run = RoundTrip(database, run);
                    CatalogBattle battle = run.BeginBattle();
                    Check(battle != null, "Preparation could not start battle");
                    battle.Step(2f);
                    run = RoundTrip(database, run);
                    battle = run.ResumeBattle();
                    Check(battle.Time == 0f, "Saved battle did not resume at its starting state");
                    foreach (CatalogBattleUnit unit in battle.Units)
                        Check(unit.Hp == unit.MaxHp, "Restarted actor did not have starting HP");
                    while (!battle.Ended && battle.Time < 75f) battle.Step(1f / 30f);
                    bool victory = battle.Ended && battle.Winner == CatalogTeam.Ally;
                    Check(run.ResolveBattle(victory) && !run.ResolveBattle(victory), "Result applied twice");
                    run = RoundTrip(database, run);
                    run.Continue();
                }
                run = RoundTrip(database, run);
                if (run.Phase == ExpeditionPhase.Completed) completions++;
                Debug.Log("V011 sample seed=" + seed + " phase=" + run.Phase + " wave=" + run.Wave +
                    " victories=" + run.Victories + " supply=" + run.Roster.SupplyUsed);
            }
            Check(completions > 0, "No sample party could complete the introductory expedition");
            Debug.Log("PASS: Unity expedition save and complete-run validation, " + assertions +
                " assertions; sample completions " + completions + "/8. This is an integration check, not release balance.");
        }

        public static void BuildAndroid()
        {
            Validate();
            PrototypeSetup.BuildAndroid();
        }
    }
}
