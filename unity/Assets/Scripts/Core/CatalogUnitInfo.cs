using System;

namespace Expedition.Core
{
    // Player-facing explanations of the executable rules in CatalogSkills/CatalogBattle.
    // Percentages describe coefficients, not promised damage after enemy mitigation.
    public static class CatalogUnitInfo
    {
        public static string Role(string id)
        {
            switch (id)
            {
                case "B-KN": return "전열 · 첫 피격 버티기";
                case "B-PR": return "중후열 · 단일 보호막";
                case "E-KN-G": return "전열 · 자신과 동료 보호막";
                case "E-KN-C": return "전열 · 보호막 반격";
                case "E-AR-P": return "후열 · 직선 관통";
                case "E-AR-T": return "후열 · 집중 사격";
                case "E-FI-B": return "후열 · 순간 범위 공격";
                case "E-FI-E": return "후열 · 지속 범위 공격";
                case "E-PR-G": return "중후열 · 2명 보호막";
                case "E-PR-H": return "중후열 · 단일 회복";
                case "E-IC-F": return "후열 · 범위 이동 둔화";
                case "E-IC-S": return "후열 · 빠른 단일 견제";
                case "E-BA-G": return "중열 · 받는 피해 감소";
                case "E-BA-A": return "중열 · 공격 속도 지원";
                case "H-01": return "전열 · 화염 참격 / 자기 보호";
                case "H-02": return "전열 · 근접 회복";
                case "H-03": return "후열 · 관통 / 이동 둔화";
                case "H-04": return "후열 · 화상 / 이동 둔화";
                case "H-05": return "중열 · 회복 / 공격 지원";
                case "H-06": return "후열 · 다음 공격 강화";
            }
            switch (CatalogRoster.FamilyCode(id))
            {
                case "KN": return "전열 · 방어 / 보호막";
                case "AR": return "후열 · 원거리 공격";
                case "FI": return "후열 · 화염 범위 공격";
                case "PR": return "중후열 · 회복 / 보호막";
                case "IC": return "후열 · 이동 둔화";
                case "BA": return "중열 · 아군 강화";
                default: throw new ArgumentException("Unknown ally form: " + id);
            }
        }

        public static string Placement(string id)
        {
            if (id == "H-02") return "앞줄 동료 곁 · 먼 후열은 회복 불가";
            if (id == "H-05") return "부상자가 거리 4.5 안에 들어오는 중열";
            if (id == "H-06") return "강화할 동료 2명과 거리 3 안에 배치";
            if (id == "E-KN-C") return "앞줄 · 자신에게 보호막을 줄 동료와 함께";
            if (id == "H-01" || CatalogRoster.FamilyCode(id) == "KN") return "적을 받아낼 앞줄에 배치";
            if (CatalogRoster.FamilyCode(id) == "PR" || CatalogRoster.FamilyCode(id) == "BA")
                return "지원할 동료와 가깝게 · 전투 전 범위 확인";
            return "앞줄 동료 뒤에서 사거리 확보";
        }

        public static string Summary(string id)
        {
            switch (id)
            {
                case "H-01": return "화염 참격과 자신의 보호막";
                case "H-02": return "근접 타격 후 주변 부상자 회복";
                case "H-03": return "최대 3명 관통 · 이동 둔화";
                case "H-04": return "화상과 둔화를 함께 부여하는 범위 공격";
                case "H-05": return "광역 회복 · 위급한 동료 추가 회복 · 공격 지원";
                case "H-06": return "지휘 사격 · 표식 · 다음 기본 공격 강화";
                default: return Role(id);
            }
        }

