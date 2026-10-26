using UnityEngine;

namespace Expedition.Presentation
{
    // One cached 4-by-2 sheet per attack. UV selection does not create sub-textures.
    internal sealed class BossSpriteEffects
    {
        internal const string RootsPath = "Art/Effects/v039/garden-roots";
        internal const string BreathPath = "Art/Effects/v039/dragon-breath";
        internal const string FirefallPath = "Art/Effects/v039/dragon-firefall";
        internal const string GolemPath = "Art/Effects/v039/golem-shatter";
        internal const string FrostPath = "Art/Effects/v039/frost-eruption";
        internal const string ChargePath = "Art/Effects/v039/charge-dust";

        Texture2D roots, breath, firefall, golem, frost, charge;
        bool loaded;

        void Load()
        {
            if (loaded) return;
            loaded = true;
            roots = LoadSheet(RootsPath);
            breath = LoadSheet(BreathPath);
            firefall = LoadSheet(FirefallPath);
            golem = LoadSheet(GolemPath);
            frost = LoadSheet(FrostPath);
            charge = LoadSheet(ChargePath);
        }

        static Texture2D LoadSheet(string path)
        {
            Texture2D sheet = Resources.Load<Texture2D>(path);
            if (sheet == null || sheet.width < 4 || sheet.height < 2) return null;
            sheet.filterMode = FilterMode.Point;
            sheet.wrapMode = TextureWrapMode.Clamp;
            return sheet;
        }

        static bool Frame(Texture2D sheet, int index, Rect destination, float opacity,
            bool dustTopCuts = false)
        {
            if (sheet == null || destination.width <= 0f || destination.height <= 0f) return false;
            index = Mathf.Clamp(index, 0, 7);
            // Generated sheets can have odd dimensions. Round each cell edge to a
            // source pixel instead of sampling across a half-pixel seam.
            int column = index % 4, row = index / 4;
            // The dust atlas's upper four frames have unequal empty gutters.
            // Its lower row and all other atlases use the regular rounded grid.
            float leftRatio = dustTopCuts && row == 0 ? DustCut(column) : column * .25f;
            float rightRatio = dustTopCuts && row == 0 ? DustCut(column + 1) : (column + 1) * .25f;
            int left = Mathf.RoundToInt(leftRatio * sheet.width);
            int right = Mathf.RoundToInt(rightRatio * sheet.width);
            int top = Mathf.RoundToInt(row * sheet.height / 2f);
            int bottom = Mathf.RoundToInt((row + 1) * sheet.height / 2f);
            Rect uv = new Rect(left / (float)sheet.width, 1f - bottom / (float)sheet.height,
                (right - left) / (float)sheet.width, (bottom - top) / (float)sheet.height);
            Color previous = GUI.color;
            GUI.color = previous * new Color(1f, 1f, 1f, Mathf.Clamp01(opacity));
            if (GamePreferences.Current.ReduceFlashes) GUI.color *= new Color(.65f, .65f, .65f, 1f);
            GUI.DrawTextureWithTexCoords(destination, sheet, uv, true);
            GUI.color = previous;
            return true;
        }

        static float DustCut(int index)
        {
            return index == 0 ? 0f : index == 1 ? 444f / 1774f :
                index == 2 ? 820f / 1774f : index == 3 ? 1280f / 1774f : 1f;
        }

        static float CellAspect(Texture2D sheet)
        {
            return (sheet.height * .5f) / (sheet.width * .25f);
        }

        static bool GroundSheet(Texture2D sheet, Vector2 ground, float width,
            float topFootPivot, float bottomFootPivot, float progress, bool dust = false)
        {
            if (sheet == null) return false;
            float height = width * CellAspect(sheet);
            int frame = Mathf.Min(7, Mathf.FloorToInt(progress * 8f));
            float footPivot = frame < 4 ? topFootPivot : bottomFootPivot;
            return Frame(sheet, frame,
                new Rect(ground.x - width * .5f, ground.y - height * footPivot, width, height),
                1f - Mathf.Clamp01((progress - .82f) / .18f), dust);
        }

        internal bool DrawRoots(Vector2 ground, float radius, float progress)
        {
            Load();
            if (roots == null) return false;
            float width = radius * 83f * 1.4f;
            float height = width * CellAspect(roots);
            int frame = Mathf.Min(7, Mathf.FloorToInt(progress * 8f));
            float footPivot = frame < 4 ? .984f : .947f;
            return Frame(roots, frame,
                new Rect(ground.x - width * .5f, ground.y - height * footPivot, width, height),
                1f - Mathf.Clamp01((progress - .78f) / .22f));
        }

        internal bool DrawBreath(Vector2 mouth, Vector2 groundTarget,
            float halfWidth, float progress)
        {
            Load();
            if (breath == null) return false;
            Vector2 delta = mouth - groundTarget;
            float length = delta.magnitude;
            if (length < 1f) return false;
            float width = length / .937f;
            float height = Mathf.Clamp(halfWidth * 48f * 4.5f, 135f, 180f);
            int frame = Mathf.Min(7, Mathf.FloorToInt(progress * 8f));
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3(mouth.x, mouth.y, 0f),
                Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg),
                Vector3.one);
            // Sheet is right-to-left; the measured nozzle pivot is (.937, .53).
            bool drawn = Frame(breath, frame,
                new Rect(-width * .937f, -height * .53f, width, height),
                1f - Mathf.Clamp01((progress - .82f) / .18f));
            GUI.matrix = previous;
            return drawn;
        }

        internal bool DrawFallingFireball(Vector2 ground, float fall, int loopingFrame)
        {
            Load();
            if (firefall == null) return false;
            float width = 75f;
            float height = width * CellAspect(firefall);
            Vector2 anchor = ground + new Vector2(0f, -150f * (1f - Mathf.Clamp01(fall)));
            return Frame(firefall, loopingFrame & 3,
                new Rect(anchor.x - width * .5f, anchor.y - height * .925f, width, height), 1f);
        }

        internal bool DrawFireImpact(Vector2 ground, float progress)
        {
            return DrawFireImpact(ground, progress, 180f, 1f);
        }

        internal bool DrawFireImpact(Vector2 ground, float progress, float width, float heightScale)
        {
            Load();
            if (firefall == null) return false;
            float height = width * CellAspect(firefall) * heightScale;
            int frame = 4 + Mathf.Min(3, Mathf.FloorToInt(progress * 4f));
            return Frame(firefall, frame,
                new Rect(ground.x - width * .5f, ground.y - height * .902f, width, height),
                1f - Mathf.Clamp01((progress - .8f) / .2f));
        }

        internal bool DrawGolem(Vector2 ground, float radius, float progress)
        {
            Load();
            return GroundSheet(golem, ground, radius * 83f * 1.65f, .94f, .88f, progress);
        }

        internal bool DrawFrost(Vector2 ground, float radius, float progress)
        {
            Load();
            return GroundSheet(frost, ground, radius * 83f * 1.8f, .94f, .925f, progress);
        }

        internal bool DrawCharge(Vector2 impact, float width, float progress)
        {
            Load();
            return GroundSheet(charge, impact, width * 83f * 2.4f, .98f, .86f, progress, true);
        }
    }
}
