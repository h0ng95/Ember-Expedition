namespace Expedition.Core
{
    public static class CatalogBattleFeedback
    {
        public static string Summary(CatalogBattleReport report)
        {
            if (report == null || report.Outcome == CatalogBattleOutcome.Unknown || report.Outcome == CatalogBattleOutcome.Interrupted)
                return CatalogText.Get("text.0001");
            string end = report.Outcome == CatalogBattleOutcome.Timeout ? CatalogText.Get("text.0002") :
                report.Outcome == CatalogBattleOutcome.Defeat ? CatalogText.Get("text.0003") : CatalogText.Get("text.0004");
            return end + " · " + report.Duration.ToString("0.0") + CatalogText.Get("text.0005") + report.AlliesAlive + "/" + report.Allies.Count + CatalogText.Get("text.0006");
        }

        public static string Evidence(CatalogBattleReport report, CatalogDatabase database)
        {
            if (report == null || report.Outcome == CatalogBattleOutcome.Unknown || report.Outcome == CatalogBattleOutcome.Interrupted) return "";
            string remaining = CatalogText.Get("text.0007") + report.EnemiesAlive + CatalogText.Get("text.0008") + report.EnemyHpLeft.ToString("N0");
            if (report.FirstLostFormId == null) return remaining + CatalogText.Get("text.0009");
            string name = database == null ? report.FirstLostFormId : database.Get(report.FirstLostFormId).Name;
            return remaining + CatalogText.Get("text.0010") + name + " · " + report.FirstLossAt.ToString("0.0") + CatalogText.Get("text.0011");
        }

        public static string Suggestion(CatalogBattleReport report)
        {
            if (report == null || report.Outcome == CatalogBattleOutcome.Unknown || report.Outcome == CatalogBattleOutcome.Interrupted) return "";
            if (report.Outcome == CatalogBattleOutcome.Victory)
                return CatalogText.Get("text.0012");
            if (report.FirstLossAt >= 0 && report.FirstLossAt < 25f)
                return report.FirstLostWasRanged ?
                    CatalogText.Get("text.0013") :
                    CatalogText.Get("text.0014");
            if (report.Outcome == CatalogBattleOutcome.Timeout)
                return CatalogText.Get("text.0015");
            return CatalogText.Get("text.0016");
        }
    }
}
