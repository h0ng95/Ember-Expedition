using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public sealed class CatalogLegacyResearchNode
    {
        public string Id { get; private set; }
        public string BranchId { get; private set; }
        public int Tier { get; private set; }
        public int Cost { get; private set; }
        public string PrerequisiteId { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }

        internal CatalogLegacyResearchNode(string id, string branch, int tier, int cost,
            string prerequisite, string title, string description)
        {
            Id = id; BranchId = branch; Tier = tier; Cost = cost;
            PrerequisiteId = prerequisite; Title = title; Description = description;
        }
    }

    // Account unlocks are copied into a run at creation, so returning to the hub cannot
    // change the rules of an expedition already in progress.
    public static class CatalogLegacyResearch
    {
        static readonly CatalogLegacyResearchNode[] nodes = {
            new CatalogLegacyResearchNode("tactical-start", "tactical", 1, 3, null,
                "서약 편성", "시작 편성 해금: 기사 · 기수 · 사제"),
            new CatalogLegacyResearchNode("tactical-vigor", "tactical", 2, 6, "tactical-start",
                "원정대 체력", "새 원정에서 모든 동료 최대 체력 +5%"),
            new CatalogLegacyResearchNode("ember-start", "ember", 1, 3, null,
                "화염 편성", "시작 편성 해금: 기사 · 화염술사 · 사제"),
            new CatalogLegacyResearchNode("ember-insight", "ember", 2, 6, "ember-start",
                "마력 이해", "새 원정에서 동료 직접 공격 기술 피해 +5%"),
            new CatalogLegacyResearchNode("frost-start", "frost", 1, 3, null,
                "서리 편성", "시작 편성 해금: 기사 · 서리술사 · 사제"),
            new CatalogLegacyResearchNode("frost-focus", "frost", 2, 6, "frost-start",
                "정밀 훈련", "새 원정에서 모든 동료 기본 공격 피해 +5%")
        };
        static readonly IReadOnlyList<CatalogLegacyResearchNode> publicNodes = Array.AsReadOnly(nodes);
        public static IReadOnlyList<CatalogLegacyResearchNode> Nodes { get { return publicNodes; } }

        public static CatalogLegacyResearchNode Get(string id)
        {
            foreach (var node in nodes)
                if (string.Equals(node.Id, id, StringComparison.Ordinal)) return node;
            return null;
        }

        public static void ValidateSelection(IEnumerable<string> ids)
        { SelectionCost(ids); }

        public static int SelectionCost(IEnumerable<string> ids)
        {
            if (ids == null) throw new ArgumentException("Missing legacy research selection.");
            var selected = new HashSet<string>(StringComparer.Ordinal);
            int cost = 0;
            foreach (string id in ids)
            {
                var node = Get(id);
                if (node == null || !selected.Add(id))
                    throw new ArgumentException("Invalid legacy research node.");
                cost += node.Cost;
            }
            foreach (string id in selected)
            {
                string parent = Get(id).PrerequisiteId;
                if (parent != null && !selected.Contains(parent))
                    throw new ArgumentException("Legacy research prerequisite missing.");
            }
            return cost;
        }

        public static bool IsFormationAvailable(string id, IEnumerable<string> ids)
        {
            if (id == "standard") return true;
            if (id != "tactical" && id != "ember" && id != "frost") return false;
            if (ids != null)
                foreach (string node in ids) if (node == id + "-start") return true;
            return false;
        }

        public static string[] GetFormationUnits(string id)
        {
            switch (id)
            {
                case "standard": return new[] { "B-KN", "B-AR", "B-PR" };
                case "tactical": return new[] { "B-KN", "B-BA", "B-PR" };
                case "ember": return new[] { "B-KN", "B-FI", "B-PR" };
                case "frost": return new[] { "B-KN", "B-IC", "B-PR" };
                default: throw new ArgumentException("Unknown starting formation.");
            }
        }
    }
}
