using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public sealed class CatalogMember
    {
        public string Id { get; internal set; }
        public string FormId { get; internal set; }
        public string BranchId { get; internal set; }
        public string InheritedTraitId { get; internal set; }
        public int Growth { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }

        internal CatalogMember Copy()
        {
            return (CatalogMember)MemberwiseClone();
        }
    }

    public sealed class CatalogRoster
    {
        readonly CatalogDatabase catalog;
        readonly List<CatalogMember> members = new List<CatalogMember>();
        readonly Stack<List<CatalogMember>> history = new Stack<List<CatalogMember>>();
        readonly Dictionary<string, string> hybridRecipe = new Dictionary<string, string>
        {
            { "H-01", "FI+KN" }, { "H-02", "KN+PR" }, { "H-03", "AR+IC" },
            { "H-04", "FI+IC" }, { "H-05", "BA+PR" }, { "H-06", "AR+BA" }
        };
        int supply;
        public IReadOnlyList<CatalogMember> Members { get { return members.AsReadOnly(); } }
        public int SupplyUsed { get { return supply; } }
        public bool CanUndo { get { return history.Count > 0; } }

        public CatalogRoster(CatalogDatabase catalog)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            this.catalog = catalog;
        }

        public bool Recruit(string id, string baseFormId, float x, float y)
        {
            if (supply >= 14 || string.IsNullOrEmpty(id) || Find(id) != null) return false;
            CatalogForm form = catalog.Get(baseFormId);
            if (!baseFormId.StartsWith("B-", StringComparison.Ordinal)) return false;
            var recruited = new CatalogMember { Id = id, FormId = form.Id, X = x, Y = y };
            members.Add(recruited);
            // A summon is not part of preparation Undo. Keep it in every older snapshot.
            foreach (List<CatalogMember> snapshot in history) snapshot.Add(recruited.Copy());
            supply++;
            return true;
        }

        public CatalogMember Find(string id)
        {
            return members.Find(member => member.Id == id);
        }

        public bool Transfer(string donorId, string recipientId, bool replaceTrait)
        {
            CatalogMember donor = Find(donorId), recipient = Find(recipientId);
            if (donor == null || recipient == null || donor == recipient) return false;
            string incoming = catalog.Get(donor.FormId).InnateTrait;
            bool growth = recipient.FormId.StartsWith("B-", StringComparison.Ordinal) ? recipient.Growth < 2 :
                recipient.FormId.StartsWith("E-", StringComparison.Ordinal) && recipient.Growth < 6;
            bool traitChange = replaceTrait && recipient.InheritedTraitId != incoming &&
                catalog.Get(recipient.FormId).InnateTrait != incoming;
            if (!growth && !traitChange) return false;
            Save();
            if (growth) recipient.Growth++;
            if (traitChange) recipient.InheritedTraitId = incoming;
            members.Remove(donor);
            return true;
        }

        public bool Evolve(string memberId, string nextFormId)
        {
            CatalogMember member = Find(memberId);
            if (member == null) return false;
            string family = FamilyCode(member.FormId);
            bool branch = member.FormId.StartsWith("B-", StringComparison.Ordinal) && member.Growth >= 2 &&
                nextFormId.StartsWith("E-" + family + "-", StringComparison.Ordinal);
            bool final = member.FormId.StartsWith("E-", StringComparison.Ordinal) && member.Growth >= 6 &&
                nextFormId == "F-" + family;
            if (!branch && !final) return false;
            catalog.Get(nextFormId);
            Save();
            if (branch) member.BranchId = nextFormId;
            member.FormId = nextFormId;
            return true;
        }

        public bool Combine(string anchorId, string otherId, string resultId)
        {
            CatalogMember anchor = Find(anchorId), other = Find(otherId);
            string recipe;
            if (anchor == null || other == null || anchor == other ||
                !hybridRecipe.TryGetValue(resultId, out recipe) ||
                !anchor.FormId.StartsWith("E-", StringComparison.Ordinal) ||
                !other.FormId.StartsWith("E-", StringComparison.Ordinal)) return false;
            string[] families = { FamilyCode(anchor.FormId), FamilyCode(other.FormId) };
            Array.Sort(families, StringComparer.Ordinal);
            if (families[0] + "+" + families[1] != recipe) return false;
            catalog.Get(resultId);
            Save();
            anchor.FormId = resultId;
            anchor.BranchId = null;
            anchor.Growth = 0;
            members.Remove(other);
            return true;
        }

        public bool Undo()
        {
            if (history.Count == 0) return false;
            members.Clear();
            members.AddRange(history.Pop());
            return true;
        }

        public void CommitPreparation() { history.Clear(); }

        public static bool FitsSupply(int finals, int branches, int hybrids, int basics,
            int available = 14, int maximumDeployed = 5)
        {
            if (finals < 0 || branches < 0 || hybrids < 0 || basics < 0 || available < 0 || maximumDeployed < 0)
                return false;
            long count = (long)finals + branches + hybrids + basics;
            long cost = 7L * finals + 3L * branches + 6L * hybrids + basics;
            return count <= maximumDeployed && cost <= available;
        }

        void Save()
        {
            var snapshot = new List<CatalogMember>(members.Count);
            foreach (CatalogMember member in members) snapshot.Add(member.Copy());
            history.Push(snapshot);
        }

        public static string FamilyCode(string formId)
        {
            if (string.IsNullOrEmpty(formId) || formId.Length < 4 || formId.StartsWith("H-", StringComparison.Ordinal))
                return null;
            return formId.Substring(2, 2);
        }
    }
}
