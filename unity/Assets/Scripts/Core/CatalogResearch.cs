using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public sealed class CatalogResearchNode
    {
        public string Id { get; private set; }
        public string BranchId { get; private set; }
        public int Tier { get; private set; }
        public int Cost { get; private set; }
        public int UnlockWave { get; private set; }
        public int RequiredBossWave { get { return UnlockWave - 1; } }
        public string RequiredRegion { get { return Tier == 1 ? CatalogText.Get("text.0042") : CatalogText.Get("text.0043"); } }
        public string PrerequisiteId { get; private set; }
        string localizedTitle;
        public string Title { get { return CatalogText.Resolve(localizedTitle); } private set { localizedTitle = value; } }
        string localizedDescription;
        public string Description { get { return CatalogText.Resolve(localizedDescription); } private set { localizedDescription = value; } }
        public string DescriptionForRules(int rules)
        {
            if (rules >= 2 && Id == "spellcraft") return CatalogText.Get("text.0044");
            if (rules >= 2 && Id == "cinders") return CatalogText.Get("text.0045");
            return Description;
        }

        internal CatalogResearchNode(string id, string branchId, int tier, int cost,
            int unlockWave, string prerequisiteId, string title, string description)
        {
            Id = id; BranchId = branchId; Tier = tier; Cost = cost;
            UnlockWave = unlockWave; PrerequisiteId = prerequisiteId;
            Title = title; Description = description;
        }
    }

    public static class CatalogResearch
    {
        public const int MaxNodes = 3;
        static readonly CatalogResearchNode[] nodes = {
            new CatalogResearchNode("bulwark", "defense", 1, 40, 6, null,
                "text.0046", "text.0047"),
            new CatalogResearchNode("shieldcraft", "defense", 2, 60, 11, "bulwark",
                "text.0048", "text.0049"),
            new CatalogResearchNode("marksmanship", "precision", 1, 40, 6, null,
                "text.0050", "text.0051"),
            new CatalogResearchNode("exploitation", "precision", 2, 60, 11, "marksmanship",
                "text.0052", "text.0053"),
            new CatalogResearchNode("spellcraft", "arcana", 1, 40, 6, null,
                "text.0054", "text.0055"),
            new CatalogResearchNode("cinders", "arcana", 2, 60, 11, "spellcraft",
                "text.0056", "text.0057")
        };
        static readonly IReadOnlyList<CatalogResearchNode> publicNodes = Array.AsReadOnly(nodes);
        public static IReadOnlyList<CatalogResearchNode> Nodes { get { return publicNodes; } }

        public static CatalogResearchNode Get(string id)
        {
            foreach (CatalogResearchNode node in nodes)
                if (string.Equals(node.Id, id, StringComparison.Ordinal)) return node;
            return null;
        }

        internal static int ValidateSelection(IEnumerable<string> ids, int wave)
        {
            if (ids == null) throw new ArgumentException("Missing research selection.");
            var selected = new HashSet<string>(StringComparer.Ordinal);
            int spent = 0;
            foreach (string id in ids)
            {
                CatalogResearchNode node = Get(id);
                if (node == null || !selected.Add(id) || selected.Count > MaxNodes || wave < node.UnlockWave)
                    throw new ArgumentException("Invalid research selection.");
                spent += node.Cost;
            }
            foreach (string id in selected)
            {
                CatalogResearchNode node = Get(id);
                if (node.PrerequisiteId != null && !selected.Contains(node.PrerequisiteId))
                    throw new ArgumentException("Research prerequisite missing.");
            }
            return spent;
        }
    }
}
