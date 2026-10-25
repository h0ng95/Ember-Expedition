using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    [Serializable]
    public sealed class PlaytestEvent
    {
        public int sequence;
        public string utc, action, detail, runId;
        public double foregroundSeconds;
        public bool initialSeedKnown;
        public uint initialSeed;
        public int goldDelta;
        public float battleSeconds = -1;
        public bool hasState;
        public ExpeditionRunSave state;
    }

    [Serializable]
    public sealed class PlaytestExport
    {
        public string schema = "ignis-playtest-1", build = "v0.39", appVersion, sessionId;
        public string scope = "local development session; restored runs have unknown initial seed; undo history omitted";
        public bool recentEventsOnly, recordingStopped;
        public PlaytestEvent[] events;
    }

    // Diagnostic data never participates in PlayerPrefs, progression, RNG or battle logic.
    public sealed class PlaytestLog
    {
        public const int MaxSessions = 8, MaxBytes = 2 * 1024 * 1024;
        public const int CopyBytes = 180 * 1024;
        readonly List<PlaytestEvent> recent = new List<PlaytestEvent>();
        readonly string sessionId = Guid.NewGuid().ToString("N"), appVersion;
        int sequence, lastGold;
        string runId;
        bool hasGold, seedKnown;
        uint seed;
        public string FilePath { get; private set; }
        public string Error { get; private set; }
        public bool Stopped { get; private set; }
        public int Count { get { return sequence; } }

        public PlaytestLog(string directory, string version)
        {
            appVersion = version;
            try
            {
                Directory.CreateDirectory(directory);
                var old = new DirectoryInfo(directory).GetFiles("session-*.jsonl");
                Array.Sort(old, (a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                for (int i = 0; i <= old.Length - MaxSessions; i++) old[i].Delete();
                FilePath = Path.Combine(directory, "session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + sessionId + ".jsonl");
                var header = new PlaytestExport { appVersion = version, sessionId = sessionId, events = Array.Empty<PlaytestEvent>() };
                File.WriteAllText(FilePath, JsonUtility.ToJson(header) + "\n", new UTF8Encoding(false));
            }
            catch (Exception exception) { Fail(exception); }
        }

        public void Begin(ExpeditionRunSave state, uint? initialSeed, double foregroundSeconds)
        {
            runId = Guid.NewGuid().ToString("N");
            seedKnown = initialSeed.HasValue;
            seed = initialSeed ?? 0;
            hasGold = false;
            Record(initialSeed.HasValue ? "new_run" : "restored_checkpoint", state, foregroundSeconds,
                initialSeed.HasValue ? "" : "New diagnostic segment; earlier choices and original seed are unknown.");
        }

        public void Record(string action, ExpeditionRunSave state, double foregroundSeconds,
            string detail = null, float battleSeconds = -1)
        {
            if (Stopped) return;
            try
            {
                // Capture data is detached; omit large undo stacks only in this diagnostic copy.
                if (state != null)
                {
                    state.UndoHistory = Array.Empty<ExpeditionPreparationSave>();
                    if (state.Roster != null) state.Roster.History = Array.Empty<CatalogRosterHistorySave>();
                }
                var entry = new PlaytestEvent { sequence = sequence + 1,
                    utc = DateTime.UtcNow.ToString("o"), action = action, detail = detail ?? "",
                    runId = runId, initialSeedKnown = seedKnown, initialSeed = seed,
                    foregroundSeconds = foregroundSeconds, battleSeconds = battleSeconds,
                    goldDelta = state != null && hasGold ? state.Gold - lastGold : 0,
                    hasState = state != null, state = state };
                string line = JsonUtility.ToJson(entry) + "\n";
                if (new FileInfo(FilePath).Length + Encoding.UTF8.GetByteCount(line) > MaxBytes - 256)
                {
                    File.AppendAllText(FilePath, "{\"action\":\"recording_limit\",\"incomplete\":true}\n", new UTF8Encoding(false));
                    Stopped = true;
                    Error = "기록 용량 한도에 도달했습니다. 앱을 다시 열면 새 기록을 시작합니다.";
                    return;
                }
                File.AppendAllText(FilePath, line, new UTF8Encoding(false));
                sequence++;
                if (state != null) { lastGold = state.Gold; hasGold = true; }
                recent.Add(entry);
                if (recent.Count > 60) recent.RemoveAt(0);
            }
            catch (Exception exception) { Fail(exception); }
        }

        public string ExportRecent()
        {
            var entries = new List<PlaytestEvent>(recent);
            while (true)
            {
                var export = new PlaytestExport { appVersion = appVersion, sessionId = sessionId,
                    recentEventsOnly = entries.Count < sequence, recordingStopped = Stopped, events = entries.ToArray() };
                string json = JsonUtility.ToJson(export, true);
                if (Encoding.UTF8.GetByteCount(json) <= CopyBytes || entries.Count == 0) return json;
                entries.RemoveAt(0);
            }
        }

        void Fail(Exception exception)
        {
            Stopped = true;
            Error = "진단 기록 저장 실패: " + exception.GetType().Name;
            // The game's own save and play continue even if diagnostics cannot write.
        }
    }

}
