using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public sealed class StoryEntry
    {
        public string Id { get; private set; }
        string localizedTitle;
        public string Title { get { return CatalogText.Resolve(localizedTitle); } private set { localizedTitle = value; } }
        string localizedBody;
        public string Body { get { return CatalogText.Resolve(localizedBody); } private set { localizedBody = value; } }
        string localizedExcerpt;
        public string Excerpt { get { return CatalogText.Resolve(localizedExcerpt); } private set { localizedExcerpt = value; } }

        internal StoryEntry(string id, string title, string excerpt, string body)
        { Id = id; Title = title; Excerpt = excerpt; Body = body; }
    }

    public static class ExpeditionStory
    {
        public static string HomeBrief { get { return CatalogText.Get("text.0281"); } }
        public static string ReplayBrief { get { return CatalogText.Get("text.0282"); } }
        public static string CompletionBrief { get { return CatalogText.Get("text.0283"); } }

        public static readonly IReadOnlyList<StoryEntry> Entries = Array.AsReadOnly(new[]
        {
            new StoryEntry("intro", "text.0284",
                "text.0285",
                "text.0286"),
            new StoryEntry("forest", "text.0287",
                "text.0288",
                "text.0289"),
            new StoryEntry("forest-clear", "text.0290",
                "text.0291",
                "text.0292"),
            new StoryEntry("castle", "text.0293",
                "text.0294",
                "text.0295"),
            new StoryEntry("castle-clear", "text.0296",
                "text.0297",
                "text.0298"),
            new StoryEntry("mine", "text.0299",
                "text.0300",
                "text.0301"),
            new StoryEntry("mine-clear", "text.0302",
                "text.0303",
                "text.0304"),
            new StoryEntry("frost", "text.0305",
                "text.0306",
                "text.0307"),
            new StoryEntry("frost-clear", "text.0308",
                "text.0309",
                "text.0310"),
            new StoryEntry("garden", "text.0311",
                "text.0312",
                "text.0313"),
            new StoryEntry("garden-clear", "text.0314",
                "text.0315",
                "text.0316"),
            new StoryEntry("palace", "text.0317",
                "text.0318",
                "text.0319"),
            new StoryEntry("palace-clear", "text.0320",
                "text.0321",
                "text.0322")
        });

        static readonly Dictionary<string, StoryEntry> ById = BuildIndex();

        static Dictionary<string, StoryEntry> BuildIndex()
        {
            var result = new Dictionary<string, StoryEntry>(StringComparer.Ordinal);
            foreach (StoryEntry entry in Entries) result.Add(entry.Id, entry);
            return result;
        }

        public static StoryEntry Get(string id)
        {
            StoryEntry entry;
            return id != null && ById.TryGetValue(id, out entry) ? entry : null;
        }

        public static StoryEntry ForWave(int wave)
        {
            if (wave >= 1 && wave <= 5) return Get("forest");
            if (wave >= 6 && wave <= 10) return Get("castle");
            if (wave >= 11 && wave <= 15) return Get("mine");
            if (wave >= 16 && wave <= 20) return Get("frost");
            if (wave >= 21 && wave <= 25) return Get("garden");
            if (wave >= 26 && wave <= 30) return Get("palace");
            return null;
        }

        public static StoryEntry BossClear(int wave)
        {
            switch (wave)
            {
                case 5: return Get("forest-clear");
                case 10: return Get("castle-clear");
                case 15: return Get("mine-clear");
                case 20: return Get("frost-clear");
                case 25: return Get("garden-clear");
                case 30: return Get("palace-clear");
                default: return null;
            }
        }
    }

    [Serializable]
    public sealed class ExpeditionJournalSave
    {
        public int Version;
        public string[] UnlockedIds;
        public string[] EndingParty;
    }

    public sealed class ExpeditionJournal
    {
        readonly HashSet<string> unlocked = new HashSet<string>(StringComparer.Ordinal);
        string[] endingParty = new string[0];
        public string[] EndingParty { get { return (string[])endingParty.Clone(); } }

        ExpeditionJournal() { unlocked.Add("intro"); }

        public static ExpeditionJournal New() { return new ExpeditionJournal(); }

        public static ExpeditionJournal Restore(ExpeditionJournalSave save)
        {
            var journal = New();
            if (save == null || save.Version != 1 || save.UnlockedIds == null) return journal;
            foreach (string id in save.UnlockedIds)
                if (ExpeditionStory.Get(id) != null) journal.unlocked.Add(id);
            if (journal.IsUnlocked("palace-clear")) journal.RecordEndingParty(save.EndingParty);
            return journal;
        }

        public bool RecordEndingParty(IEnumerable<string> forms)
        {
            if (!IsUnlocked("palace-clear") || forms == null) return false;
            var copy = new List<string>();
            foreach (string form in forms)
                if (!string.IsNullOrEmpty(form) && form.Length <= 16 && copy.Count < 5) copy.Add(form);
            bool changed = copy.Count != endingParty.Length;
            for (int i = 0; !changed && i < copy.Count; i++) changed |= copy[i] != endingParty[i];
            endingParty = copy.ToArray(); return changed;
        }

        public bool Observe(ExpeditionRun run)
        {
            if (run == null) throw new ArgumentNullException("run");
            bool changed = false;
            changed |= unlocked.Add("forest");
            if (run.Victories >= 5) changed |= unlocked.Add("forest-clear");
            if (run.Wave >= 6) changed |= unlocked.Add("castle");
            if (run.Victories >= 10) changed |= unlocked.Add("castle-clear");
            if (run.Wave >= 11) changed |= unlocked.Add("mine");
            if (run.Victories >= 15) changed |= unlocked.Add("mine-clear");
            if (run.Wave >= 16) changed |= unlocked.Add("frost");
            if (run.Victories >= 20) changed |= unlocked.Add("frost-clear");
            if (run.Wave >= 21) changed |= unlocked.Add("garden");
            if (run.Victories >= 25) changed |= unlocked.Add("garden-clear");
            if (run.Wave >= 26) changed |= unlocked.Add("palace");
            if (run.Victories >= 30) changed |= unlocked.Add("palace-clear");
            return changed;
        }

        public bool IsUnlocked(string id)
        { return id != null && unlocked.Contains(id); }

        public int UnlockedCount { get { return unlocked.Count; } }

        public ExpeditionJournalSave Capture()
        {
            var ids = new List<string>(unlocked.Count);
            foreach (StoryEntry entry in ExpeditionStory.Entries)
                if (unlocked.Contains(entry.Id)) ids.Add(entry.Id);
            return new ExpeditionJournalSave { Version = 1, UnlockedIds = ids.ToArray(), EndingParty = EndingParty };
        }
    }
}
