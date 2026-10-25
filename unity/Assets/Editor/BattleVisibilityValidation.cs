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
            Check(BattleVisibility.Compare(true, 2, CatalogTeam.Enemy, "back",
                true, -2, CatalogTeam.Enemy, "front") < 0, "enemy feet paint back to front");
            Check(BattleVisibility.Compare(true, -2, CatalogTeam.Enemy, "enemy",
                true, 2, CatalogTeam.Ally, "ally") < 0, "living allies stay in front");
            Check(BattleVisibility.Compare(true, 0, CatalogTeam.Enemy, "enemy",
                true, 0, CatalogTeam.Ally, "ally") < 0, "enemy paints before ally at equal feet");
            Check(BattleVisibility.Compare(true, 0, CatalogTeam.Ally, "a",
                true, 0, CatalogTeam.Ally, "b") < 0, "ID resolves final tie");

            Check(BattleVisibility.Compare(true, -2, CatalogTeam.Enemy, "boss",
                true, 2, CatalogTeam.Ally, "ally") < 0,
                "ally behind boss feet still paints in front");
            Check(BattleVisibility.Compare(true, 2, CatalogTeam.Enemy, "escort",
                true, -2, CatalogTeam.Enemy, "boss") < 0,
                "boss partly covers an escort behind its feet");
            Check(BattleVisibility.Compare(true, 2, CatalogTeam.Enemy, "boss",
                true, -2, CatalogTeam.Enemy, "escort") < 0,
                "escort in front paints after boss");
            Check(BattleVisibility.Compare(true, 2, CatalogTeam.Ally, "ally",
                true, -2, CatalogTeam.Enemy, "boss") > 0,
                "boss ordering is symmetric");
            Check(BattleVisibility.Compare(false, -2, CatalogTeam.Ally, "fallen",
                true, 2, CatalogTeam.Enemy, "boss") < 0,
                "dead ally still recedes behind living boss");
            Check(BattleVisibility.Compare(false, -2, CatalogTeam.Enemy, "boss",
                true, 2, CatalogTeam.Enemy, "escort") < 0,
                "fallen boss cannot cover living escort");
            Check(BattleVisibility.Compare(true, 2, CatalogTeam.Enemy, "boss-a",
                true, -2, CatalogTeam.Enemy, "boss-b") < 0,
                "two bosses retain foot ordering");
            Check(BattleVisibility.NeedsEscortReveal(new Rect(0, 0, 100, 100),
                new Rect(-10, -10, 120, 120)), "fully covered escort gets ground cue");
            Check(!BattleVisibility.NeedsEscortReveal(new Rect(0, 0, 100, 100),
                new Rect(-20, 0, 100, 100)), "partly visible escort needs no cue");
            Check(!BattleVisibility.NeedsEscortReveal(new Rect(0, 0, 100, 100),
                new Rect(110, 0, 100, 100)), "separated silhouettes need no cue");
            Check(!BattleVisibility.NeedsEscortReveal(new Rect(0, 0, 0, 100),
                new Rect(0, 0, 100, 100)), "invalid bounds need no cue");

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
            Debug.Log("Battle visibility validated: 18 assertions, foot-sorted enemies and covered-escort cue.");
        }
    }
}
