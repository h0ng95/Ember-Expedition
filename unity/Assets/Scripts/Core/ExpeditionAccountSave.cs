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

        public void Validate(CatalogDatabase database)
        {
            if (Version > 1 || Run != null && Run.Version > 8 || Progress != null && Progress.Version > 2 || Journal != null && Journal.Version > 1)
                throw new FutureSaveVersionException();
            if (Version != 1 || Progress == null || Journal == null || Journal.Version != 1 || Journal.UnlockedIds == null ||
                TutorialMask < 0 || TutorialMask > 3 || LessonMask < 0 || LessonMask > 7)
                throw new ArgumentException("Invalid account snapshot.");
            var progress = ExpeditionProgress.Restore(Progress);
            foreach (string id in Journal.UnlockedIds)
                if (ExpeditionStory.Get(id) == null) throw new ArgumentException("Invalid story record.");
            if (Run != null)
            {
                var run = ExpeditionRun.Restore(database, Run);
                if (progress.Observe(run)) throw new ArgumentException("Run and progress are from different checkpoints.");
                var journal = ExpeditionJournal.Restore(Journal);
                if (journal.Observe(run)) throw new ArgumentException("Run and story are from different checkpoints.");
            }
        }
    }
}
