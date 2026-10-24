using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public sealed class StoryEntry
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }

        internal StoryEntry(string id, string title, string body)
        { Id = id; Title = title; Body = body; }
    }

    public static class ExpeditionStory
    {
        public static readonly IReadOnlyList<StoryEntry> Entries = Array.AsReadOnly(new[]
        {
            new StoryEntry("intro", "흩어진 이그니스",
                "왕국의 결계와 수호자를 지탱하던 마법의 불꽃, 이그니스가 흩어졌다. 결계는 무너지고 뒤틀린 수호자들은 사람을 공격한다.\n\n당신은 마지막 거점의 원정대장이다. 남은 불씨를 등불에 담고, 우두머리들이 품은 조각을 되찾아 왕국의 불을 되살린다."),
            new StoryEntry("forest", "뒤틀린 숲길",
                "이그니스의 파편이 잿갈기 안에서 요동쳐 숲의 짐승들이 거대해졌다.\n등불을 든 원정대는 결계가 사라진 숲길을 지나 조각을 되찾으려 한다."),
            new StoryEntry("forest-clear", "숲의 첫 조각",
                "잿갈기가 쓰러지자 거대해진 숲 짐승이 잠잠해진다.\n원정대는 첫 파편을 등불에 담고 성채로 향한다."),
            new StoryEntry("castle", "뒤틀린 성문",
                "성문 골렘은 이그니스가 깨진 뒤 남은 경비 명령에 사로잡혔다.\n돌아온 사람도 적으로 보는 수호자를 넘어 성채 안의 불꽃 조각을 찾는다."),
            new StoryEntry("castle-clear", "멈춘 성문 골렘",
                "뒤틀린 골렘이 멈추자 성문의 결계가 잠시 되살아난다.\n등불에 두 번째 조각을 담고 검은 연기 나는 폐광으로 간다."),
            new StoryEntry("mine", "멈추지 않는 용광로",
                "폐광의 용광로 수호자는 흩어진 이그니스의 파편에 매여 끝없이 쇠를 녹인다.\n원정대는 타오르는 광차길을 지나 수호자가 품은 불꽃을 되찾으려 한다."),
            new StoryEntry("mine-clear", "다음 불꽃의 단서",
                "용광로 수호자가 멈추고 세 번째 불꽃 조각을 되찾는다.\n조각에 스민 서리빛이 북쪽 성당을 가리킨다."),
            new StoryEntry("frost", "얼어붙은 성당",
                "서리 성당의 수호대는 식어버린 예식의 명령을 반복한다.\n원정대는 얼어붙은 회랑에서 네 번째 이그니스 조각을 찾는다."),
            new StoryEntry("frost-clear", "다시 울리는 종",
                "서리의 파수꾼이 쓰러지자 성당의 종이 다시 울린다.\n네 번째 조각을 등불에 담고 다음 길을 밝힌다.")
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
                default: return null;
            }
        }
    }

    [Serializable]
    public sealed class ExpeditionJournalSave
    {
        public int Version;
        public string[] UnlockedIds;
    }

    public sealed class ExpeditionJournal
    {
        readonly HashSet<string> unlocked = new HashSet<string>(StringComparer.Ordinal);

        ExpeditionJournal() { unlocked.Add("intro"); }

        public static ExpeditionJournal New() { return new ExpeditionJournal(); }

        public static ExpeditionJournal Restore(ExpeditionJournalSave save)
        {
            var journal = New();
            if (save == null || save.Version != 1 || save.UnlockedIds == null) return journal;
            foreach (string id in save.UnlockedIds)
                if (ExpeditionStory.Get(id) != null) journal.unlocked.Add(id);
            return journal;
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
            return new ExpeditionJournalSave { Version = 1, UnlockedIds = ids.ToArray() };
        }
    }
}
