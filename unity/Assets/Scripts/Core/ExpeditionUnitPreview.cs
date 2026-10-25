using System;

namespace Expedition.Core
{
    public sealed class ExpeditionUnitPreview
    {
        public readonly CatalogMember Member;
        public readonly CatalogBattleUnit Stats;
        public readonly int DeployedCount, RosterCount;

        ExpeditionUnitPreview(CatalogDatabase database, ExpeditionRun source, string memberId)
        {
            Member = source.Roster.Find(memberId).Copy();
            Stats = new CatalogBattle(database, source.TacticIds, source.LegacyResearchIds).AddUnit(
                "info-preview", CatalogTeam.Ally, Member.FormId, Member.BranchId, Member.InheritedTraitId, 0, 0);
            DeployedCount = source.DeployedIds.Count;
            RosterCount = source.Roster.Members.Count;
        }

        // Run the real preparation operation on a detached snapshot. Opening or changing
        // a preview never spends gold, advances RNG, removes a member, or adds Undo history.
        public static ExpeditionUnitPreview Create(CatalogDatabase database, ExpeditionRun source,
            string memberId, string resultId = null, string donorId = null)
        {
            if (source == null || source.Roster.Find(memberId) == null) return null;
            ExpeditionRun projected = source;
            if (resultId != null)
            {
                projected = ExpeditionRun.Restore(database, source.Capture());
                bool valid = donorId == null ? projected.Evolve(memberId, resultId) :
                    projected.Combine(memberId, donorId, resultId);
                if (!valid) return null;
            }
            return new ExpeditionUnitPreview(database, projected, memberId);
        }
    }
}
