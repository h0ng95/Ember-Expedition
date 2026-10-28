using UnityEngine;

namespace Expedition.Presentation
{
    // Source-pixel measurements from the original transparent atlas (1774 x 887).
    // The generated frames do not share a nozzle pivot; using one makes fire swim
    // around the mouth. Keep the artwork intact and compensate in drawing space.
    public static class DragonBreathArt
    {
        public const string Path = "Art/Effects/v049/dragon-breath";
        public const float AfterglowDuration = .32f;
        static readonly Vector2[] Nozzles = {
            new Vector2(352.24f,234.26f), new Vector2(361.58f,232.42f),
            new Vector2(384.34f,232.66f), new Vector2(400.92f,231.92f),
            new Vector2(411.84f,209.02f), new Vector2(409.37f,210.54f),
            new Vector2(386.67f,210.98f), new Vector2(393.73f,209.62f)
        };
        static readonly float[] Fronts = {236f,47f,10f,11f,28f,39f,33f,32f};

        public static int EmissionFrame(float progress)
        {
            float t = Mathf.Clamp01(progress);
            // A brief ignition, then enough time on the large turbulent lobes.
            return t < .08f ? 0 : t < .19f ? 1 : t < .36f ? 2 :
                t < .57f ? 3 : t < .79f ? 4 : 5;
        }

        public static Vector2 Nozzle(int frame) { return Nozzles[Mathf.Clamp(frame,0,7)]; }
        public static float Front(int frame) { return Fronts[Mathf.Clamp(frame,0,7)]; }
        public static Vector2 CellSize(int frame)
        {
            int column = frame % 4;
            return new Vector2(Mathf.RoundToInt((column+1)*1774f/4f) -
                Mathf.RoundToInt(column*1774f/4f), frame < 4 ? 444f : 443f);
        }

        public static Rect LocalBounds(int frame, float length, float halfWidth)
        {
            frame = Mathf.Clamp(frame,0,7);
            Vector2 nozzle = Nozzles[frame], cell = CellSize(frame);
            // The first two frames grow into the warned endpoint. Full plumes end
            // there instead of expanding their damage-looking reach past it.
            float travel = frame < 2 ? 380f : nozzle.x - Fronts[frame];
            float scaleX = Mathf.Max(1f,length) / travel;
            float height = Mathf.Clamp(halfWidth * 48f * 9f,250f,320f);
            float scaleY = height / cell.y;
            return new Rect(-nozzle.x * scaleX,-nozzle.y * scaleY,
                cell.x * scaleX,height);
        }

        // Smoke is no longer attached to the mouth. Crop the obsolete bright
        // nozzle out of the last two frames; no new textures are allocated.
        public static float SmokeCrop(int frame)
        { return (Nozzle(frame).x - 62f) / CellSize(frame).x; }
    }
}
