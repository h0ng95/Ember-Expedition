using System;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Palace sprites use the regional 6 by 4 sheet and face left.
    public static class PalaceEnemyArt
    {
        public const string ManifestPath = "Art/Enemies/v031/palace-manifest";
        public const float BreathVisualDuration = .44f;
        public static readonly string[] Species = {
            "palace-guard", "palace-arbalist", "palace-sentinel", "palace-boss"
        };

        public static string SpeciesFor(CatalogBattleUnit unit)
        {
            if (unit == null || unit.Team != CatalogTeam.Enemy) return null;
            if (unit.Id == "palace-boss" && unit.IsBoss &&
                unit.EnemyRole == CatalogEnemyRole.CrownDrake) return Species[3];
            if (unit.Id == "palace-sentinel" && unit.EnemyRole == CatalogEnemyRole.Supervisor)
                return Species[2];
            if (unit.Id.StartsWith("palace-arbalist-", StringComparison.Ordinal) &&
                unit.EnemyRole == CatalogEnemyRole.Raider) return Species[1];
            if (unit.Id.StartsWith("palace-guard-", StringComparison.Ordinal) &&
                unit.EnemyRole == CatalogEnemyRole.MineWorker) return Species[0];
            return null;
        }

        public static float SilhouetteHeight(string id)
        {
            return id == Species[0] ? 108f : id == Species[1] ? 110f :
                id == Species[2] ? 150f : id == Species[3] ? 340f : 0f;
        }

        // The effect owns this clock, so the mouth cannot close while fire is visible.
        public static int BreathFrame(float progress)
        {
            return 14 + Mathf.Min(2, Mathf.FloorToInt(Mathf.Clamp01(progress) * 3f));
        }

        public static bool TryGetBreathMouth(int frame, Rect drawnBounds, out Vector2 mouth)
        {
            mouth = Vector2.zero;
            // Measured inside the open jaws in the original 1536x1024 sheet:
            // (550,646), (803,648), (1058,658), using a top-left source origin.
            // Normalize against each trimmed frame, then use the actual drawn bounds.
            Vector2 uv;
            if (frame == 24 || frame == 25)
            { mouth = DrakeAttackPresentation.FlightMouth(frame, drawnBounds); return true; }
            if (frame == 13) uv = new Vector2(12f / 224f, 111f / 170f);
            else if (frame == 14) uv = new Vector2(27f / 238f, 97f / 182f);
            else if (frame == 15) uv = new Vector2(29f / 241f, 98f / 181f);
            else if (frame == 16) uv = new Vector2(27f / 233f, 105f / 175f);
            else return false;
            mouth = new Vector2(drawnBounds.x + drawnBounds.width * uv.x,
                drawnBounds.y + drawnBounds.height * uv.y);
            return true;
        }

        public static float VisualFlightLift(float flightLift, float worldY, float animationTime = 0f)
        {
            if (flightLift <= 0f) return 0f;
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(flightLift));
            float groundY = Math.Max(377f, Math.Min(617f, 497f - worldY * 48f));
            // Keep the original sprite scale. Reserve the head/torso envelope,
            // allowing only the tall wing tips to pass behind the foreground HUD.
            float bodyHeight = SilhouetteHeight(Species[3]) * 150f / 184f;
            float available = Mathf.Max(0f, groundY - 76f - bodyHeight);
            // Two wing beats per second; the small bob shares the battle clock.
            return rise * (Mathf.Min(104f, Mathf.Max(0f, available - 6f)) +
                Mathf.Min(6f, available * .2f) * Mathf.Sin(animationTime * Mathf.PI * 4f));
        }

        public static int FrameIndex(string id, int state, float stateTime, float windupAge,
            float releaseRemaining, float releaseAge, float attackWindupRemaining,
            CrownDrakePhase phase, float flightLift, float breathWarningRemaining,
            string releaseAbilityId)
        {
            if (state == 4)
                return ForestEnemyArt.FrameIndex(4, stateTime, windupAge, releaseRemaining,
                    releaseAge, 0f, false, attackWindupRemaining, false);
            if (id == Species[2] && releaseRemaining > 0f && releaseAbilityId == "MINE_GUARD")
                return releaseAge < .09f ? 22 : 23;
            if (id == Species[3])
            {
                if (flightLift > 0f || phase == CrownDrakePhase.Rising ||
                    phase == CrownDrakePhase.AirWarning || phase == CrownDrakePhase.LandingWarning)
                    return 22 + (Math.Max(0, (int)(stateTime * 4f)) % 2);
                // Ground fissures use a stomp/brace pose, never the old baked-in mouth flames.
                if (breathWarningRemaining > 0f) return 12;
                if (releaseRemaining > 0f)
                    return releaseAbilityId == "BASIC" ? DrakeAttackPresentation.BasicFrame : 17;
            }
            return ForestEnemyArt.FrameIndex(id == Species[3] && state == 3 ? 0 : state,
                stateTime, windupAge, releaseRemaining, releaseAge, 0f, false,
                attackWindupRemaining, false);
        }
    }
}
