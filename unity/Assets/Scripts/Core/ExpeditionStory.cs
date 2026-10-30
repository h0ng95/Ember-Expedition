using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public sealed class StoryEntry
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public string Excerpt { get; private set; }

        internal StoryEntry(string id, string title, string excerpt, string body)
        { Id = id; Title = title; Excerpt = excerpt; Body = body; }
    }

    public static class ExpeditionStory
    {
        public const string HomeBrief = "이그니스는 왕국의 결계와 동력 시설을 움직이는 마법의 핵입니다.\n왕실은 수호자들에게 빼앗겼다며 원정대에 회수를 명령했습니다.";
        public const string ReplayBrief = "이그니스의 진실을 발견한 원정의 기록을 다시 펼칩니다.\n새 도전에서는 동료·골드·연구가 초기화되고, 발견한 이야기는 남습니다.";
        public const string CompletionBrief = "강제 추출을 멈췄습니다. 이그니스를 고향으로 돌려보낼 시간입니다.";

        public static readonly IReadOnlyList<StoryEntry> Entries = Array.AsReadOnly(new[]
        {
            new StoryEntry("intro", "재 속에서 한 맹세",
                "왕실은 빼앗긴 이그니스의 회수를 명령했다.\n원정대는 왕국의 불과 사람들을 지키겠다고 맹세한다.",
                "왕국의 결계와 난방, 용광로를 움직이는 마법의 핵, 이그니스. 그 빛이 하나씩 꺼지고 있다. 왕실은 수호자들이 핵을 빼앗아 갔다고 발표했다.\n\n마지막 거점의 원정대장인 당신은 동료를 모아 회수 명령을 받는다. 불이 꺼진 성벽 아래에는 추위와 습격을 견디는 사람들이 남아 있다.\n\n출발하던 날, 재가 내려앉은 광장에서 맹세했다.\n“왕국의 불을 되찾아, 그 아래 살아가는 이들을 지킨다.”"),
            new StoryEntry("forest", "메마른 숲길",
                "오래 마른 뿌리 사이로 뒤틀린 짐승들이 달려든다.\n잿갈기가 지킨다는 이그니스의 빛을 따라간다.",
                "숲의 나무들은 속까지 말라 있다. 짐승의 변이도 어젯밤 시작된 일처럼 보이지 않는다.\n\n왕실 지도는 잿갈기를 이그니스의 약탈자로 표시한다. 그러나 놈의 발자국은 마을이 아닌, 숲 안쪽의 빛을 둘러싸고 있다."),
            new StoryEntry("forest-clear", "핵 곁의 새싹",
                "잿갈기 뒤에서 작은 생물이 나와 그 곁에 웅크렸다.\n핵 주변의 새싹을 본 원정대는 더 다가가지 않는다.",
                "잿갈기는 쓰러진 뒤에도 작은 생물 앞에 몸을 낮춘 채 버텼다. 뒤에 숨었던 녀석이 조심스레 나와 그 곁에 웅크렸다. 원정대는 무기를 거두고 걸음을 멈췄다.\n\n이그니스의 빛 아래에는 새싹이 자라고 있었다. 바깥에는 오래 말라 죽은 뿌리만 남아 있다. 잿갈기가 마지막까지 지킨 것이 힘인지, 이 작은 땅인지 알 수 없었다.\n\n왕국에 남은 사람들도 기다리고 있다. 원정대는 아직 회수 명령을 버리지 못한다. 첫 핵을 챙기되, 숲에서 본 광경을 기록하고 성채로 향한다."),
            new StoryEntry("castle", "성문에 남은 명령",
                "성문 골렘은 돌아온 사람에게도 무기를 겨눈다.\n숲에서 이어진 옛 운반로가 성채 안으로 들어간다.",
                "성문 골렘의 가슴에는 왕국의 오래된 문장이 새겨져 있다. 인간이 만든 수호 장치는 피난민의 외침에도 길을 열지 않는다.\n\n성채 아래로 무거운 것을 옮기던 운반로가 이어진다. 이그니스의 흔적과 함께, 숲의 흙이 굳은 궤짝이 남아 있다."),
            new StoryEntry("castle-clear", "누구를 막는 성문인가",
                "골렘에는 ‘외부 생물의 동력원 접근 금지’ 명령이 남았다.\n성문은 사람을 지켰을까, 안에 모인 핵을 지켰을까.",
                "멈춘 골렘에서 경비 명령을 읽었다. ‘외부 생물의 동력원 접근 금지.’ 왕국이 새긴 명령이었다.\n\n운반로 끝의 두 번째 핵은 첫 핵과 문양이 달랐다. 같은 불에서 깨진 조각이라기에는 낯설다. 반입 장부의 다음 행선지는 폐광이었다."),
            new StoryEntry("mine", "멈추지 않는 용광로",
                "채굴은 멎었지만 용광로 수호자는 아직 일하고 있다.\n광차길 아래 동력관은 왕궁 쪽으로 이어진다.",
                "사람이 떠난 용광로는 이그니스의 힘으로 계속 달아오른다. 수호 장치는 오래된 생산 명령을 반복하며 접근하는 생물을 공격한다.\n\n원정대는 핵의 흔적을 좇아 동력관을 살핀다. 굳게 닫힌 관리실에는 반입과 송출을 기록한 장부가 남아 있다."),
            new StoryEntry("mine-clear", "반출 장부",
                "장부에는 여러 지역의 핵을 왕궁으로 보낸 기록이 남았다.\n원정대는 세 번째 핵과 장부를 챙겨 북쪽 성당으로 향한다.",
                "용광로가 멈추자 관리실에 들어갈 수 있었다. 장부에는 숲, 북쪽 성당, 정원의 이름과 핵의 반출 날짜가 적혀 있다. 왕국의 번영이 시작된 시기였다.\n\n왕실은 흩어진 조각을 되찾으라 했다. 그런데 이 핵들은 왜 오래전부터 서로 다른 지역에서 들어오고 있었을까."),
            new StoryEntry("frost", "불보다 오래된 제단",
                "핵은 불꽃처럼 빛나지만 주변의 냉기와 물을 움직인다.\n파수꾼은 얼어붙은 제단 앞에서 원정대를 막아선다.",
                "서리 성당의 핵은 뜨겁지 않다. 빛이 닿는 물길에는 작은 흐름이 남아 있고, 멀어진 회랑은 얼음 속에 갇혔다.\n\n이그니스는 단순한 연료가 아닐지 모른다. 원정대는 서리의 파수꾼 너머, 핵과 같은 문양이 새겨진 제단을 바라본다."),
            new StoryEntry("frost-clear", "왕국보다 오래된 이름",
                "제단의 기록은 왕국보다 오래되었고, 핵의 문양과 같았다.\n파수꾼은 핵을 빼앗은 것이 아니라 되찾아 온 것일까.",
                "파수꾼의 폭주가 잦아들자 제단의 글이 드러났다. ‘우리의 물과 겨울을 잇는 근원.’ 기록은 왕국 건국보다 오래되었다.\n\n원정대는 네 번째 핵을 보호 용기에 넣는다. 회수 명령만 믿고 왕궁으로 보낼 수는 없다. 장부에 남은 마지막 지역, 정원에서 진실을 확인하기로 한다."),
            new StoryEntry("garden", "뿌리 아래의 동력관",
                "가시의 군주 아래로 왕실 문장이 찍힌 관이 뻗어 있다.\n원정대는 핵을 옮기기 전에 관의 끝을 확인하려 한다.",
                "정원의 가시는 왕궁으로 난 길을 막고 있다. 뿌리 사이에는 폐광에서 본 것과 같은 동력관이 박혀 있다.\n\n가시의 군주는 핵을 감싼 채 접근을 막는다. 원정대는 성당의 기록과 장부를 대조하며, 이 관이 무엇을 가져갔는지 확인하려 한다."),
            new StoryEntry("garden-clear", "되찾은 자들의 땅",
                "인간이 지역의 핵을 가져갔고, 땅은 황폐해져 생물이 뒤틀렸다.\n수호자들은 자기 이그니스를 되찾았다. 원정대는 회수를 멈춘다.",
                "가시의 군주가 물러난 자리에서 왕실의 옛 도면을 찾았다. 이그니스는 각 지역의 생명과 마법을 잇는 근원이었다. 과거 인간은 핵을 가져가 왕국의 동력으로 썼다. 힘을 잃은 땅은 메마르고 생물은 뒤틀렸다.\n\n수호자들은 최근 자기 핵을 되찾아 갔다. 왕국은 그것을 약탈이라 불렀다. 오래 망가진 땅은 핵이 돌아와도 바로 낫지 않았다.\n\n원정대는 다섯 번째 핵을 그 자리에 둔다. 도면에는 아직 멈추지 않은 왕궁의 강제 추출 장치가 남아 있다."),
            new StoryEntry("palace", "왕관에 묶인 수호자",
                "왕궁은 옛 이그니스의 터 위에 세워졌다.\n원정대는 용을 묶은 왕관과 강제 추출 장치를 멈추려 한다.",
                "왕관의 수호룡은 왕국이 세워지기 전부터 이곳의 핵을 지켰다. 인간은 그 위에 궁을 짓고 용과 핵을 동력망에 묶었다. 왕관은 충성의 표식이 아닌 구속 장치였다.\n\n추출은 아직 계속되고, 구속에 갇힌 용은 다가오는 모두를 공격한다. 원정대는 핵을 빼앗으러 온 것이 아니다. 용의 공격을 견디며 장치를 끊으려 한다.\n\n왕국에 남은 사람들도 구해야 한다. 하지만 다른 땅의 생명을 계속 태우는 방식으로는 돌아갈 수 없다."),
            new StoryEntry("palace-clear", "서약이 지키는 것",
                "왕관이 부서지고 용은 마지막 핵을 다시 품는다.\n원정대는 회수한 핵을 고향으로 돌려보내기 시작한다.",
                "왕관이 부서지자 용의 날개에서 힘이 풀렸다. 강제 추출이 멈추고, 마지막 핵은 오래전의 수호자 곁에 남는다.\n\n원정대는 회수한 핵을 각 지역으로 돌려보낸다. 숲에는 작은 싹이 나고 얼어붙은 물길이 조금씩 흐른다. 회복에는 시간이 필요하다.\n\n왕국의 모든 시설을 예전처럼 켤 수는 없다. 남은 동력으로 사람들을 지키며 새로운 삶을 만들어야 한다.\n\n재 속에서 했던 맹세를 떠올린다.\n“우리가 지켜야 할 생명은 왕국 안에만 있지 않았다.”")
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
            return new ExpeditionJournalSave { Version = 1, UnlockedIds = ids.ToArray() };
        }
    }
}
