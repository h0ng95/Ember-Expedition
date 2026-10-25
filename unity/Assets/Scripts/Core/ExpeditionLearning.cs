using System;

namespace Expedition.Core
{
    public enum ExpeditionLesson { None = 0, Growth = 1, Evolution = 2, Tactics = 4 }

    public sealed class ExpeditionGrowthExample
    {
        public readonly string DonorId, RecipientId;
        public ExpeditionGrowthExample(string donor, string recipient) { DonorId = donor; RecipientId = recipient; }
    }

    public static class ExpeditionLearning
    {
        public static bool Ready(CatalogMember member)
        {
            return member != null && (member.FormId.StartsWith("B-", StringComparison.Ordinal) && member.Growth >= 2 ||
                member.FormId.StartsWith("E-", StringComparison.Ordinal) && member.Growth >= 6);
        }
        static bool Deployed(ExpeditionRun run, string id)
        { foreach (string active in run.DeployedIds) if (active == id) return true; return false; }

        // Teach with an unlocked base-form reserve. Never suggest sacrificing a field
        // member or an evolved/hybrid reserve just to complete an introduction.
        public static ExpeditionGrowthExample GrowthExample(ExpeditionRun run, int benchPage = -1)
        {
            if (run == null || run.Phase != ExpeditionPhase.Preparation) return null;
            int bench = -1;
            foreach (CatalogMember donor in run.Roster.Members)
            {
                if (Deployed(run, donor.Id)) continue;
                bench++;
                if (benchPage >= 0 && bench / 5 != benchPage || donor.MaterialLocked || !donor.FormId.StartsWith("B-", StringComparison.Ordinal)) continue;
                foreach (string id in run.DeployedIds)
                {
                    CatalogMember recipient = run.Roster.Find(id);
                    bool growing = recipient.FormId.StartsWith("B-", StringComparison.Ordinal) || recipient.FormId.StartsWith("E-", StringComparison.Ordinal);
                    if (growing && !Ready(recipient) && run.Roster.CanTransfer(donor.Id, id, false))
                        return new ExpeditionGrowthExample(donor.Id, id);
                }
            }
            return null;
        }
        public static CatalogMember EvolutionExample(ExpeditionRun run)
        {
            if (run != null && run.Phase == ExpeditionPhase.Preparation)
                foreach (CatalogMember member in run.Roster.Members) if (Ready(member)) return member;
            return null;
        }
        public static bool CanExploreTactics(ExpeditionRun run)
        {
            if (run == null) return false;
            foreach (CatalogResearchNode node in CatalogResearch.Nodes) if (run.CanPurchaseTactic(node.Id)) return true;
            return false;
        }
        public static int RestoreMask(int stored) { return stored >= 0 && stored <= 7 ? stored : 7; }
        public static int ExistingKnowledge(ExpeditionRun run)
        {
            int mask = 0;
            if (run == null) return mask;
            foreach (CatalogMember member in run.Roster.Members)
                if (!member.FormId.StartsWith("B-", StringComparison.Ordinal)) { mask |= 3; break; }
            if (run.TacticIds.Count > 0) mask |= 4;
            return mask;
        }
    }
}
