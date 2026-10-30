using System.Collections.Generic;
using Expedition.Core;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        SeasonOneEndingView ending;
        bool endingQueued;
        readonly List<string> endingActors = new List<string>();
        void QueueEnding(CatalogBattle battle, bool victory)
        {
            endingQueued = SeasonOneEndingView.ShouldAutoPlay(victory, run.Wave,
                journal != null && journal.IsUnlocked("palace-clear"));
            endingActors.Clear();
            if (!victory || run.Wave != 30) return;
            foreach (var unit in battle.Units)
                if (unit.Team == CatalogTeam.Ally && unit.IsAlive) endingActors.Add(unit.FormId);
        }
        void FinishBattlePresentation()
        {
            enabled = true;
            if (!endingQueued) return;
            endingQueued = false;
            OpenEnding(false);
        }
        void OpenEnding(bool replay)
        {
            if (journal == null || !journal.IsUnlocked("palace-clear")) return;
            if (ending != null) ending.Dispose();
            if (replay)
            {
                endingActors.Clear();
                foreach (string form in journal.EndingParty)
                    try { if (database.Get(form) != null) endingActors.Add(form); }
                    catch (System.ArgumentException) { }
            }
            if (replay && endingActors.Count == 0 && run != null && run.LastVictory && run.Wave == 30)
                foreach (string id in run.DeployedIds)
                { var member = run.Roster.Find(id); if (member != null) endingActors.Add(member.FormId); }
            modal = Modal.None; CancelPointer();
            ending = new SeasonOneEndingView(endingActors, () => {});
        }
    }
}
