using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        static bool CanGrow(CatalogMember member)
        {
            return member.FormId.StartsWith("B-", StringComparison.Ordinal) && member.Growth < 2 ||
                member.FormId.StartsWith("E-", StringComparison.Ordinal) && member.Growth < 6;
        }

        static string GrowthForm(CatalogMember member)
        {
            string family = CatalogRoster.FamilyCode(member.FormId);
            if (member.FormId.StartsWith("E-", StringComparison.Ordinal)) return "F-" + family;
            switch (family)
            {
                case "KN": return "E-KN-G"; case "AR": return "E-AR-P";
                case "PR": return "E-PR-H"; case "FI": return "E-FI-B";
                case "IC": return "E-IC-F"; case "BA": return "E-BA-G";
                default: throw new InvalidOperationException("Unknown growth family: " + family);
            }
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
            Check(run.Undo() && run.Roster.Find(id) != null && run.Gold == 20,
                "Saved undo lost a confirmed summon or refunded its gold");

            int completions = 0;
            var recordedWaves = new HashSet<int>();
            for (uint seed = 40001; seed <= 40008; seed++)
            {
                run = ExpeditionRun.New(database, seed);
                while (run.Phase == ExpeditionPhase.Preparation)
                {
                    // Current 30-wave fixture: finish a tank, healer, then the other
                    // active allies. Buy one affordable defensive node from wave 11.
                    // The old 6-wave fixture never finished its archer or grew a healer.
                    if (run.Wave >= 11)
                        foreach (string node in new[] { "bulwark", "shieldcraft", "marksmanship" })
                            if (run.CanPurchaseTactic(node))
                            { Check(run.PurchaseTactic(node), "Affordable tactic rejected"); break; }
                    while (run.CanSummon)
                    {
                        CatalogMember recipient = run.DeployedIds.Select(active => run.Roster.Find(active))
                            .Where(CanGrow).OrderBy(member => member.Id == "ally-01" ? 0 :
                                CatalogRoster.FamilyCode(member.FormId) == "PR" ? 1 :
                                CatalogRoster.FamilyCode(member.FormId) == "KN" ? 2 : 3).FirstOrDefault();
                        if (run.DeployedIds.Count == 5 && recipient == null) break;
                        id = run.Summon();
                        Check(id != null, "Affordable summon rejected");
                        if (run.DeployedIds.Count < 5)
                        {
                            Check(run.SetDeployed(id, true), "Legal deployment rejected");
                        }
                        else
                        {
                            string recipientId = recipient.Id;
                            Check(run.Transfer(id, recipientId, false), "Legal growth rejected");
                            recipient = run.Roster.Find(recipientId);
                            if (recipient.Growth == 2 && recipient.FormId.StartsWith("B-", StringComparison.Ordinal) ||
                                recipient.Growth == 6 && recipient.FormId.StartsWith("E-", StringComparison.Ordinal))
                                Check(run.Evolve(recipientId, GrowthForm(recipient)), "Ready evolution rejected");
                        }
                    }
                    // Keep backline out of the tank's slam radius; same formation as the balance probe.
                    int backline = 0, frontline = 0;
                    foreach (string activeId in run.DeployedIds)
                    {
                        CatalogMember member = run.Roster.Find(activeId);
                        bool compact = run.Lantern == 2;
                        if (CatalogRoster.FamilyCode(member.FormId) == "KN")
                        {
                            float[] lanes = compact ? new[] { -.45f, .45f, -1.25f, 1.25f, 0f } :
                                new[] { 0f, -1.7f, 1.7f, -.85f, .85f };
                            float x = compact ? -1.5f : -1.4f, y = lanes[frontline++];
                            Check(member.X == x && member.Y == y || run.Move(activeId, x, y), "Frontline placement rejected");
                        }
                        else
                        {
                            float[] lanes = compact ? new[] { -1.1f, 0f, 1.1f, -1.8f, 1.8f } :
                                new[] { 1.5f, -1.5f, .5f, -.5f, 0f };
                            float x = compact ? -2.6f : -3.3f, y = lanes[backline++];
                            Check(member.X == x && member.Y == y || run.Move(activeId, x, y), "Backline placement rejected");
                        }
                    }
                    run = RoundTrip(database, run);
                    if ((run.Wave == 5 || run.Wave == 10) && recordedWaves.Add(run.Wave))
                    {
                        Directory.CreateDirectory("Logs");
                        File.WriteAllText("Logs/v014-device-wave" + run.Wave + ".json", JsonUtility.ToJson(run.Capture()));
                    }
                    CatalogBattle battle = run.BeginBattle();
                    Check(battle != null, "Preparation could not start battle");
                    battle.Step(2f);
                    run = RoundTrip(database, run);
                    battle = run.ResumeBattle();
                    Check(battle.Time == 0f, "Saved battle did not resume at its starting state");
                    foreach (CatalogBattleUnit unit in battle.Units)
                        Check(unit.Hp == unit.MaxHp, "Restarted actor did not have starting HP");
                    while (!battle.Ended && battle.Time < 75f) battle.Step(1f / 60f);
                    bool victory = battle.Ended && battle.Winner == CatalogTeam.Ally;
                    Check(run.ResolveBattle(victory) && !run.ResolveBattle(victory), "Result applied twice");
                    run = RoundTrip(database, run);
                    Check(run.Continue(), "Result could not advance");
                }
                run = RoundTrip(database, run);
                if (run.Phase == ExpeditionPhase.Completed) completions++;
                Debug.Log("V040 sample seed=" + seed + " phase=" + run.Phase + " wave=" + run.Wave +
                    " victories=" + run.Victories + " supply=" + run.Roster.SupplyUsed + " gold=" + run.Gold);
            }
            Check(completions > 0, "No sample party could complete the 30-wave expedition");
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
