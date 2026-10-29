using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public sealed class ExpeditionDialogueLine
    {
        public bool Observer { get; private set; }
        public string Text { get; private set; }
        internal ExpeditionDialogueLine(bool observer, string text) { Observer = observer; Text = text; }
    }

    public static class ExpeditionDialogue
    {
        static readonly IReadOnlyList<ExpeditionDialogueLine>[] Scenes = {
            Scene("핵을 확보했다. 성채로 이동한다.",
                "잠깐… 새싹이 이 빛 주위에만 자라고 있어.",
                "잿갈기가 지키던 게 이것이었나. 기록해 둬."),
            Scene("골렘이 멈췄다. 안에 명령이 남아 있어.",
                "‘외부 생물의 동력원 접근 금지.’ 왕국이 새긴 명령이야.",
                "핵의 문양도 서로 다르군. 반입 장부부터 찾아보자."),
            Scene("왕궁으로 보낸 핵… 출발지가 전부 다른 지역이다.",
                "왕국의 불이 깨진 조각이라더니, 처음부터 다른 것이었나?",
                "장부를 챙겨. 북쪽 성당에 같은 문양이 있다."),
            Scene("이 제단은 왕국보다 오래됐다. 핵의 문양과 같아.",
                "‘우리의 물과 겨울을 잇는 근원.’ 이곳의 것이었다는 뜻일까?",
                "왕궁으로 보내는 건 보류한다. 정원에서 확인하자."),
            Scene("도면에 다 적혀 있어. 인간이 핵을 가져가 왕국의 동력으로 썼다.",
                "그래서 땅이 메마르고 생물들이 뒤틀린 거야. 수호자들은 되찾았을 뿐인데…",
                "이 핵은 여기 둔다. 왕궁의 추출 장치를 멈추러 간다."),
            Scene("왕관이 부서졌다. 용도 더는 공격하지 않는군.",
                "핵을 돌려주면… 왕국에 남은 사람들은 어떻게 해?",
                "남은 동력으로 버티며 방법을 찾는다. 다른 땅을 계속 빼앗을 수는 없어.",
                "우리가 지키겠다고 맹세한 생명은 왕국 안에만 있지 않았으니까.")
        };

        static IReadOnlyList<ExpeditionDialogueLine> Scene(params string[] text)
        {
            var lines = new ExpeditionDialogueLine[text.Length];
            for (int i = 0; i < text.Length; i++) lines[i] = new ExpeditionDialogueLine(i == 1, text[i]);
            return Array.AsReadOnly(lines);
        }

        public static IReadOnlyList<ExpeditionDialogueLine> ForBoss(int wave)
        { return wave >= 5 && wave <= 30 && wave % 5 == 0 ? Scenes[wave / 5 - 1] : null; }

        // Speakers are surviving deployed allies, never fixed portraits or fallen companions.
        public static CatalogBattleUnit Speaker(IEnumerable<CatalogBattleUnit> units, bool observer)
        {
            CatalogBattleUnit leader = null;
            foreach (var unit in units)
                if (unit.Team == CatalogTeam.Ally && unit.IsAlive &&
                    (leader == null || unit.FormId.IndexOf("-KN", StringComparison.Ordinal) >= 0))
                { leader = unit; if (unit.FormId.IndexOf("-KN", StringComparison.Ordinal) >= 0) break; }
            if (!observer || leader == null) return leader;
            CatalogBattleUnit companion = null;
            foreach (var unit in units)
                if (unit.Team == CatalogTeam.Ally && unit.IsAlive && unit != leader)
                {
                    if (companion == null) companion = unit;
                    if (unit.FormId.IndexOf("-PR", StringComparison.Ordinal) >= 0) return unit;
                }
            return companion ?? leader;
        }
    }
}
