using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    // Progress belongs to the player, not one expedition. A new or lost run cannot erase it.
    [Serializable]
    public sealed class ExpeditionProgressSave
    {
        public int Version;
        public int BestWave;
        public int CompletedWaves;
        public string[] ResearchIds;
        public int ArchivedResearchEarned;
    }

    public sealed class ExpeditionProgress
    {
        const int SaveVersion = 3;
        // This save schema must continue accepting fifteen-wave clears after more content ships.
        const int FirstContentWaves = 15;
        int bestWave, completedWaves;
        int archivedResearchEarned;
        readonly List<string> researchIds = new List<string>();

        public int BestWave { get { return bestWave; } }
        public int CompletedWaves { get { return completedWaves; } }
        public bool HardUnlocked { get { return completedWaves >= ExpeditionRun.ReleaseWaves; } }
        public IReadOnlyList<string> ResearchIds { get { return researchIds.AsReadOnly(); } }
        // Freeze the old first-clear point total during migration. New progress
        // still records best clears, but never grants permanent research points.
        public int ResearchEarned { get { return archivedResearchEarned; } }
        public int ResearchPoints { get { return ResearchEarned - CatalogLegacyResearch.SelectionCost(researchIds); } }

        public bool CanPurchaseResearch(string id)
        {
            return false; // Retained records are historical; permanent purchases are retired.
        }

        public bool PurchaseResearch(string id)
        {
            if (!CanPurchaseResearch(id)) return false;
            researchIds.Add(id);
            return true;
        }

        public static ExpeditionProgress New() { return new ExpeditionProgress(); }

        public static ExpeditionProgress Restore(ExpeditionProgressSave save)
        {
            if (save == null || save.Version < 1 || save.Version > SaveVersion ||
                save.BestWave < 0 || save.BestWave > ExpeditionRun.ReleaseWaves ||
                save.CompletedWaves < 0 || save.CompletedWaves > save.BestWave ||
                save.CompletedWaves > 0 && save.CompletedWaves < FirstContentWaves)
                throw new ArgumentException("Invalid expedition progress save.");
            var result = new ExpeditionProgress { bestWave = save.BestWave,
                completedWaves = save.CompletedWaves,
                archivedResearchEarned = save.Version < 3 ? save.BestWave + save.BestWave / 5 : save.ArchivedResearchEarned };
            if (result.archivedResearchEarned < 0 || result.archivedResearchEarned > save.BestWave + save.BestWave / 5)
                throw new ArgumentException("Invalid archived research points.");
            if (save.Version == 1)
            {
                if (save.ResearchIds != null && save.ResearchIds.Length != 0)
                    throw new ArgumentException("Legacy progress cannot contain research.");
            }
            else
            {
                int cost = CatalogLegacyResearch.SelectionCost(save.ResearchIds);
                if (cost > result.ResearchEarned)
                    throw new ArgumentException("Research exceeds earned points.");
                result.researchIds.AddRange(save.ResearchIds);
            }
            return result;
        }

        public ExpeditionProgressSave Capture()
        { return new ExpeditionProgressSave { Version = SaveVersion,
            BestWave = bestWave, CompletedWaves = completedWaves,
            ResearchIds = researchIds.ToArray(), ArchivedResearchEarned = archivedResearchEarned }; }

        public bool Observe(ExpeditionRun run)
        {
            if (run == null) throw new ArgumentNullException("run");
            if (run.Difficulty != ExpeditionDifficulty.Normal)
                throw new ArgumentException("Only Normal progress is available.", "run");
            bool changed = false;
            if (run.Victories > bestWave)
            { bestWave = run.Victories; changed = true; }
            if (run.PreviousClearWaves > completedWaves)
            { completedWaves = run.PreviousClearWaves; changed = true; }
            bool cleared = run.Phase == ExpeditionPhase.Completed ||
                run.Phase == ExpeditionPhase.Result && run.LastVictory &&
                    run.Wave == run.ContentWaves;
            if (cleared && run.ContentWaves > completedWaves)
            { completedWaves = run.ContentWaves; changed = true; }
            return changed;
        }
    }
}
