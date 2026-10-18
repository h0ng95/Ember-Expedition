# UI concept asset prompts

Generated on 2026-10-18 using the built-in `image_gen.imagegen` tool. Newly generated images; no input image references. These are concept assets for the first UI review, not final production sprites.

## Release review status · 2026-10-18

All images in this record, including all six characters in both sheets, are **concepts awaiting release review**. Generation records have been inspected; character-by-character comparison against existing IP and release rights review have not been completed. Newly generated does not certify originality, non-infringement, or exclusive copyright ownership. Historical prompts below are preserved verbatim and are not legal conclusions.

User requirement: use independently designed characters and exclude unresolved resemblance to existing characters. Do not trace, extract, recolor, or request minor variants of existing game characters. Record visual comparisons and revisions before selecting production designs. Review names, silhouettes, faces, equipment, motifs, and color combinations together. The dark hood / glowing eyes combination in the sage is flagged for a design differentiation review, not a finding of infringement.

## Forest battlefield

- Workspace file: `forest-battlefield.png`
- Dimensions: 1672 × 941 pixels
- Format: PNG, RGB
- Original generated file: `C:/Users/whwog/.codex/generated_images/01a14e78-770e-7a01-831c-16739142a0b8/exec-9ab515fa-f4ce-4a00-a406-32f1e575ed3a.png`
- Tool: built-in `image_gen.imagegen`
- `transparent_background: false`

### Exact prompt

Use case: stylized-concept
Asset type: original pixel-art mobile landscape autobattler battlefield background, without interface or characters.
Primary request: A wide 16:9 landscape empty playable battlefield in ancient forest ruins, rendered as carefully composed chunky pixel art with crisp discrete pixel clusters and limited harmonized colors.
Scene/backdrop: Warm pale daylight in a moss-covered forest. A huge broken stone arch stands in the upper center distance. Layered teal forest shadows, old ivy-covered stone shapes, ochre grassy ground, trees frame only the far left and right edges.
Composition/framing: A straight-on game battle stage with slight view down onto the ground, like a classic side-view RPG battlefield. Keep vertical 45 percent through 90 percent as a broad flat clearly readable open ground plane, uninterrupted across most of the image width for many draggable characters. Distant scenery and stone arch occupy the upper 45 percent. Ground near the foreground is spare and calmer than the distant scenery.
Lighting/mood: Warm daylight, quiet adventure, bright readable playable ground with soft teal shadows.
Style/medium: Authentic hand-placed-looking pixel game art, intentionally large crisp pixels, strong shape language, restrained detail, no gradients or smooth illustration.
Constraints: Completely empty of characters, creatures, people, UI, cards, grid lines, letters, logos or watermarks. No foreground obstacles in the central playable area. Landscape composition approximately 16:9. Create new original art, not a reproduction of any existing game.

## Expedition sprite sheet

- Workspace file: `expedition-sprites.png`
- Dimensions: 1254 × 1254 pixels
- Format: PNG, RGBA. Alpha extrema inspected: 0–255 (genuine transparency).
- Original generated file: `C:/Users/whwog/.codex/generated_images/01a14e78-770e-7a01-831c-16739142a0b8/exec-8d0a62c1-d6aa-4ad3-8e9e-b3a1c216838c.png`
- Tool: built-in `image_gen.imagegen`
- `transparent_background: true`
- Cell layout: 3 columns × 2 rows requested. Visually inspected: knight's sword and sage's lantern encroach on neighboring nominal cell bounds; use individual crop windows with extra care for production. This prototype may display fixed crop windows; final production sprites require cleaner separation.

### Exact prompt

