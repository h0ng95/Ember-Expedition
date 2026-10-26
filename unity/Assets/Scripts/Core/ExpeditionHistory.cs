using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    [Serializable]
    public sealed class ExpeditionRecord
    {
        public string SavedUtc;
        public bool Abandoned;
        public int Score, ScoreVersion;
        public string BalanceVersion;
        public ExpeditionRunSave FinalRun;
        public bool Ranked { get { return !Abandoned && FinalRun != null && FinalRun.Version >= 9; } }
    }
    [Serializable]
    public sealed class ExpeditionHistorySave
    {
        public int Version;
        public ExpeditionRecord[] Recent;
        public ExpeditionRecord Best;
        public bool HasBest;
        public ExpeditionRecord ArchivedBest;
        public bool HasArchivedBest;
    }

    public sealed class ExpeditionHistory
    {
        public const string BalanceVersion = "normal-046";
        public const int ScoreVersion = 1, Limit = 30;
        readonly List<ExpeditionRecord> recent = new List<ExpeditionRecord>();
        public IReadOnlyList<ExpeditionRecord> Recent { get { return recent.AsReadOnly(); } }
        public ExpeditionRecord Best { get; private set; }
        public ExpeditionRecord ArchivedBest { get; private set; }
        public bool Contains(string id)
        {
            foreach (var record in recent) if (record.FinalRun.RunId == id) return true;
            return Best != null && Best.FinalRun.RunId == id || ArchivedBest != null && ArchivedBest.FinalRun.RunId == id;
        }
        public bool Observe(ExpeditionRun run, bool abandon = false)
        {
            if (run == null || Contains(run.RunId) || !run.HasEnded && !abandon) return false;
            var record = new ExpeditionRecord { SavedUtc = DateTime.UtcNow.ToString("o"),
                Abandoned = !run.HasEnded, Score = run.UsesLegacyRules || !run.HasEnded ? 0 : run.Score,
                ScoreVersion = ScoreVersion, BalanceVersion = CatalogBalance.Version(run.RulesVersion),
                FinalRun = run.Capture() };
            recent.Insert(0, record);
            if (recent.Count > Limit) recent.RemoveAt(recent.Count - 1);
            if (record.Ranked) KeepBest(record);
            return true;
        }
        void KeepBest(ExpeditionRecord record)
        {
            if (record.BalanceVersion == BalanceVersion)
            { if (Best == null || record.Score > Best.Score) Best = record; }
            else if (ArchivedBest == null || record.Score > ArchivedBest.Score) ArchivedBest = record;
        }
        public ExpeditionHistorySave Capture()
        { return new ExpeditionHistorySave { Version = 2, Recent = recent.ToArray(), Best = Best, HasBest = Best != null,
            ArchivedBest = ArchivedBest, HasArchivedBest = ArchivedBest != null }; }
        public static ExpeditionHistory Restore(CatalogDatabase db, ExpeditionHistorySave state)
        {
            var result = new ExpeditionHistory();
            if (state == null) return result;
            if (state.Version > 2) throw new FutureSaveVersionException();
            if (state.Version < 1 || state.Recent == null || state.Recent.Length > Limit)
                throw new ArgumentException("Invalid expedition history.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in state.Recent)
            {
                ValidateRecord(db, record);
                if (!ids.Add(record.FinalRun.RunId)) throw new ArgumentException("Duplicate expedition record.");
                result.recent.Add(record);
            }
            if (state.HasBest)
            {
                ValidateRecord(db, state.Best);
                if (!state.Best.Ranked || state.Version == 2 && state.Best.BalanceVersion != BalanceVersion)
                    throw new ArgumentException("Ineligible best record.");
                result.KeepBest(state.Best);
            }
            if (state.Version == 2 && state.HasArchivedBest)
            {
                ValidateRecord(db, state.ArchivedBest);
                if (!state.ArchivedBest.Ranked || state.ArchivedBest.BalanceVersion != "normal-045")
                    throw new ArgumentException("Ineligible archived best record.");
                result.ArchivedBest = state.ArchivedBest;
            }
            foreach (var record in result.recent)
            {
                var best = record.BalanceVersion == BalanceVersion ? result.Best : result.ArchivedBest;
                if (record.Ranked && (best == null || record.Score > best.Score))
                    throw new ArgumentException("Best record does not match history.");
            }
            return result;
        }
        static void ValidateRecord(CatalogDatabase db, ExpeditionRecord record)
        {
            if (record == null || record.FinalRun == null) throw new ArgumentException("Missing expedition record.");
            DateTime stamp; Guid id;
            if (!DateTime.TryParse(record.SavedUtc, out stamp) || !Guid.TryParseExact(record.FinalRun.RunId, "N", out id))
                throw new ArgumentException("Invalid record identity.");
            if (record.ScoreVersion > ScoreVersion || record.FinalRun.Version > 10)
                throw new FutureSaveVersionException();
            var run = ExpeditionRun.Restore(db, record.FinalRun);
            if (record.ScoreVersion != ScoreVersion || record.Abandoned == run.HasEnded ||
                record.BalanceVersion != CatalogBalance.Version(run.RulesVersion) ||
                record.Score != (record.Ranked ? run.Score : 0))
                throw new ArgumentException("Invalid record result or score.");
        }
    }
}
