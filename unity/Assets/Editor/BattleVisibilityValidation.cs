using System;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class BattleVisibilityValidation
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Battle visibility: " + message);
        }

        [MenuItem("Expedition/Validate Battle Visibility")]
        public static void Validate()
        {
            Check(BattleVisibility.Compare(false, -2, CatalogTeam.Ally, "fallen",
                true, 2, CatalogTeam.Enemy, "live") < 0, "corpses paint first");
            Check(BattleVisibility.Compare(true, 2, CatalogTeam.Ally, "back",
                true, -2, CatalogTeam.Enemy, "front") < 0, "feet paint back to front");
            Check(BattleVisibility.Compare(true, 0, CatalogTeam.Enemy, "enemy",
                true, 0, CatalogTeam.Ally, "ally") < 0, "enemy paints before ally at equal feet");
            Check(BattleVisibility.Compare(true, 0, CatalogTeam.Ally, "a",
                true, 0, CatalogTeam.Ally, "b") < 0, "ID resolves final tie");

            Check(BattleVisibility.Compare(true, -2, CatalogTeam.Enemy, "boss",
                true, 2, CatalogTeam.Ally, "ally", true, false) < 0,
                "ally behind boss feet still paints in front");
            Check(BattleVisibility.Compare(true, -2, CatalogTeam.Enemy, "boss",
                true, 2, CatalogTeam.Enemy, "escort", true, false) < 0,
                "escort behind boss feet still paints in front");
            Check(BattleVisibility.Compare(true, 2, CatalogTeam.Ally, "ally",
                true, -2, CatalogTeam.Enemy, "boss", false, true) > 0,
                "boss ordering is symmetric");
            Check(BattleVisibility.Compare(false, -2, CatalogTeam.Ally, "fallen",
                true, 2, CatalogTeam.Enemy, "boss", false, true) < 0,
                "dead ally still recedes behind living boss");
            Check(BattleVisibility.Compare(false, -2, CatalogTeam.Enemy, "boss",
                true, 2, CatalogTeam.Enemy, "escort", true, false) < 0,
                "fallen boss cannot cover living escort");
            Check(BattleVisibility.Compare(true, 2, CatalogTeam.Enemy, "boss-a",
                true, -2, CatalogTeam.Enemy, "boss-b", true, true) < 0,
                "two bosses retain foot ordering");

            var art = new ForestEnemyArt();
            try
            {
                Rect boss;
                Check(art.TryGetBounds("forest-boss", 0, Vector2.zero, out boss),
                    "boss geometry is available from the manifest");
                Check(boss.height > 144f && boss.width > 120f,
                    "boss remains larger than ally bounds");
            }
            finally { art.Release(); }
            Debug.Log("Battle visibility validated: 12 assertions, boss behind allies and escorts.");
        }
    }
}
