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
        public string PrerequisiteId { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }

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
                "방벽 훈련", "근접 동료 최대 체력 +12%"),
            new CatalogResearchNode("shieldcraft", "defense", 2, 60, 11, "bulwark",
                "방패술", "보호막이 있는 동료 피해 +15%"),
            new CatalogResearchNode("marksmanship", "precision", 1, 40, 6, null,
                "정밀 사격", "원거리 동료 기본 공격 피해 +15%"),
            new CatalogResearchNode("exploitation", "precision", 2, 60, 11, "marksmanship",
                "약점 공략", "표식 또는 둔화 적 대상 피해 +18%"),
            new CatalogResearchNode("spellcraft", "arcana", 1, 40, 6, null,
                "술식 연구", "동료 직접 공격 기술 피해 +15%"),
            new CatalogResearchNode("cinders", "arcana", 2, 60, 11, "spellcraft",
                "잔불 증폭", "동료 화상 피해 +30%")
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
