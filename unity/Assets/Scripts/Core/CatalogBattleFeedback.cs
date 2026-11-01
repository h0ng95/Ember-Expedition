namespace Expedition.Core
{
    public static class CatalogBattleFeedback
    {
        public static string Summary(CatalogBattleReport report)
        {
            if (report == null || report.Outcome == CatalogBattleOutcome.Unknown || report.Outcome == CatalogBattleOutcome.Interrupted)
                return "이번 전투의 종료 원인을 확인할 기록이 없습니다.";
            string end = report.Outcome == CatalogBattleOutcome.Timeout ? "시간 초과" :
                report.Outcome == CatalogBattleOutcome.Defeat ? "아군 전멸" : "승리";
            return end + " · " + report.Duration.ToString("0.0") + "초 · 생존 " + report.AlliesAlive + "/" + report.Allies.Count + "명";
        }

        public static string Evidence(CatalogBattleReport report, CatalogDatabase database)
        {
            if (report == null || report.Outcome == CatalogBattleOutcome.Unknown || report.Outcome == CatalogBattleOutcome.Interrupted) return "";
            string remaining = "남은 적 " + report.EnemiesAlive + "명 · 체력 " + report.EnemyHpLeft.ToString("N0");
            if (report.FirstLostFormId == null) return remaining + " · 아군 이탈 없음";
            string name = database == null ? report.FirstLostFormId : database.Get(report.FirstLostFormId).Name;
            return remaining + "\n첫 이탈: " + name + " · " + report.FirstLossAt.ToString("0.0") + "초";
        }

        public static string Suggestion(CatalogBattleReport report)
        {
            if (report == null || report.Outcome == CatalogBattleOutcome.Unknown || report.Outcome == CatalogBattleOutcome.Interrupted) return "";
            if (report.Outcome == CatalogBattleOutcome.Victory)
                return "동료별 실제 피해·회복·보호를 다음 편성과 비교해 보세요.";
            if (report.FirstLossAt >= 0 && report.FirstLossAt < 25f)
                return report.FirstLostWasRanged ?
                    "원거리 동료가 초반에 쓰러졌어요. 회복·보호 범위와 전열 뒤의 시작 위치를 확인해 보세요." :
                    "초반에 동료를 잃었어요. 첫 이탈한 동료의 성장·진화와 회복·보호 배치를 확인해 보세요.";
            if (report.Outcome == CatalogBattleOutcome.Timeout)
                return "시간 안에 처치하지 못했어요. 피해 기여도를 보고 공격 동료의 성장·진화와 연구를 비교해 보세요.";
            return "회복·보호 기여도와 첫 이탈 시점을 확인해 보세요. 동료 간격을 바꾸어 다시 도전할 수 있어요.";
        }
    }
}
