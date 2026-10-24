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
    }

    public sealed class ExpeditionProgress
    {
        const int SaveVersion = 2;
        // This save schema must continue accepting fifteen-wave clears after more content ships.
        const int FirstContentWaves = 15;
        int bestWave, completedWaves;
        readonly List<string> researchIds = new List<string>();

        public int BestWave { get { return bestWave; } }
        public int CompletedWaves { get { return completedWaves; } }
        public bool HardUnlocked { get { return completedWaves >= ExpeditionRun.ReleaseWaves; } }
        public IReadOnlyList<string> ResearchIds { get { return researchIds.AsReadOnly(); } }
        // First clears only: one point per new battle, plus one for each new boss.
        // Deriving the balance from durable progress prevents replay/reload double rewards.
        public int ResearchEarned { get { return bestWave + bestWave / 5; } }
        public int ResearchPoints { get { return ResearchEarned - CatalogLegacyResearch.SelectionCost(researchIds); } }

        public bool CanPurchaseResearch(string id)
        {
            var node = CatalogLegacyResearch.Get(id);
            return node != null && !researchIds.Contains(id) && ResearchPoints >= node.Cost &&
                (node.PrerequisiteId == null || researchIds.Contains(node.PrerequisiteId));
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
                completedWaves = save.CompletedWaves };
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
            ResearchIds = researchIds.ToArray() }; }

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
