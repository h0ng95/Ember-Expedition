using UnityEngine;

namespace Expedition.Presentation
{
    // Original hand-authored 20 px UI sprites. Transparent margins are trimmed before
    // rendering, so every symbol is centered by its visible silhouette, not its canvas.
    public static class ResearchPixelIcons
    {
        public const int Size = 20;
        // . transparent, o outline, s shade, m metal, h highlight, a accent.
        static readonly string[][] Art = {
            new[] { // Crenellated wall: front-line endurance.
                "....................", "....................", "..oooo..oooo..oooo..",
                "..ohmo..ohmo..ohmo..", "..ohmo..ohmo..ohmo..", "..ohmoooohmoooohmo..",
                "..ohhhhhhhhhhhhhmo..", "..ommmmmmmmmmmmmso..", "..omssomssomssomso..",
                "..ohhhmhhhmhhhmhso..", "..ommmmmmmmmmmmmso..", "..omsssomsssomssso..",
                "..ohhhhmhhhhmhhhso..", "..ommmmmmmmmmmmmso..", "..omssomssomssomso..",
                "..ohhhmhhhmhhhmhso..", "..ommmmmmmmmmmmmso..", "..osssssssssssssso..",
                "..oooooooooooooooo..", "...................."
            },
            new[] { // Faceted kite shield with a bright protective core.
                "....................", ".......oooooo.......", "....ooohhhhhhooo....",
                "..oohhhmmmmmmmhhoo..", "..ohmmmmmmmmmmmmso..", "..ohmssssssssssmso..",
                "..ohmsooomooosomso..", "..ohmsomhhmosomso...", "..ohmsomhamosomso...",
                "..ohmsomhamosomso...", "...ohsomhhmososo....", "...ohssoomoososo....",
                "....ohsmmmmmsso.....", "....ohsmmmmmsso.....", ".....ohsmmssso......",
                "......ohmsso........", ".......ohso.........", "........oo..........",
                "....................", "...................."
            },
            new[] { // Bow and nocked arrow.
                "....................", "......oo............", "......ohho..........",
                ".......omho.........", ".......omos.........", ".......omosoo.......",
                ".......omo..so......", "..oo...omo...so.....", "..ohoo.omo....so....",
                "...ohhoomhhhhhhhoo..", "...ommmmmmmmmmmmaao.", "..oso..omo....so....",
                "..oo...omo...so.....", ".......omo..so......", ".......omosoo.......",
                ".......omos.........", ".......omho.........", "......ohho..........",
                "......oo............", "...................."
            },
            new[] { // Crosshair and exposed weak point.
                "....................", "........oooo........", "........ohmo........",
                ".....oooohmoooo.....", "....ohhhmhhmhhmo....", "...ohmo..hm..omso...",
                "...omo...hm...omo...", "...omo..oooo..omo...", ".ooohhooohhooohhooo.",
                ".ohhhhhhoaaohhhhhmo.", ".ommmmmmoaaommmmmso.", ".ooossooossooossooo.",
                "...omo..oooo..omo...", "...omo...hm...omo...", "...osmo..hm..omso...",
                "....osssmhhmssso....", ".....oooohmoooo.....", "........ohmo........",
                "........oooo........", "...................."
            },
            new[] { // Open grimoire and spell diamond.
                ".........oo.........", "........ohho........", ".......ohahho.......",
                "........ommo........", ".........oo.........", "....................",
                "..oooooo....oooooo..", ".ohhhhhho..ohhhhhho.", ".ohmmmmhhoohhmmmmo..",
                ".ohmssmmhmmhmmssmo..", ".ohmmmmmhmmhmmmmmo..", ".ohmssmmhmmhmmssmo..",
                ".ohmmmmmhmmhmmmmmo..", ".ohmssmmhmmhmmssmo..", ".ohmmmmmhmmhmmmmmo..",
                ".ohhhhhhhmmhhhhhmo..", ".osssssssmmsssssso..", "..ooooooommoooooo...",
                ".........oo.........", "...................."
            },
            new[] { // A flame with two tongues and a luminous ember.
                ".........oo.........", "........ohmo........", ".......ohmmo........",
                ".......ohmso........", "......ohmmso........", "...oo.ohmmsoo.......",
                "...ohommhmmso.......", "...ohmmhhmmso.oo....", "...ohmhhammsooho....",
                "..ohmmhaahmmmho.....", "..ohmmhaaahmmho.....", "..ohmhaaaahmmso.....",
                "..ohmhaaahhmmso.....", "...ohmaaahmmmso.....", "...ohmhaahmmso......",
                "....ohmhhmmso.......", ".....ohmmmso........", "......osssso........",
                ".......oooo.........", "...................."
            }
        };
        static readonly uint[][] Palettes = {
            new uint[] { 0x332C31, 0x826040, 0xCBA05D, 0xFFE3A0, 0xFFF1B8 },
            new uint[] { 0x252E44, 0x596A87, 0x9CB9CC, 0xE9F5FF, 0xFFC468 },
            new uint[] { 0x392B34, 0x925231, 0xD68F4B, 0xFFD390, 0xFCE7B0 },
            new uint[] { 0x3C2733, 0x993D45, 0xEF7957, 0xFFD9A3, 0xFFE6A5 },
            new uint[] { 0x24334D, 0x4972A4, 0x91B9E1, 0xD7EFFF, 0x83E8FF },
            new uint[] { 0x5F2930, 0xCB4933, 0xF57B36, 0xFFCB66, 0xFFF1AA }
        };

        public static Texture2D Create(int index)
        {
            var rows = Art[index];
            int left = Size, top = Size, right = -1, bottom = -1;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (rows[y][x] != '.') { left = Mathf.Min(left, x); right = Mathf.Max(right, x); top = Mathf.Min(top, y); bottom = Mathf.Max(bottom, y); }
            var texture = new Texture2D(right - left + 1, bottom - top + 1, TextureFormat.RGBA32, false) {
                name = "Research pixel icon " + index, filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[texture.width * texture.height];
            for (int y = top; y <= bottom; y++)
                for (int x = left; x <= right; x++) pixels[(bottom - y) * texture.width + x - left] = Shade(rows[y][x], index);
            texture.SetPixels(pixels); texture.Apply(false, true);
            return texture;
        }

        static Color Shade(char pixel, int index)
        {
            int shade = "osmha".IndexOf(pixel);
            if (shade < 0) return Color.clear;
            uint rgb = Palettes[index][shade];
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        }
    }
}
