using UnityEngine;
using Expedition.Core;

namespace Expedition.Presentation
{
    // Presentation only. The legacy Core ability IDs retain their existing damage and timing.
    public static class DrakeAttackPresentation
    {
        public const string PillarsPath = "Art/Effects/v057/dragon-pillars";
        public const string FlightPath = "Art/Enemies/v057/dragon-air-breath";
        public const float PillarDuration = .8f;
        public const float AirLeadIn = .16f;
        public const int PillarCount = 8;
        public const float FlightScale = 277f / 414f; // Crown-to-toes, excluding wings.

        public static int AirFrame(float time) { return 24 + (Mathf.FloorToInt(time * 4f) & 1); }
        public static int BasicFrame { get { return 13; } } // Open jaw without baked-in fire.
        public static bool AirIgniting(CatalogBattleUnit unit)
        {
            return unit.IsAlive && unit.DrakePhase == CrownDrakePhase.AirWarning &&
                unit.SlamWarningRemaining > 0f && unit.SlamWarningRemaining <= AirLeadIn;
        }
        public static float PillarProgress(float age, int index)
        {
            // All bases ignite on the damage event. Only their upward crest staggers.
            return Mathf.Clamp01((age - index * .028f) / .60f);
        }
        public static Vector2 PillarPoint(Vector2 start, Vector2 end, int index)
        { return Vector2.Lerp(start, end, index / (float)(PillarCount - 1)); }

        public static Rect FlightSource(int frame)
        {
            return frame == 24 ? new Rect(21, 71, 860, 694) : new Rect(897, 294, 859, 475);
        }
        public static Rect FlightBounds(int frame, Vector2 foot)
        {
            Rect source = FlightSource(frame);
            Vector2 anchor = frame == 24 ? new Vector2(450, 765) : new Vector2(1337, 769);
            return new Rect(foot.x + (source.x - anchor.x) * FlightScale,
                foot.y + (source.y - anchor.y) * FlightScale,
                source.width * FlightScale, source.height * FlightScale);
        }
        public static Vector2 FlightMouth(int frame, Rect bounds)
        {
            Rect source = FlightSource(frame);
            Vector2 mouth = frame == 24 ? new Vector2(143, 578) : new Vector2(979, 600);
            return bounds.position + Vector2.Scale(mouth - source.position,
                new Vector2(bounds.width / source.width, bounds.height / source.height));
        }
        public static float FlightLift(CatalogBattleUnit unit)
        {
            if (!unit.IsAlive || unit.FlightLift <= 0f) return 0f;
            if (unit.DrakePhase != CrownDrakePhase.LandingWarning) return unit.FlightLift;
            // Use the actual scheduled landing, even if enrage begins during descent.
            float landingDuration = unit.SlamWarningRemaining / unit.FlightLift;
            // Hold the full body height while emitting, then descend to the unchanged landing hit.
            return Mathf.Clamp01(unit.SlamWarningRemaining /
                (landingDuration - PalaceEnemyArt.BreathVisualDuration));
        }
    }
}