        public static string Strength(string id)
        {
            switch (id)
            {
                case "H-01": return "앞줄에서 화염 참격으로 여러 적을 치고 자신을 보호합니다.";
                case "H-02": return "앞줄에서 적을 때리며 자신과 가까운 부상자를 회복합니다.";
                case "H-03": return "일렬로 선 적을 관통하고 이동을 늦춥니다. 둔화된 적에게 기본 공격으로 표식도 남깁니다.";
                case "H-04": return "한 번의 범위 기술로 화상과 둔화를 걸어 자신의 추가 기술 피해 조건을 만듭니다.";
                case "H-05": return "주변 부상자를 회복하면서 공격력을 높이고, 위급한 동료 한 명을 추가로 회복합니다.";
                case "H-06": return "표식을 남기고 자신과 가까운 동료의 다음 기본 공격을 강화합니다. 기본 공격 중심 편성에 어울립니다.";
                case "B-PR": return "부상자에게 보호막을 줘 다음 피해를 받아냅니다.";
                case "E-KN-G": return "자신과 가까운 동료 한 명에게 보호막을 주며 앞줄을 지킵니다.";
                case "E-KN-C": return "자신의 보호막이 피해로 깨지면 주변 적에게 반격합니다.";
                case "E-AR-P": return "일렬로 선 적을 최대 세 명까지 관통합니다.";
                case "E-AR-T": return "체력이 낮은 적을 세 발로 집중 사격하고 표식을 남깁니다.";
                case "E-PR-G": return "가까운 아군 두 명에게 보호막을 공급합니다.";
                case "E-PR-H": return "가까운 아군 중 가장 위급한 한 명의 체력을 회복합니다.";
                case "F-KN": return "선택한 기사 분기 기술을 유지하며, 자신과 가까운 부상자 두 명에게 최대 체력에 비례한 보호막을 줍니다.";
                case "F-AR": return "선택한 궁수 분기 기술을 유지하며, 사거리 안의 가장 먼 적에게 강한 사격과 표식을 추가합니다.";
            }
            switch (CatalogRoster.FamilyCode(id))
            {
                case "KN": return "앞줄에서 적을 받아내고 뒤의 공격수를 지킵니다. 진화하면 보호막이나 반격으로 역할이 나뉩니다.";
                case "AR": return "긴 사거리에서 공격합니다. 진화하면 관통과 집중 사격 중 하나를 선택합니다.";
                case "FI": return "모인 적에게 범위 피해와 화상을 줍니다.";
                case "PR": return "보호막이나 회복으로 동료가 더 오래 싸우게 합니다.";
                case "IC": return "적의 이동을 늦춰 접근을 지연시키고, 둔화 조건을 쓰는 동료와 연계합니다.";
                case "BA": return "주변 동료의 공격이나 생존을 강화합니다. 강한 동료를 범위 안에 둘수록 유리합니다.";
                default: throw new ArgumentException("Unknown ally form: " + id);
            }
        }

        public static string Caution(string id)
        {
            switch (id)
            {
                case "H-01": return "보호막은 자신만 받습니다. 기사 분기의 동료 보호와 화염술사의 원거리 공격은 유지되지 않습니다.";
                case "H-02": return "적을 공격해야 회복합니다. 회복 거리가 짧고, 사제 분기의 보호막이나 먼 아군 지원은 유지되지 않습니다.";
                case "H-03": return "적이 흩어지면 관통 이득이 줄어듭니다. 두 분기의 기술이 관통 둔화 기술 하나로 바뀝니다.";
                case "H-04": return "두 명의 기본 공격과 서로 다른 시전 주기를 한 명으로 줄입니다. 단일 적에게는 범위 공격의 이득이 작습니다.";
                case "H-05": return "기수 분기의 피해 감소·공격 속도, 사제 분기의 보호막을 그대로 유지하지 않습니다. 공격할 동료가 필요합니다.";
                case "H-06": return "강화는 기술 피해를 높이지 않습니다. 다음 기본 공격 한 번에만 적용되며, 동료가 멀거나 5초 안에 공격하지 못하면 낭비됩니다. 기수의 지속 지원은 유지되지 않습니다.";
                case "B-PR": return "기본 사제는 체력을 회복하지 않습니다. 회복은 치유 분기로 진화해야 사용할 수 있습니다.";
                case "E-KN-C": return "자신의 반격 기술은 보호막을 만들지 않습니다. 보호막을 공급할 동료가 필요합니다.";
                case "E-PR-G": return "보호막은 잃은 체력을 회복하지 않습니다.";
                case "E-PR-H": return "한 번에 한 명만 회복하므로 여러 동료가 동시에 큰 피해를 받으면 대응이 늦습니다.";
            }
            switch (CatalogRoster.FamilyCode(id))
            {
                case "KN": return "혼자 적을 모두 잡는 역할은 아닙니다. 공격수와 회복·지원 동료를 함께 배치하세요.";
                case "AR": return "적에게 가까이 붙으면 위험합니다. 전열과 지원 범위를 함께 확보하세요.";
                case "FI": return "적이 흩어지면 범위 기술의 효율이 줄어듭니다.";
                case "PR": return "피해를 줄 공격수가 필요합니다. 전체 기술을 제외하면 지원 거리를 벗어난 동료에게 닿지 않습니다.";
                case "IC": return "둔화는 이동만 늦춥니다. 공격을 멈추는 빙결이 아니며 보스에게는 절반만 적용됩니다.";
                case "BA": return "강화는 기술마다 대상 수와 거리가 제한됩니다. 범위 밖 동료에게는 효과가 닿지 않습니다.";
                default: throw new ArgumentException("Unknown ally form: " + id);
            }
        }

