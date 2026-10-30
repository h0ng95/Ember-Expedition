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
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(true, "잠깐. 뒤에 작은 녀석이 있어."),
                new ExpeditionDialogueLine(false, "…무기 내려. 더 다가가지 마."),
                new ExpeditionDialogueLine(true, "새싹도 이 빛 주위에만 자라고 있어."),
                new ExpeditionDialogueLine(false, "이곳을 지키던 건가… 기록해 둬.")
            }),
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(false, "왕국의 문장이다. 그런데 우리에게도 문을 닫았군."),
                new ExpeditionDialogueLine(true, "‘외부 생물의 동력원 접근 금지.’ 사람도 예외가 아니었어."),
                new ExpeditionDialogueLine(false, "문 밖의 사람들보다 핵을 먼저 지키라는 명령인가."),
                new ExpeditionDialogueLine(true, "누가 내린 명령인지, 반입 장부를 찾아보자.")
            }),
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(true, "숲, 성당, 정원… 핵을 가져온 곳이 전부 달라."),
                new ExpeditionDialogueLine(false, "우리가 겨울을 넘긴 동력이 여기서 왔다는 건가."),
                new ExpeditionDialogueLine(true, "그 뒤부터 그 땅이 메말랐다면?"),
                new ExpeditionDialogueLine(false, "장부를 챙겨. 우리를 기다리는 사람들도 알아야 해.")
            }),
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(true, "제단이 왕국보다 오래됐어. 핵의 문양도 같고."),
                new ExpeditionDialogueLine(false, "왕국의 것이었다는 말부터 틀렸을 수 있겠군."),
                new ExpeditionDialogueLine(true, "이걸 가져가도 되는 걸까."),
                new ExpeditionDialogueLine(false, "왕궁으로 보내는 건 보류한다. 정원의 기록까지 확인하자.")
            }),
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(true, "우리가 핵을 가져가 땅이 메말랐어. 수호자들은 되찾았던 거야."),
                new ExpeditionDialogueLine(false, "그럼 지금까지 우리가 회수한 건…"),
                new ExpeditionDialogueLine(true, "핵을 돌려주면 왕국의 사람들은 어떻게 해?"),
                new ExpeditionDialogueLine(false, "그 책임도 우리가 진다. 이 핵은 두고, 왕궁의 추출 장치를 멈춘다.")
            }),
            Scene("구속이 풀렸다. 이제 우리를 공격하지 않는군.",
                "남은 동력만으로 왕국이 버틸 수 있을까.",
                "돌아가서 함께 버텨야지. 다른 땅을 계속 빼앗을 수는 없어.",
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
