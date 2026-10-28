namespace Expedition.Core
{
    // Saved per expedition. Updates must not change an expedition already in progress.
    public static class CatalogBalance
    {
        public const int CurrentRules = 4;
        public const int CurrentSaveVersion = 12;
        public static string Version(int rules)
        { return rules == 0 ? "legacy-v044" : rules == 1 ? "normal-045" : rules == 2 ? "normal-046" : rules == 3 ? "normal-048" : "normal-050"; }
        public static float Spellcraft(int rules) { return rules >= 2 ? 1.30f : 1.15f; }
        public static float Cinders(int rules) { return rules >= 2 ? 1.50f : 1.30f; }
        public static float FrostPower(string skill, int rules)
        {
            switch (skill)
            {
                case "B-IC": return rules >= 2 ? 1.6f : 1.3f;
                case "E-IC-F": return rules >= 2 ? 2.2f : 1.7f;
                case "E-IC-S": return rules >= 2 ? 2.1f : 1.9f;
                case "F-IC": return rules >= 2 ? 3.1f : 2.6f;
                default: throw new System.ArgumentException("Unknown frost skill.", "skill");
            }
        }
    }
}