        public static string Decision(string id)
        {
            string text = "강점\n" + Strength(id) + "\n\n주의점\n" + Caution(id);
            if (id.StartsWith("H-", StringComparison.Ordinal))
                text += "\n\n결합 선택\n두 분기 동료의 기존 기술·고유 특성이 결합체의 것으로 교체됩니다. 남는 동료의 전승만 유지합니다. 두 명이 출전 중이었다면 한 자리가 비므로 다른 동료를 배치할 수 있습니다. 최종 진화체와 결합체는 다시 결합하지 않습니다.";
            return text;
        }

        public static string Skill(string id, int balanceRules = 1)
        {
            string text;
            switch (id)
            {
                case "B-KN": text = "단일 적에게 위력 180% 피해 · 이동 속도 20% 감소, 2초."; break;
                case "B-AR": text = "체력이 낮은 적에게 위력 120% 피해 · 표식 5초."; break;
                case "B-FI": text = "반경 1 안의 적 최대 3명에게 위력 160% 피해와 화상."; break;
                case "B-PR": text = "거리 4.5 안의 아군 1명에게 위력 180% 보호막, 4초. 자신 포함 · 보호막이 부족한 부상자 우선."; break;
                case "B-IC": text = "반경 1.2 안의 적 최대 3명에게 위력 " + (CatalogBalance.FrostPower(id, balanceRules) * 100).ToString("0") + "% 피해 · 이동 속도 25% 감소, 3초."; break;
                case "B-BA": text = "자신과 거리 2.5 안의 가까운 아군 2명 공격력 +20%, 4초."; break;
                case "E-KN-G": text = "자신과 거리 2 안의 가장 가까운 아군 1명에게 위력 250% 보호막, 4초."; break;
                case "E-KN-C": return "보호막이 피해로 깨질 때 반격 · 재사용 2.5초\n전방 거리 1.8 안의 적 최대 3명에게 위력 220% 피해 · 이동 속도 20% 감소, 2초. 보호막 공급이 필요하며 빙결 중에는 반격할 수 없습니다.";
                case "E-AR-P": text = "사거리 안의 직선 경로에 있는 적 최대 3명에게 위력 210% 관통 피해."; break;
                case "E-AR-T": text = "체력이 낮은 적에게 표식 5초 · 위력 90% 화살 3발을 0.15초 간격으로 발사."; break;
                case "E-FI-B": text = "반경 1.4 안의 적 최대 4명에게 위력 280% 피해와 화상."; break;
                case "E-FI-E": text = "적 위치에 화염 지대 생성. 반경 1.6 안의 최대 4명에게 1초마다 위력 65% 피해와 화상, 총 4회."; break;
                case "E-PR-G": text = "거리 4.8 안의 아군 최대 2명에게 위력 230% 보호막, 4초. 자신 포함 · 보호막이 부족한 부상자 우선."; break;
                case "E-PR-H": text = "거리 4.8 안에서 체력 비율이 가장 낮은 아군 1명 회복, 위력 260%. 자신 포함 · 부상자가 없으면 대기."; break;
                case "E-IC-F": text = "반경 1.6 안의 적 최대 4명에게 위력 " + (CatalogBalance.FrostPower(id, balanceRules) * 100).ToString("0") + "% 피해 · 이동 속도 40% 감소, 4초."; break;
                case "E-IC-S": text = "단일 적에게 위력 " + (CatalogBalance.FrostPower(id, balanceRules) * 100).ToString("0") + "% 피해 · 이동 속도 30% 감소, 2초."; break;
                case "E-BA-G": text = "자신과 거리 2.8 안의 가까운 아군 2명 받는 피해 20% 감소, 4초."; break;
                case "E-BA-A": text = "자신과 거리 2.8 안의 가까운 아군 2명 공격 속도 +20%, 4초."; break;
                case "F-KN": text = "자신과 거리 2.5 안의 부상자 2명에게 자신의 최대 체력 18% 보호막, 5초."; break;
                case "F-AR": text = "사거리 안의 가장 먼 적에게 위력 420% 피해 · 표식 5초."; break;
                case "F-FI": text = "적이 밀집한 지점의 반경 1.8 안 최대 5명에게 위력 330% 피해와 화상."; break;
                case "F-PR": text = "살아 있는 아군 전체를 위력 160%만큼 회복하고 같은 양의 보호막 부여, 5초."; break;
                case "F-IC": text = "반경 2 안의 적 최대 5명에게 위력 " + (CatalogBalance.FrostPower(id, balanceRules) * 100).ToString("0") + "% 피해 · 이동 속도 40% 감소, 4초."; break;
                case "F-BA": text = "살아 있는 아군 전체 공격력 +20%, 받는 피해 15% 감소, 5초."; break;
                case "H-01": text = "전방 거리 2 안의 적 최대 3명에게 위력 240% 피해와 화상. 자신에게 최대 체력 15% 보호막, 4초."; break;
                case "H-02": text = "단일 적에게 위력 160% 피해. 거리 2.5 안의 부상자 최대 2명을 위력 180% 회복, 자신 포함."; break;
                case "H-03": text = "사거리 안 직선 경로의 적 최대 3명에게 위력 200% 피해 · 이동 속도 30% 감소, 3초."; break;
                case "H-04": text = "반경 1.5 안의 적 최대 4명에게 화상, 이동 속도 30% 감소(3초), 위력 200% 피해 순으로 적용."; break;
                case "H-05": text = "자신과 거리 4.5 안의 부상자 최대 2명 위력 220% 회복 · 공격력 +15%, 4초. 회복 전 체력 50% 미만인 동료 중 가장 위급한 1명은 위력 100% 추가 회복(자신 제외)."; break;
                case "H-06": text = "적에게 표식(5초)을 부여하고 위력 160% 사격. 자신과 거리 3 안의 가까운 아군 2명의 다음 기본 공격 1회 +60%, 5초 안에 사용."; break;
                default: throw new ArgumentException("Unknown ally skill: " + id);
            }
            return "재사용 " + CatalogBattle.SkillCooldown(id).ToString("0") + "초\n" + text;
        }

