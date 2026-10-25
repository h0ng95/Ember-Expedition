using System;
using System.IO;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Stays enabled while ExpeditionScreen is disabled for combat.
    public sealed class PlaytestRecorder : MonoBehaviour
    {
        public PlaytestLog Log { get; private set; }
        double foregroundSeconds, lastClock;
        bool paused, focused = true;

        void Awake()
        {
            lastClock = Time.realtimeSinceStartupAsDouble;
            if (Debug.isDebugBuild || Application.isEditor)
                Log = new PlaytestLog(Path.Combine(Application.persistentDataPath, "PlaytestLogs"), Application.version);
        }
        void Tick()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!paused && focused) foregroundSeconds += Math.Max(0, now - lastClock);
            lastClock = now;
        }
        public void Begin(ExpeditionRun run, uint? initialSeed)
        {
            if (Log == null || run == null) return;
            Tick();
            Log.Begin(run.Capture(), initialSeed, foregroundSeconds);
        }
        public void Record(string action, ExpeditionRun run = null, string detail = null, float battleSeconds = -1)
        {
            if (Log == null) return;
            Tick();
            Log.Record(action, run == null ? null : run.Capture(), foregroundSeconds, detail, battleSeconds);
        }
        void OnApplicationPause(bool value)
        {
            Tick();
            if (paused == value) return;
            paused = value;
            Record(value ? "app_background" : "app_resume");
        }
        void OnApplicationFocus(bool value)
        {
            Tick();
            if (focused == value) return;
            focused = value;
            Record(value ? "focus_gained" : "focus_lost");
        }
        void OnApplicationQuit() { Record("app_quit", detail: "Not a battle defeat; force-stop may have no quit event."); }
    }
}
