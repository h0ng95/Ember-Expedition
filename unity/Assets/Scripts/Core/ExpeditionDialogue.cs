using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public sealed class ExpeditionDialogueLine
    {
        public bool Observer { get; private set; }
        string localizedText;
        public string Text { get { return CatalogText.Resolve(localizedText); } private set { localizedText = value; } }
        internal ExpeditionDialogueLine(bool observer, string text) { Observer = observer; Text = text; }
    }

    public static class ExpeditionDialogue
    {
        static readonly IReadOnlyList<ExpeditionDialogueLine>[] Scenes = {
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(true, "text.0186"),
                new ExpeditionDialogueLine(false, "text.0187"),
                new ExpeditionDialogueLine(true, "text.0188"),
                new ExpeditionDialogueLine(false, "text.0189")
            }),
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(true, "text.0190"),
                new ExpeditionDialogueLine(true, "text.0191"),
                new ExpeditionDialogueLine(false, "text.0192"),
                new ExpeditionDialogueLine(true, "text.0193")
            }),
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(true, "text.0194"),
                new ExpeditionDialogueLine(false, "text.0195"),
                new ExpeditionDialogueLine(true, "text.0196"),
                new ExpeditionDialogueLine(false, "text.0197")
            }),
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(true, "text.0198"),
                new ExpeditionDialogueLine(true, "text.0199"),
                new ExpeditionDialogueLine(false, "text.0200"),
                new ExpeditionDialogueLine(false, "text.0201")
            }),
            Array.AsReadOnly(new[] {
                new ExpeditionDialogueLine(true, "text.0202"),
                new ExpeditionDialogueLine(false, "text.0203"),
                new ExpeditionDialogueLine(true, "text.0204"),
                new ExpeditionDialogueLine(false, "text.0205")
            }),
            Scene("text.0206",
                "text.0207",
                "text.0208",
                "text.0209")
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
