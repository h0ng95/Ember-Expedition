using System;

namespace Expedition.Core
{
    [Serializable]
    public sealed class ExpeditionAccountSave
    {
        public int Version;
        public ExpeditionRunSave Run;
        public ExpeditionProgressSave Progress;
        public ExpeditionJournalSave Journal;
        public int TutorialMask, LessonMask;
        public ExpeditionHistorySave History;
        public bool HasRun;

        public void Validate(CatalogDatabase database)
        {
            if (Version > 2 || Run != null && Run.Version > 10 || Progress != null && Progress.Version > 3 || Journal != null && Journal.Version > 1)
                throw new FutureSaveVersionException();
            if (Version < 1 || Version == 2 && History == null || Progress == null || Journal == null || Journal.Version != 1 || Journal.UnlockedIds == null ||
                TutorialMask < 0 || TutorialMask > 3 || LessonMask < 0 || LessonMask > 7)
                throw new ArgumentException("Invalid account snapshot.");
            var progress = ExpeditionProgress.Restore(Progress);
            var history = ExpeditionHistory.Restore(database, Version >= 2 ? History : null);
            foreach (string id in Journal.UnlockedIds)
                if (ExpeditionStory.Get(id) == null) throw new ArgumentException("Invalid story record.");
            bool hasRun = Version >= 2 ? HasRun : Run != null && Run.Version != 0;
            if (hasRun)
            {
                var run = ExpeditionRun.Restore(database, Run);
                if (Version == 2 && run.HasEnded && !history.Contains(run.RunId))
                    throw new ArgumentException("Finished expedition missing from history.");
                if (progress.Observe(run)) throw new ArgumentException("Run and progress are from different checkpoints.");
                var journal = ExpeditionJournal.Restore(Journal);
                if (journal.Observe(run)) throw new ArgumentException("Run and story are from different checkpoints.");
            }
        }
    }
}
