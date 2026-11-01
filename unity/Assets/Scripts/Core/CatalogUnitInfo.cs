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
                case "B-KN": return CatalogText.Get("text.0058");
                case "B-PR": return CatalogText.Get("text.0059");
                case "E-KN-G": return CatalogText.Get("text.0060");
                case "E-KN-C": return CatalogText.Get("text.0061");
                case "E-AR-P": return CatalogText.Get("text.0062");
                case "E-AR-T": return CatalogText.Get("text.0063");
                case "E-FI-B": return CatalogText.Get("text.0064");
                case "E-FI-E": return CatalogText.Get("text.0065");
                case "E-PR-G": return CatalogText.Get("text.0066");
                case "E-PR-H": return CatalogText.Get("text.0067");
                case "E-IC-F": return CatalogText.Get("text.0068");
                case "E-IC-S": return CatalogText.Get("text.0069");
                case "E-BA-G": return CatalogText.Get("text.0070");
                case "E-BA-A": return CatalogText.Get("text.0071");
                case "H-01": return CatalogText.Get("text.0072");
                case "H-02": return CatalogText.Get("text.0073");
                case "H-03": return CatalogText.Get("text.0074");
                case "H-04": return CatalogText.Get("text.0075");
                case "H-05": return CatalogText.Get("text.0076");
                case "H-06": return CatalogText.Get("text.0077");
            }
            switch (CatalogRoster.FamilyCode(id))
            {
                case "KN": return CatalogText.Get("text.0078");
                case "AR": return CatalogText.Get("text.0079");
                case "FI": return CatalogText.Get("text.0080");
                case "PR": return CatalogText.Get("text.0081");
                case "IC": return CatalogText.Get("text.0082");
                case "BA": return CatalogText.Get("text.0083");
                default: throw new ArgumentException("Unknown ally form: " + id);
            }
        }

        public static string Placement(string id)
        {
            if (id == "H-02") return CatalogText.Get("text.0084");
            if (id == "H-05") return CatalogText.Get("text.0085");
            if (id == "H-06") return CatalogText.Get("text.0086");
            if (id == "E-KN-C") return CatalogText.Get("text.0087");
            if (id == "H-01" || CatalogRoster.FamilyCode(id) == "KN") return CatalogText.Get("text.0088");
            if (CatalogRoster.FamilyCode(id) == "PR" || CatalogRoster.FamilyCode(id) == "BA")
                return CatalogText.Get("text.0089");
            return CatalogText.Get("text.0090");
        }

        public static string Summary(string id)
        {
            switch (id)
            {
                case "H-01": return CatalogText.Get("text.0091");
                case "H-02": return CatalogText.Get("text.0092");
                case "H-03": return CatalogText.Get("text.0093");
                case "H-04": return CatalogText.Get("text.0094");
                case "H-05": return CatalogText.Get("text.0095");
                case "H-06": return CatalogText.Get("text.0096");
                default: return Role(id);
            }
        }

        public static string Strength(string id)
        {
            switch (id)
            {
                case "H-01": return CatalogText.Get("text.0097");
                case "H-02": return CatalogText.Get("text.0098");
                case "H-03": return CatalogText.Get("text.0099");
                case "H-04": return CatalogText.Get("text.0100");
                case "H-05": return CatalogText.Get("text.0101");
                case "H-06": return CatalogText.Get("text.0102");
                case "B-PR": return CatalogText.Get("text.0103");
                case "E-KN-G": return CatalogText.Get("text.0104");
                case "E-KN-C": return CatalogText.Get("text.0105");
                case "E-AR-P": return CatalogText.Get("text.0106");
                case "E-AR-T": return CatalogText.Get("text.0107");
                case "E-PR-G": return CatalogText.Get("text.0108");
                case "E-PR-H": return CatalogText.Get("text.0109");
                case "F-KN": return CatalogText.Get("text.0110");
                case "F-AR": return CatalogText.Get("text.0111");
            }
            switch (CatalogRoster.FamilyCode(id))
            {
                case "KN": return CatalogText.Get("text.0112");
                case "AR": return CatalogText.Get("text.0113");
                case "FI": return CatalogText.Get("text.0114");
                case "PR": return CatalogText.Get("text.0115");
                case "IC": return CatalogText.Get("text.0116");
                case "BA": return CatalogText.Get("text.0117");
                default: throw new ArgumentException("Unknown ally form: " + id);
            }
        }

        public static string Caution(string id)
        {
            switch (id)
            {
                case "H-01": return CatalogText.Get("text.0118");
                case "H-02": return CatalogText.Get("text.0119");
                case "H-03": return CatalogText.Get("text.0120");
                case "H-04": return CatalogText.Get("text.0121");
                case "H-05": return CatalogText.Get("text.0122");
                case "H-06": return CatalogText.Get("text.0123");
                case "B-PR": return CatalogText.Get("text.0124");
                case "E-KN-C": return CatalogText.Get("text.0125");
                case "E-PR-G": return CatalogText.Get("text.0126");
                case "E-PR-H": return CatalogText.Get("text.0127");
            }
            switch (CatalogRoster.FamilyCode(id))
            {
                case "KN": return CatalogText.Get("text.0128");
                case "AR": return CatalogText.Get("text.0129");
                case "FI": return CatalogText.Get("text.0130");
                case "PR": return CatalogText.Get("text.0131");
                case "IC": return CatalogText.Get("text.0132");
                case "BA": return CatalogText.Get("text.0133");
                default: throw new ArgumentException("Unknown ally form: " + id);
            }
        }

        public static string Decision(string id)
        {
            string text = CatalogText.Get("text.0134") + Strength(id) + CatalogText.Get("text.0135") + Caution(id);
            if (id.StartsWith("H-", StringComparison.Ordinal))
                text += CatalogText.Get("text.0136");
            return text;
        }

        public static string Skill(string id, int balanceRules = 1)
        {
            string text;
            switch (id)
            {
                case "B-KN": text = CatalogText.Get("text.0137"); break;
                case "B-AR": text = CatalogText.Get("text.0138"); break;
                case "B-FI": text = CatalogText.Get("text.0139"); break;
                case "B-PR": text = CatalogText.Get("text.0140"); break;
                case "B-IC": text = CatalogText.Get("text.0141") + (CatalogBalance.FrostPower(id, balanceRules) * 100).ToString("0") + CatalogText.Get("text.0142"); break;
                case "B-BA": text = CatalogText.Get("text.0143"); break;
                case "E-KN-G": text = CatalogText.Get("text.0144"); break;
                case "E-KN-C": return CatalogText.Get("text.0145");
                case "E-AR-P": text = CatalogText.Get("text.0146"); break;
                case "E-AR-T": text = CatalogText.Get("text.0147"); break;
                case "E-FI-B": text = CatalogText.Get("text.0148"); break;
                case "E-FI-E": text = CatalogText.Get("text.0149"); break;
                case "E-PR-G": text = CatalogText.Get("text.0150"); break;
                case "E-PR-H": text = CatalogText.Get("text.0151"); break;
                case "E-IC-F": text = CatalogText.Get("text.0152") + (CatalogBalance.FrostPower(id, balanceRules) * 100).ToString("0") + CatalogText.Get("text.0153"); break;
                case "E-IC-S": text = CatalogText.Get("text.0154") + (CatalogBalance.FrostPower(id, balanceRules) * 100).ToString("0") + CatalogText.Get("text.0155"); break;
                case "E-BA-G": text = CatalogText.Get("text.0156"); break;
                case "E-BA-A": text = CatalogText.Get("text.0157"); break;
                case "F-KN": text = CatalogText.Get("text.0158"); break;
                case "F-AR": text = CatalogText.Get("text.0159"); break;
                case "F-FI": text = CatalogText.Get("text.0160"); break;
                case "F-PR": text = CatalogText.Get("text.0161"); break;
                case "F-IC": text = CatalogText.Get("text.0162") + (CatalogBalance.FrostPower(id, balanceRules) * 100).ToString("0") + CatalogText.Get("text.0153"); break;
                case "F-BA": text = CatalogText.Get("text.0163"); break;
                case "H-01": text = CatalogText.Get("text.0164"); break;
                case "H-02": text = CatalogText.Get("text.0165"); break;
                case "H-03": text = CatalogText.Get("text.0166"); break;
                case "H-04": text = CatalogText.Get("text.0167"); break;
                case "H-05": text = CatalogText.Get("text.0168"); break;
                case "H-06": text = CatalogText.Get("text.0169"); break;
                default: throw new ArgumentException("Unknown ally skill: " + id);
            }
            return CatalogText.Get("text.0170") + CatalogBattle.SkillCooldown(id).ToString("0") + CatalogText.Get("text.0171") + text;
        }

        public static string Trait(string id)
        {
            switch (id)
            {
                case null: case "": return CatalogText.Get("text.0172");
                case "T-KN": return CatalogText.Get("text.0173");
                case "T-AR": return CatalogText.Get("text.0174");
                case "T-FI": return CatalogText.Get("text.0175");
                case "T-PR": return CatalogText.Get("text.0176");
                case "T-IC": return CatalogText.Get("text.0177");
                case "T-BA": return CatalogText.Get("text.0178");
                case "T-H01": return CatalogText.Get("text.0179");
                case "T-H02": return CatalogText.Get("text.0180");
                case "T-H03": return CatalogText.Get("text.0181");
                case "T-H04": return CatalogText.Get("text.0182");
                case "T-H05": return CatalogText.Get("text.0183");
                case "T-H06": return CatalogText.Get("text.0184");
                default: throw new ArgumentException("Unknown trait: " + id);
            }
        }
        public static string Keywords { get { return CatalogText.Get("text.0185"); } }
    }
}
