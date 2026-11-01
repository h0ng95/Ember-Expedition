using System;
using System.IO;
using System.Linq;
using Expedition.Core;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class MidgameFlowValidation
    {
        static int checks;
        static void Check(bool condition, string why)
        { checks++; if (!condition) throw new Exception("Midgame validation: " + why); }
        public static void ValidateAndBuild()
        { Validate(); PrototypeSetup.BuildAndroid(); }
        public static void Validate()
        {
            checks = 0;
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var current = ExpeditionRun.New(db, 67001);
            var oldSave = current.Capture(); oldSave.Version = 13; oldSave.RulesVersion = 5;
            var previous = ExpeditionRun.Restore(db, oldSave);
            Check(current.Capture().Version == 14 && previous.Capture().Version == 13,
                "new runs use v14, existing v13 runs stay v13");
            // Forced victories here only reach static encounter definitions. These are NOT balance samples.
            int changed = 0;
            for (int wave = 1; wave <= 30; wave++)
            {
                var before = previous.BeginBattle(); var after = current.BeginBattle();
                Check(before.Units.Count == after.Units.Count, "same encounter composition " + wave);
                foreach (var unit in before.Units)
                {
                    var next = after.Get(unit.Id);
                    bool furnace = wave == 15 && unit.Id == "furnace-boss";
                    Check(next != null && unit.Power == next.Power && unit.Defense == next.Defense &&
                        unit.AttackInterval == next.AttackInterval && unit.Range == next.Range &&
                        unit.MoveSpeed == next.MoveSpeed && unit.X == next.X && unit.Y == next.Y &&
                        unit.EnemyRole == next.EnemyRole && unit.BossPatternsEnabled == next.BossPatternsEnabled &&
                        unit.DrakeRangedBasic == next.DrakeRangedBasic, "only intended HP changes " + wave + "/" + unit.Id);
                    Check(furnace ? unit.MaxHp == 6000 && next.MaxHp == 5700 : unit.MaxHp == next.MaxHp,
                        "HP compatibility " + wave + "/" + unit.Id);
                    if (unit.MaxHp != next.MaxHp) changed++;
                }
                previous.ResolveBattle(true); current.ResolveBattle(true);
                previous.Continue(); current.Continue();
            }
            Check(changed == 1, "exactly one encounter HP change across all 30 waves");
            var history = new ExpeditionHistory(); history.Observe(previous);
            var oldHistory = history.Capture(); oldHistory.Version = 5;
            oldHistory.Best = oldHistory.ArchivedBests.Single(); oldHistory.HasBest = true;
            oldHistory.ArchivedBests = new ExpeditionRecord[0];
            string original = JsonUtility.ToJson(oldHistory);
            var restored = ExpeditionHistory.Restore(db, oldHistory);
            Check(restored.Best == null && restored.ArchivedBests.Count == 1 &&
                restored.ArchivedBests[0].BalanceVersion == "normal-052", "old best archived under its actual balance rules");
            Check(original == JsonUtility.ToJson(oldHistory), "migration does not mutate original history");
            // Distinct identity for the second finished run in this record-only fixture.
            var completed = current.Capture(); completed.RunId = Guid.NewGuid().ToString("N");
            restored.Observe(ExpeditionRun.Restore(db, completed));
            var roundtrip = ExpeditionHistory.Restore(db, restored.Capture());
            Check(roundtrip.Best.BalanceVersion == "normal-067" && roundtrip.ArchivedBests.Count == 1 &&
                roundtrip.Recent.Count == 2, "old and new results round-trip separately");
            string source = Path.GetFullPath("../.local-tools/v067-holdout");
            string output = "Logs/v067-fixtures"; Directory.CreateDirectory(output);
            foreach (string name in new[] { "legal-15-timeout", "legal-18-defeat", "legal-19-defeat", "legal-18-win" })
            {
                string json = File.ReadAllText(Path.Combine(source, name + ".json"));
                var run = ExpeditionRun.Restore(db, JsonUtility.FromJson<ExpeditionRunSave>(json));
                var battle = run.BeginBattle(); var report = new CatalogBattleReport(battle);
                while (!battle.Ended && battle.Time < 75f) battle.Step(battle.Time < 74.75f ? .25f : 1f / 60f);
                bool won = battle.Ended && battle.Winner == CatalogTeam.Ally;
                run.ResolveBattle(won); report.Complete(won);
                Check(report.Outcome.ToString().ToLowerInvariant() == (name.EndsWith("win") ? "victory" : name.Split('-').Last()),
                    "real legal checkpoint produces expected failure evidence " + name);
                Check(CatalogBattleFeedback.Summary(report).Length > 0 && report.Duration > 0,
                    "completion evidence " + name);
                File.WriteAllText(Path.Combine(output, name + ".json"), json);
            }
            File.WriteAllText("Logs/v067-validation.txt", "PASS " + checks + " checks; 30-wave rules and old/new history compatibility; four actual battle outcomes.");
            Debug.Log("MIDGAME_VALIDATION_PASS " + checks);
        }
    }
}
