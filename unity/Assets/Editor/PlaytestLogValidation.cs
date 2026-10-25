using System;
using System.IO;
using System.Text;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class PlaytestLogValidation
    {
        static int checks;
        static void Check(bool value, string message)
        {
            checks++;
            if (!value) throw new InvalidOperationException("Playtest diagnostics: " + message);
        }

        [MenuItem("Expedition/Validate Playtest Diagnostics")]
        public static void Validate()
        {
            checks = 0;
            string root = Path.GetFullPath(Path.Combine("Temp", "playtest-validation-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            var database = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var run = ExpeditionRun.New(database, 75);
            var control = ExpeditionRun.New(database, 75);
            var log = new PlaytestLog(Path.Combine(root, "normal"), "validation");
            string initial = JsonUtility.ToJson(run.Capture());
            log.Begin(run.Capture(), 75, 0);
            Check(JsonUtility.ToJson(run.Capture()) == initial, "recording must not mutate save or undo stacks");
            run.Summon(); control.Summon();
            log.Record("summon", run.Capture(), 2);
            var exported = JsonUtility.FromJson<PlaytestExport>(log.ExportRecent());
            Check(exported.events.Length == 2 && exported.events[1].goldDelta == -20, "summon cost delta");
            Check(exported.events[0].initialSeedKnown && exported.events[0].initialSeed == 75, "new seed provenance");
            Check(JsonUtility.ToJson(run.Capture()) == JsonUtility.ToJson(control.Capture()), "RNG and formation unaffected");
            var reproduced = ExpeditionRun.Restore(database, exported.events[1].state);
            Check(JsonUtility.ToJson(reproduced.Capture()) == JsonUtility.ToJson(run.Capture()), "exported checkpoint can be restored by the game");
            var nextOriginal = ExpeditionRun.Restore(database, run.Capture());
            string reproducedId = reproduced.Summon(), originalId = nextOriginal.Summon();
            Check(reproduced.Roster.Find(reproducedId).FormId == nextOriginal.Roster.Find(originalId).FormId,
                "export preserves the next random summon");
            var reproducedBattle = ExpeditionRun.Restore(database, exported.events[1].state).BeginBattle();
            var battle = run.BeginBattle();
            var other = control.BeginBattle();
            log.Record("battle_start", run.Capture(), 4);
            for (int i = 0; i < 20000 && (!battle.Ended || !other.Ended); i++)
            { battle.Step(.02f); other.Step(.02f); reproducedBattle.Step(.02f); }
            Check(battle.Ended && other.Ended && reproducedBattle.Ended && battle.Winner == other.Winner &&
                battle.Time == other.Time && battle.Time == reproducedBattle.Time && battle.Winner == reproducedBattle.Winner,
                "battle outcome and clock unchanged including exported checkpoint");
            run.ResolveBattle(battle.Winner == CatalogTeam.Ally);
            control.ResolveBattle(other.Winner == CatalogTeam.Ally);
            log.Record("battle_result", run.Capture(), 30, battleSeconds: battle.Time);
            Check(JsonUtility.ToJson(run.Capture()) == JsonUtility.ToJson(control.Capture()), "rewards and lanterns unchanged");
            log.Record("app_background", null, 31);
            log.Record("app_resume", null, 31);
            log.Begin(run.Capture(), null, 32);
            exported = JsonUtility.FromJson<PlaytestExport>(log.ExportRecent());
            Check(!exported.events[exported.events.Length - 1].initialSeedKnown, "restored checkpoint must not invent original seed");
            Check(exported.events[exported.events.Length - 3].action == "app_background" &&
                exported.events[exported.events.Length - 2].action == "app_resume", "interruption is not defeat");
            Check(!exported.events[exported.events.Length - 3].hasState &&
                exported.events[exported.events.Length - 1].hasState,
                "explicit state presence survives Unity serialization of null nested classes");
            for (int i = 0; i < 70; i++) log.Record("checkpoint", run.Capture(), 33 + i);
            exported = JsonUtility.FromJson<PlaytestExport>(log.ExportRecent());
            Check(exported.recentEventsOnly && exported.events.Length <= 60 && Encoding.UTF8.GetByteCount(log.ExportRecent()) <= PlaytestLog.CopyBytes, "bounded copy explicitly identifies partial trace");
            foreach (string line in File.ReadAllLines(log.FilePath)) Check(line.StartsWith("{") && line.EndsWith("}"), "atomic JSONL lines");
            using (var stream = new FileStream(log.FilePath, FileMode.Open, FileAccess.Write)) stream.SetLength(PlaytestLog.MaxBytes - 300);
            log.Record("over_limit", run.Capture(), 104);
            Check(log.Stopped && new FileInfo(log.FilePath).Length <= PlaytestLog.MaxBytes, "bounded full log stops with visible notice");
            string blocked = Path.Combine(root, "blocked"); File.WriteAllText(blocked, "not a directory");
            var failed = new PlaytestLog(blocked, "validation");
            failed.Begin(run.Capture(), null, 0);
            Check(failed.Stopped && failed.Error != null, "I/O failure never escapes into gameplay");
            for (int i = 0; i < 10; i++) new PlaytestLog(Path.Combine(root, "retention"), "validation");
            Check(Directory.GetFiles(Path.Combine(root, "retention"), "*.jsonl").Length == PlaytestLog.MaxSessions, "session retention bounded");
            Debug.Log("Playtest diagnostics validated: " + checks + " assertions; evidence in " + root);
        }

        public static void ValidateAndBuild()
        {
            Validate();
            BattleReadabilityValidation.Validate();
            BossSpriteEffectsValidation.Validate();
            CombatSettlementValidation.Validate();
            BattleVisibilityValidation.Validate();
            PrototypeSetup.BuildAndroid();
        }
    }
}