Use case: stylized-concept
Asset type: Original game character sprite sheet with genuine transparent alpha background.
Primary request: One square pixel-art sprite sheet containing exactly six separate original full-body characters in 3 equal columns by 2 equal rows. Each character is centered in its equally sized cell with generous transparent margins and clean isolated edges.
Subjects, exact cell order:
TOP LEFT: a small hooded teal lantern sage with a glowing pale gold lantern and readable mysterious face.
TOP CENTER: a chunky cream-and-gold shield knight with large round shield, small helmet, sturdy cute silhouette.
TOP RIGHT: a nimble archer wearing a rust-orange scarf and carrying a short wooden bow.
BOTTOM LEFT: a small teal-and-bronze walking automaton, squat mechanical body, expressive single lamp eye.
BOTTOM CENTER: a purple mushroom alchemist with broad mushroom cap, small bottle and satchel, curious and friendly.
BOTTOM RIGHT: a red-moss horned forest monster with a squat sturdy silhouette, large leaf-like mane and two short horns.
Pose: All standing idle in three-quarter view facing toward the right except bottom-right monster faces toward the left. Entire bodies and equipment visible and uncropped. Characters consistent in pixel size, ground baseline inside each cell, and game art style.
Style/medium: Curated detailed but chunky pixel art, crisp square pixel clusters, small bold silhouettes, limited warm cream, teal, rust and purple palette, dark colored outline, cute fantasy adventure. No smooth painterly shading.
Constraints: ACTUAL TRANSPARENT background, not painted checkerboard. Exactly 3 columns and 2 rows, no text, no labels, no cell boundaries, no tiles, no environment, no scenery, no shadow beneath characters, no UI, no watermarks. No Pokémon characters, no existing game characters. Characters must not cross between cells.

## Expedition sprite sheet v2 (preferred for the UI prototype)

- Workspace file: `expedition-sprites-v2.png`
- Dimensions: 1254 × 1254 pixels
- Format: PNG, RGBA. Verified alpha extrema 0–255.
- Original generated file: `C:/Users/whwog/.codex/generated_images/01a14e78-770e-7a01-831c-16739142a0b8/exec-e35b7eda-9478-4ca3-a2d1-37ff5cf0fdcb.png`
- Tool: built-in `image_gen.imagegen`
- `transparent_background: true`
- Targeted correction: cleaner chunky pixels and more transparent space around each character. Visually verified all six characters remain inside their nominal 3-by-2 cells.
- Nominal cells: 418 × 627 pixels, row-major order sage / knight / archer / automaton / alchemist / monster.
- Approximate tight opaque bounds, measured at alpha > 100, absolute pixels (left, top, right, bottom): sage (90, 220, 395, 609); knight (503, 194, 789, 609); archer (906, 224, 1170, 609); automaton (85, 757, 370, 1126); alchemist (507, 786, 766, 1126); monster (901, 779, 1189, 1125). Add a small margin when displaying tight crops.

### Exact prompt

Use case: stylized-concept
Asset type: pixel-art game sprite sheet with genuine alpha transparency.
Generate a square transparent PNG sprite sheet with exactly 6 tiny original pixel characters arranged in 3 equal columns and 2 equal rows. Very important technical constraint: each character is COMPLETELY CONTAINED INSIDE THE CENTRAL 60% WIDTH AND CENTRAL 65% HEIGHT OF ITS VIRTUAL CELL. The other 40% width is empty transparent margin. Thus the overall sheet has enormous clear gaps between characters. No sprite or weapon may reach neighboring cells.
Sprite proportions: a character is about 48 by 64 distinct chunky pixel units; render with large square pixel clusters and flat limited palette. Strict crisp pixel sprite art, like a classic 16-bit handheld RPG enlarged with nearest-neighbor scaling. Use 3 or 4 flat colors per material. Absolutely no airbrushed softness, no gradients, no smooth illustration and no microtextures.
Positions and subjects:
Top-left: teal hooded lantern sage, gold eyes in dark hood, short staff with tiny lantern held close to the body.
Top-middle: stout cream and gold shield knight, teal accent, round shield held close in front, short sword held vertically next to body.
Top-right: rust-orange scarf archer, tiny wooden shortbow held upright close to body, cream hair and olive tunic.
Bottom-left: round teal and bronze little walking automaton with a single glowing lantern eye and a small sprout on head.
Bottom-middle: purple mushroom alchemist with short mushroom cap fitting inside the cell, small potion and tan satchel.
Bottom-right: short red-moss forest monster with 2 cream horns and rust leaf mane, tiny amber eyes.
Characters face three-quarter right except monster three-quarter left. Full body from head to feet. All approximately equal height. Cute original fantasy adventure silhouettes.
Background: REAL TRANSPARENT alpha, zero opacity. No painted checkerboard, no ground shadows, no backdrop, no cell lines, no text, no labels. Exactly six sprites with huge clear gaps, no touching sprites, no extra objects. Entire image has a wide empty transparent outer border. This is a clean technical sprite atlas, not a character illustration poster.
