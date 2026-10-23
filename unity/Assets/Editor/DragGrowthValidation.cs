using System;
using System.IO;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class DragGrowthValidation
    {
        const string Key = "ember.expedition.run.v013";
        static int assertions;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Check(bool value, string message)
        {
            assertions++;
            if (!value) throw new InvalidOperationException("Drag growth: " + message);
        }
        static void Set(ExpeditionScreen screen, string field, object value)
        { typeof(ExpeditionScreen).GetField(field, Private).SetValue(screen, value); }
        static object Get(ExpeditionScreen screen, string field)
        { return typeof(ExpeditionScreen).GetField(field, Private).GetValue(screen); }
        static void Call(ExpeditionScreen screen, string method, params object[] args)
        { typeof(ExpeditionScreen).GetMethod(method, Private).Invoke(screen, args); }
        static void Point(ExpeditionScreen screen, Vector2 point, bool down, bool held, bool up, float now)
        { Call(screen, "ProcessPointer", point, down, held, up, now); }
        static Vector2 Target(ExpeditionRun run, string id)
        {
            var member = run.Roster.Find(id);
            return new Vector2(560 + member.X * 75, 380 - member.Y * 50 - 45);
        }
        static void Drag(ExpeditionScreen screen, Vector2 from, Vector2 to, float hold)
        {
            Point(screen, from, true, true, false, 0);
            Point(screen, to, false, true, false, .1f);
            Point(screen, to, false, true, false, .1f + hold);
            Point(screen, to, false, false, true, .12f + hold);
        }
        static void Attach(ExpeditionScreen screen, ExpeditionRun run)
        {
            Call(screen, "CancelPointer");
            Set(screen, "run", run);
            Set(screen, "home", false);
            Set(screen, "badSave", false);
            Set(screen, "benchPage", 0);
            var field = typeof(ExpeditionScreen).GetField("modal", Private);
            field.SetValue(screen, Enum.ToObject(field.FieldType, 0));
        }
        public static void Validate()
        {
            assertions = 0;
            bool existed = PlayerPrefs.HasKey(Key);
            string original = PlayerPrefs.GetString(Key, "");
            var go = new GameObject("Drag growth validation");
            go.SetActive(false);
            try
            {
                var database = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
                var screen = go.AddComponent<ExpeditionScreen>();
                Set(screen, "database", database);
                var run = ExpeditionRun.New(database, 72);
                string donor = run.Summon();
                Attach(screen, run);
                Vector2 bench = new Vector2(220, 620), target = Target(run, "ally-01");
                Drag(screen, bench, target, .5f);
                Check(run.Roster.Find(donor) == null && run.Roster.Find("ally-01").Growth == 1,
                    "held bench donor grows field recipient once");
                Check(run.Undo() && run.Roster.Find(donor) != null, "drag growth Undo restores donor");
                run.SetMaterialLocked(donor, true);
                run = ExpeditionRun.Restore(database, JsonUtility.FromJson<ExpeditionRunSave>(JsonUtility.ToJson(run.Capture())));
                Check(run.Roster.Find(donor).MaterialLocked, "JSON retains protection");
                Attach(screen, run);
                Drag(screen, bench, target, .5f);
                Check(run.Roster.Find(donor) != null && run.Roster.Find("ally-01").Growth == 0,
                    "protected drag cannot consume a member");

                run = ExpeditionRun.New(database, 73); donor = run.Summon(); Attach(screen, run);
                Point(screen, bench, true, true, false, 0);
                Point(screen, target, false, true, false, .1f);
                Point(screen, target, false, true, false, .7f);
                Call(screen, "CancelPointer");
                Point(screen, target, false, false, true, .8f);
                Check(run.Roster.Find(donor) != null && !run.CanUndo, "canceled gesture never executes");
                Drag(screen, bench, bench, 1f);
                Check(run.Roster.Find(donor) != null && !run.CanUndo && Get(screen, "modal").ToString() == "Detail",
                    "stationary long press only opens details");
                Attach(screen, run);
                Point(screen, bench, true, true, false, 0);
                Point(screen, new Vector2(180, 470), false, true, false, .1f);
                Point(screen, target, false, false, true, 2f);
                Check(run.Roster.Find(donor) != null, "jumping onto target on release never consumes");

                run = ExpeditionRun.New(database, 74);
                string first = run.Summon(), second = run.Summon();
                run.Transfer(first, "ally-01", false); Attach(screen, run);
                Drag(screen, bench, Target(run, "ally-01"), .5f);
                Check(run.Roster.Find(second) == null && run.Roster.Find("ally-01").Growth == 2 &&
                    Get(screen, "modal").ToString() == "Confirm" && (string)Get(screen, "selectedId") == "ally-01",
                    "threshold opens evolution for the recipient immediately");
                Call(screen, "ExecuteGrowth");
                Check(run.Roster.Find("ally-01").FormId.StartsWith("E-KN-") && Get(screen, "modal").ToString() == "None",
                    "evolution after drag returns to board");

                // A legal later-wave budget creates a device fixture with two grown branches.
                var save = ExpeditionRun.New(database, 75).Capture();
                save.Wave = 6; save.Victories = 5; save.RewardedWave = 5; save.Gold = 180; save.LastReward = 50;
                run = ExpeditionRun.Restore(database, save);
                for (int i = 0; i < 2; i++) run.Transfer(run.Summon(), "ally-01", false);
                run.Evolve("ally-01", "E-KN-G");
                for (int i = 0; i < 2; i++) run.Transfer(run.Summon(), "ally-03", false);
                run.Evolve("ally-03", "E-PR-H");
                run.Move("ally-01", -1.3f, 0); run.Move("ally-02", -4.5f, 1.8f); run.Move("ally-03", -4.5f, -1.8f);
                run.Summon(); run.Summon();
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/v016-device-choice.json", JsonUtility.ToJson(run.Capture()));
                Attach(screen, run);
                Check(run.GetCombinationOptions().Count == 1, "legal pair is discoverable without dragging");
                int historyCount = run.Capture().UndoHistory.Length;
                Call(screen, "OpenCombinationOptions");
                Call(screen, "ChooseCombinationOption", 0);
                Check(Get(screen, "modal").ToString() == "Confirm" &&
                    run.Roster.Find("ally-03") != null && run.Roster.Find("ally-01").FormId == "E-KN-G" &&
                    run.Capture().UndoHistory.Length == historyCount,
                    "viewing an opportunity and its confirmation has no roster effect");
                Attach(screen, run); // Closing/ignoring the offer leaves regular growth available.
                Drag(screen, Target(run, "ally-03"), Target(run, "ally-01"), .5f);
                Check(run.Roster.Find("ally-03") == null && run.Roster.Find("ally-01").FormId == "E-KN-G" &&
                    run.Roster.Find("ally-01").Growth == 3 && run.DeployedIds.Count == 2,
                    "dragging even a legal hybrid pair only adds growth and NEVER auto-combines");
                Check(run.Undo() && run.GetCombinationOptions().Count == 1, "Undo recovers the optional combination");
                Call(screen, "OpenCombinationOptions");
                Call(screen, "ChooseCombinationOption", 0);
                Call(screen, "ExecuteGrowth");
                Check(run.Roster.Find("ally-01").FormId == "H-02" && run.Roster.Find("ally-03") == null &&
                    Get(screen, "modal").ToString() == "None", "explicit confirmed combination executes and returns to board");
                Check(run.Undo() && run.DeployedIds.Count == 3, "explicit combination Undo restores both slots");

                // Five active units: a quick drop swaps, a held drop grows.
                foreach (var member in run.Roster.Members) run.SetDeployed(member.Id, true);
                donor = run.Summon(); Attach(screen, run);
                Drag(screen, bench, Target(run, "ally-02"), .05f);
                Check(run.Roster.Find(donor) != null && run.DeployedIds.Count == 5 &&
                    System.Linq.Enumerable.Contains(run.DeployedIds, donor), "quick full-field drop swaps without consuming");

                // Legacy saves omit the newly added optional protection flag.
                string legacy = JsonUtility.ToJson(run.Capture()).Replace(",\"MaterialLocked\":false", "");
                Check(ExpeditionRun.Restore(database, JsonUtility.FromJson<ExpeditionRunSave>(legacy)).Wave == 6,
                    "old saves without protection flag remain loadable");
                Debug.Log("PASS: drag growth input and JSON validation, " + assertions + " assertions.");
            }
            finally
            {
                if (existed) PlayerPrefs.SetString(Key, original); else PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        public static void BuildAndroid()
        {
            Validate();
            PrototypeSetup.BuildAndroid();
        }
    }
}
