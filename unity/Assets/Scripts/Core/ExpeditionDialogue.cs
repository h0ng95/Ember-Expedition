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
                new ExpeditionDialogueLine(true, "작은 녀석이 나왔어. 쓰러진 잿갈기 곁을 떠나지 않네."),
                new ExpeditionDialogueLine(false, "무기를 내려. 더 공격할 필요는 없어."),
                new ExpeditionDialogueLine(true, "봐. 이그니스 주변에만 새싹이 자라고 있어."),
                new ExpeditionDialogueLine(false, "이 빛과 새끼를 지키던 걸까. 숲에서 본 일은 기록해 두자.")
            }),
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(true, "골렘 안에서 명령판이 나왔어. 왕실 문장이 찍혀 있어."),
                new ExpeditionDialogueLine(true, "‘이그니스에 접근하는 자는 누구든 막아라.’ 우리도 공격한 이유가 이거였네."),
                new ExpeditionDialogueLine(false, "사람보다 이그니스를 먼저 지키라는 명령이군."),
                new ExpeditionDialogueLine(true, "뒷면에 폐광의 보관소 이름이 있어. 그곳의 장부를 찾아보자.")
            }),
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(true, "이 장부에 숲과 성당의 이그니스를 왕궁으로 옮긴 기록이 있어."),
                new ExpeditionDialogueLine(false, "왕국의 동력을 다른 땅에서 가져온 거였군."),
                new ExpeditionDialogueLine(true, "그 뒤에 물이 마르고 생물이 변했다는 보고도 붙어 있어. 관계가 있는 걸까?"),
                new ExpeditionDialogueLine(false, "장부를 챙겨. 서리 성당에 남은 기록과 비교해 보자.")
            }),
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(true, "드러난 제단을 봐. 이그니스와 같은 눈꽃 문양이 새겨져 있어."),
                new ExpeditionDialogueLine(true, "왕국이 세워지기 전부터 이 빛이 성당을 지켰다는 기록도 있어."),
                new ExpeditionDialogueLine(false, "처음부터 왕국의 물건은 아니었던 거군."),
                new ExpeditionDialogueLine(false, "이그니스는 여기 두자. 정원의 기록까지 확인하고 왕궁에 보고한다.")
            }),
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(true, "이 왕실 도면에 적혀 있어. 이그니스를 옮겨 정원의 힘을 왕궁으로 빼내던 장치야."),
                new ExpeditionDialogueLine(false, "옛 왕실이 먼저 빼앗았고, 수호자들은 자기 것을 되찾았던 거군."),
                new ExpeditionDialogueLine(true, "그럼 이그니스는 돌려줘야겠네. 하지만 왕국의 난방과 결계는 어떻게 해?"),
                new ExpeditionDialogueLine(false, "정원의 빛은 두고 간다. 왕궁의 추출을 멈춘 뒤, 남은 동력으로 사람들을 지킬 방법을 찾자.")
            }),
            Scene("왕관이 부서지자 용이 공격을 멈췄다. 저 장치에 묶여 있었던 거야.",
                "왕궁으로 흐르던 빛도 끊겼어. 이그니스가 용 곁에 남아 있어.",
                "우리가 가져온 이그니스도 고향으로 돌려보내자.",
                "돌아가면 남은 동력을 난방과 결계부터 쓰자. 사람들을 지킬 다른 방법도 찾아야 해.")
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
