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
        public bool MaterialLocked { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }

        internal CatalogMember Copy()
        {
            return (CatalogMember)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class CatalogMemberSave
    {
        public string Id, FormId, BranchId, InheritedTraitId;
        public int Growth;
        public bool MaterialLocked;
        public float X, Y;
    }

    [Serializable]
    public sealed class CatalogRosterHistorySave
    {
        public CatalogMemberSave[] Members;
    }

    [Serializable]
    public sealed class CatalogRosterSave
    {
        public int SupplyUsed;
        public CatalogMemberSave[] Members;
        // Most recent preparation action first.
        public CatalogRosterHistorySave[] History;
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
            if (supply == int.MaxValue || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(baseFormId) || Find(id) != null ||
                float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y)) return false;
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

        public bool SetMaterialLocked(string id, bool locked)
        {
            CatalogMember member = Find(id);
            if (member == null || member.MaterialLocked == locked) return false;
            member.MaterialLocked = locked;
            foreach (var snapshot in history)
                foreach (var item in snapshot)
                    if (item.Id == id) item.MaterialLocked = locked;
            return true;
        }

        public bool CanTransfer(string donorId, string recipientId, bool replaceTrait = false)
        {
            CatalogMember donor = Find(donorId), recipient = Find(recipientId);
            if (donor == null || recipient == null || donor == recipient || donor.MaterialLocked) return false;
            string incoming = catalog.Get(donor.FormId).InnateTrait;
            bool growth = recipient.FormId.StartsWith("B-", StringComparison.Ordinal) ? recipient.Growth < 2 :
                recipient.FormId.StartsWith("E-", StringComparison.Ordinal) && recipient.Growth < 6;
            return growth || replaceTrait && recipient.InheritedTraitId != incoming &&
                catalog.Get(recipient.FormId).InnateTrait != incoming;
        }

        public bool Transfer(string donorId, string recipientId, bool replaceTrait)
        {
            if (!CanTransfer(donorId, recipientId, replaceTrait)) return false;
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

        public string HybridResult(string firstForm, string secondForm)
        {
            if (string.IsNullOrEmpty(firstForm) || string.IsNullOrEmpty(secondForm) ||
                !firstForm.StartsWith("E-", StringComparison.Ordinal) ||
                !secondForm.StartsWith("E-", StringComparison.Ordinal)) return null;
            string[] families = { FamilyCode(firstForm), FamilyCode(secondForm) };
            Array.Sort(families, StringComparer.Ordinal);
            string recipe = families[0] + "+" + families[1];
            foreach (var pair in hybridRecipe) if (pair.Value == recipe) return pair.Key;
            return null;
        }

        public bool CanCombine(string anchorId, string otherId, string resultId)
        {
            CatalogMember anchor = Find(anchorId), other = Find(otherId);
            return anchor != null && other != null && anchor != other && !other.MaterialLocked &&
                resultId != null && HybridResult(anchor.FormId, other.FormId) == resultId;
        }

        public bool Combine(string anchorId, string otherId, string resultId)
        {
            if (!CanCombine(anchorId, otherId, resultId)) return false;
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

        public CatalogRosterSave Capture()
        {
            var snapshots = new List<CatalogRosterHistorySave>();
            foreach (List<CatalogMember> snapshot in history)
                snapshots.Add(new CatalogRosterHistorySave { Members = CaptureMembers(snapshot) });
            return new CatalogRosterSave { SupplyUsed = supply, Members = CaptureMembers(members), History = snapshots.ToArray() };
        }

        public static CatalogRoster Restore(CatalogDatabase catalog, CatalogRosterSave save)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (save == null || save.SupplyUsed < 0 || save.Members == null || save.History == null)
                throw new ArgumentException("Invalid roster save.");
            var roster = new CatalogRoster(catalog);
            roster.supply = save.SupplyUsed;
            roster.members.AddRange(RestoreMembers(catalog, save.Members, save.SupplyUsed));
            for (int i = save.History.Length - 1; i >= 0; i--)
                roster.history.Push(RestoreMembers(catalog, save.History[i] == null ? null : save.History[i].Members, save.SupplyUsed));
            return roster;
        }

        static CatalogMemberSave[] CaptureMembers(List<CatalogMember> source)
        {
            var result = new CatalogMemberSave[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                CatalogMember member = source[i];
                result[i] = new CatalogMemberSave { Id = member.Id, FormId = member.FormId,
                    BranchId = member.BranchId, InheritedTraitId = member.InheritedTraitId,
                    Growth = member.Growth, MaterialLocked = member.MaterialLocked, X = member.X, Y = member.Y };
            }
            return result;
        }

        static List<CatalogMember> RestoreMembers(CatalogDatabase catalog, CatalogMemberSave[] source, int supply)
        {
            if (source == null || source.Length > supply) throw new ArgumentException("Invalid roster members.");
            var result = new List<CatalogMember>(source.Length);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CatalogMemberSave item in source)
            {
                if (item == null || string.IsNullOrEmpty(item.Id) || !ids.Add(item.Id) ||
                    item.Growth < 0 || item.Growth > 6 || float.IsNaN(item.X) || float.IsInfinity(item.X) ||
                    float.IsNaN(item.Y) || float.IsInfinity(item.Y)) throw new ArgumentException("Invalid roster member.");
                catalog.Get(item.FormId);
                string branchId = string.IsNullOrEmpty(item.BranchId) ? null : item.BranchId;
                string traitId = string.IsNullOrEmpty(item.InheritedTraitId) ? null : item.InheritedTraitId;
                if (branchId != null) catalog.Get(branchId);
                if (item.FormId.StartsWith("F-", StringComparison.Ordinal) &&
                    (branchId == null || !branchId.StartsWith("E-" + FamilyCode(item.FormId) + "-", StringComparison.Ordinal)))
                    throw new ArgumentException("Final form lacks a matching branch.");
                if (item.FormId.StartsWith("E-", StringComparison.Ordinal) && branchId != item.FormId ||
                    !item.FormId.StartsWith("E-", StringComparison.Ordinal) &&
                    !item.FormId.StartsWith("F-", StringComparison.Ordinal) && branchId != null)
                    throw new ArgumentException("Invalid branch for form.");
                result.Add(new CatalogMember { Id = item.Id, FormId = item.FormId,
                    BranchId = branchId, InheritedTraitId = traitId,
                    Growth = item.Growth, MaterialLocked = item.MaterialLocked, X = item.X, Y = item.Y });
            }
            return result;
        }

        public static bool FitsSupply(int finals, int branches, int hybrids, int basics,
            int available = int.MaxValue, int maximumDeployed = 5)
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
