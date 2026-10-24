using System;

namespace Expedition.Core
{
    // Progress belongs to the player, not one expedition. A new or lost run cannot erase it.
    [Serializable]
    public sealed class ExpeditionProgressSave
    {
        public int Version;
        public int BestWave;
        public int CompletedWaves;
    }

    public sealed class ExpeditionProgress
    {
        const int SaveVersion = 1;
        // This save schema must continue accepting fifteen-wave clears after more content ships.
        const int FirstContentWaves = 15;
        int bestWave, completedWaves;

        public int BestWave { get { return bestWave; } }
        public int CompletedWaves { get { return completedWaves; } }
        public bool HardUnlocked { get { return completedWaves >= ExpeditionRun.ReleaseWaves; } }

        public static ExpeditionProgress New() { return new ExpeditionProgress(); }

        public static ExpeditionProgress Restore(ExpeditionProgressSave save)
        {
            if (save == null || save.Version != SaveVersion ||
                save.BestWave < 0 || save.BestWave > ExpeditionRun.ReleaseWaves ||
                save.CompletedWaves < 0 || save.CompletedWaves > save.BestWave ||
                save.CompletedWaves > 0 && save.CompletedWaves < FirstContentWaves)
                throw new ArgumentException("Invalid expedition progress save.");
            return new ExpeditionProgress { bestWave = save.BestWave,
                completedWaves = save.CompletedWaves };
        }

        public ExpeditionProgressSave Capture()
        { return new ExpeditionProgressSave { Version = SaveVersion,
            BestWave = bestWave, CompletedWaves = completedWaves }; }

        public bool Observe(ExpeditionRun run)
        {
            if (run == null) throw new ArgumentNullException("run");
            if (run.Difficulty != ExpeditionDifficulty.Normal)
                throw new ArgumentException("Only Normal progress is available.", "run");
            bool changed = false;
            if (run.Victories > bestWave)
            { bestWave = run.Victories; changed = true; }
            bool cleared = run.Phase == ExpeditionPhase.Completed ||
                run.Phase == ExpeditionPhase.Result && run.LastVictory &&
                    run.Wave == run.ContentWaves;
            if (cleared && run.ContentWaves > completedWaves)
            { completedWaves = run.ContentWaves; changed = true; }
            return changed;
        }
    }
}