        public static string Trait(string id)
        {
            switch (id)
            {
                case null: case "": return "없음";
                case "T-KN": return "첫 피격부터 받는 피해 20% 감소, 3초.";
                case "T-AR": return "표식이 있는 적에게 기본 공격 피해 +15%.";
                case "T-FI": return "기본 공격으로 화상 부여.";
                case "T-PR": return "전투 시작 시 자신의 최대 체력 10% 보호막, 4초.";
                case "T-IC": return "기본 공격으로 이동 속도 20% 감소, 2초.";
                case "T-BA": return "체력 50% 이하가 되면 최대 체력 12% 보호막, 3초. 전투당 1회.";
                case "T-H01": return "화상 상태인 적에게 기본 공격 피해 +20%.";
                case "T-H02": return "자신이 받는 회복량 +15%.";
                case "T-H03": return "이미 둔화된 적을 기본 공격하면 표식 3초.";
                case "T-H04": return "화상과 둔화가 함께 걸린 적에게 직접 기술 피해 +15%.";
                case "T-H05": return "다른 아군에게 기술로 지원 효과를 주면 자신에게 위력 80% 보호막, 2초. 재사용 6초.";
                case "T-H06": return "적 처치 시 거리 3 안의 부상자 1명을 위력 100% 회복, 자신 포함. 재사용 4초.";
                default: throw new ArgumentException("Unknown trait: " + id);
            }
        }
        public const string Keywords = "위력 = 현재 공격력 기준 계수. 화상 = 1초마다 위력 15% 피해, 3회. 둔화는 이동만 늦추며 보스에게는 절반 적용. 표식 자체에는 피해가 없습니다. 기술은 대상·사거리 조건을 만족해야 사용합니다.";
    }
}
