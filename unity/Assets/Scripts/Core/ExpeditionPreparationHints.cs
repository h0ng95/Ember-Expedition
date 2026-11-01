using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    // Read-only guidance for boss preparations. Nothing here gates a battle or changes a roster.
    public static class ExpeditionPreparationHints
    {
        static readonly string[] Empty = new string[0];
        static readonly CatalogLocalizedStrings Fallback = new CatalogLocalizedStrings("text.0210",
            "text.0211",
            "text.0212");

        public static string[] ForBoss(ExpeditionRun run, CatalogDatabase catalog)
        {
            if (run == null) throw new ArgumentNullException("run");
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (run.Phase != ExpeditionPhase.Preparation || !run.IsBossWave) return Empty;

            var lines = new List<string>(3);
            int bench = run.Roster.Members.Count - run.DeployedIds.Count;
            if (run.DeployedIds.Count < 5 && bench > 0)
                lines.Add(CatalogText.Get("text.0213") + run.DeployedIds.Count + CatalogText.Get("text.0214") + bench + CatalogText.Get("text.0006"));

            CatalogMember ready = null;
            foreach (string id in run.DeployedIds)
            {
                CatalogMember member = run.Roster.Find(id);
                if (member.FormId.StartsWith("B-", StringComparison.Ordinal) && member.Growth >= 2 ||
                    member.FormId.StartsWith("E-", StringComparison.Ordinal) && member.Growth >= 6)
                { ready = member; break; }
            }
            if (ready != null)
                lines.Add(catalog.Get(ready.FormId).Name +
                    (ready.FormId.StartsWith("B-", StringComparison.Ordinal) ?
                        CatalogText.Get("text.0215") : CatalogText.Get("text.0216")));

            if (run.Wave >= 10)
            {
                foreach (string id in run.DeployedIds)
                {
                    CatalogMember member = run.Roster.Find(id);
                    int needed = member.FormId == "B-KN" ? 2 - member.Growth :
                        member.FormId.StartsWith("E-KN-", StringComparison.Ordinal) ? 6 - member.Growth : 0;
                    // Ready forms were handled above; hybrids and final forms have no such growth path.
                    if (needed <= 0) continue;
                    lines.Add(catalog.Get(member.FormId).Name +
                        (member.FormId == "B-KN" ? CatalogText.Get("text.0217") :
                            CatalogText.Get("text.0218")) + needed);
                    break;
                }
            }

            if (lines.Count < 3)
                foreach (string id in run.DeployedIds)
                    if (run.Roster.Find(id).FormId == "H-06")
                    {
                        lines.Add(CatalogText.Get("text.0219"));
                        break;
                    }

            for (int i = 0; lines.Count < 3; i++) lines.Add(Fallback[i]);
            if (lines.Count > 3) lines.RemoveRange(3, lines.Count - 3);
            return lines.ToArray();
        }
    }
}
