# Forest enemies v0.19 — animation generation

2026-10-23. Built-in `image_gen.imagegen`; `transparent_background: true` on each call. Four distinct assets generated, followed by one spacing correction each for elite and boss. Reference: `docs/design-v0.18/assets/forest-enemy-concepts-v1.png`, our original character concept, used for identity and style. No external game artwork supplied. No raster postprocessing. Selected PNGs remain unchanged in `unity/Assets/Resources/Art/Enemies/v019/`. Each is 1536 × 1024 RGBA, 24 poses. Raw RGB under zero-alpha background may look brown in a viewer that ignores alpha; the engine must respect alpha. Adaptive gutter detection is needed before importing frames. These are the first animation pass, with motion review recorded separately.

Final selection: cub and thorn use the initial outputs below; elite and boss use the one-pass spacing corrections documented in [spacing-correction.md](spacing-correction.md). All chosen files retain their original generated pixels.

## Initial output sources

| File | Original output filename |
| --- | --- |
| forest-cub.png | exec-ae072454-9d19-4354-bb24-b89f5edc2119.png |
| forest-thorn.png | exec-c66c9a2c-4654-43e1-b367-357da3e6a0fa.png |
| forest-elite.png | exec-1b45b6ac-c913-4531-bda0-fe8896a3d20a.png |
| forest-boss.png | exec-8b63b3b2-aa68-48a8-9b91-44fba8605910.png |

Original directory: `C:/Users/whwog/.codex/generated_images/01a14e3b-5142-7871-8565-822e92917bb8/`.

## Frame order

Indices 0–3 idle; 4–5 hit; 6–11 walk; 12–17 attack; 18–21 death; 22–23 charge anticipation. All naturally face left. Engine timing and actual in-game capture are required to validate attack/release synchronization; image generation alone does not establish correct timing.

## Exact prompts

### forest-cub

Use case: stylized-concept. Asset type: ONE production-oriented transparent pixel-art animation sprite sheet for an original medieval mobile auto-battler.
Input reference: the supplied four-enemy atlas is an IDENTITY AND STYLE REFERENCE. Animate ONLY the specified creature; do not include any of the other three species. Preserve its distinctive anatomy, silhouette, eye, horns/spines and colors consistently in all frames.
Layout: landscape 3:2 canvas, requested 1536x1024 pixels, EXACTLY SIX EQUAL COLUMNS AND FOUR EQUAL ROWS, 24 frames, reading left to right then top to bottom. Each invisible square cell is 256x256. Never draw grid lines or labels. Every cell contains one complete full-body pose. Character and every horn, tail and claw must remain inside the middle 72 percent of cell width and 70 percent of cell height. Leave conspicuous transparent gaps. Standing feet baseline is at 85 percent of each cell height. Keep same body size, same camera, same central body anchor in every frame; do not resize the character to fit a pose. Death naturally lowers the body but feet/body floor remain at same baseline. All poses face THREE-QUARTER LEFT, nose and eyes pointing to screen-left. NO rotation to face right, front or back.
Animation map:
Row 1: columns 1-4 are a subtle four-frame idle breathing loop (calm, inhale, full inhale, settle), columns 5-6 are two hit-reaction frames (recoil away from leftward attacker, recover; face remains LEFT).
Row 2: columns 1-6 are a true six-frame walking cycle toward LEFT: visibly different alternating foreleg and hindleg positions, contact/down/passing/up/opposite contact/return. Animate legs rather than just translate/resize one drawing. The body center stays anchored in its cell.
Row 3: columns 1-6 are a six-frame attack sequence: anticipation, deepest windup, explosive LEFTWARD release, follow-through, recoil, return. Frames 3-4 carry the attack. No detached projectiles or motion effects.
Row 4: columns 1-4 are a four-frame non-gory death: stagger, forelegs buckle, collapse low, fully fallen still; head remains on the LEFT. Columns 5-6 are two dedicated charged-attack anticipation poses: lowered center of gravity, then deeper committed ready stance, not an attack release.
Rendering: match the reference's compact charming woodland pixel art, chunky hard square pixel clusters, dark outline and flat 3-4 tone ramps. About 48-64 logical pixel units per character, enlarged crisp. No smooth painted detail, no photorealism, no 3D, no tiny noisy textures, no blur. Warm bark brown, dark charcoal, moss olive, ivory horn/spines, rust-orange leaf details, tiny amber eye.
Background: REAL TRANSPARENT alpha, zero opacity. No background, checkerboard, floor, cast shadow, glow, text, titles, labels, borders, gore, extra objects or other characters. Exactly 24 discrete separated poses of the SAME creature.

Subject: ONLY the TOP LEFT horn cub in the reference: small squat four-legged woodland animal, short ivory horn buds, low snout pointing left, dark umber bark-fur, small rust leaf neck tuft, moss back and short twig tail. Preserve a SMALL simple grunt identity with no skull face plate or boss mane. Attack is a short headbutt: tuck head and compress forelegs in frames1-2, thrust head and short horns LEFT in frames3-4, withdraw in5-6. Charge poses lower head ready to headbutt.

### forest-thorn

Use case: stylized-concept. Asset type: ONE production-oriented transparent pixel-art animation sprite sheet for an original medieval mobile auto-battler.
Input reference: the supplied four-enemy atlas is an IDENTITY AND STYLE REFERENCE. Animate ONLY the specified creature; do not include any of the other three species. Preserve its distinctive anatomy, silhouette, eye, horns/spines and colors consistently in all frames.
Layout: landscape 3:2 canvas, requested 1536x1024 pixels, EXACTLY SIX EQUAL COLUMNS AND FOUR EQUAL ROWS, 24 frames, reading left to right then top to bottom. Each invisible square cell is 256x256. Never draw grid lines or labels. Every cell contains one complete full-body pose. Character and every horn, tail and claw must remain inside the middle 72 percent of cell width and 70 percent of cell height. Leave conspicuous transparent gaps. Standing feet baseline is at 85 percent of each cell height. Keep same body size, same camera, same central body anchor in every frame; do not resize the character to fit a pose. Death naturally lowers the body but feet/body floor remain at same baseline. All poses face THREE-QUARTER LEFT, nose and eyes pointing to screen-left. NO rotation to face right, front or back.
Animation map:
Row 1: columns 1-4 are a subtle four-frame idle breathing loop (calm, inhale, full inhale, settle), columns 5-6 are two hit-reaction frames (recoil away from leftward attacker, recover; face remains LEFT).
Row 2: columns 1-6 are a true six-frame walking cycle toward LEFT: visibly different alternating foreleg and hindleg positions, contact/down/passing/up/opposite contact/return. Animate legs rather than just translate/resize one drawing. The body center stays anchored in its cell.
Row 3: columns 1-6 are a six-frame attack sequence: anticipation, deepest windup, explosive LEFTWARD release, follow-through, recoil, return. Frames 3-4 carry the attack. No detached projectiles or motion effects.
Row 4: columns 1-4 are a four-frame non-gory death: stagger, forelegs buckle, collapse low, fully fallen still; head remains on the LEFT. Columns 5-6 are two dedicated charged-attack anticipation poses: lowered center of gravity, then deeper committed ready stance, not an attack release.
Rendering: match the reference's compact charming woodland pixel art, chunky hard square pixel clusters, dark outline and flat 3-4 tone ramps. About 48-64 logical pixel units per character, enlarged crisp. No smooth painted detail, no photorealism, no 3D, no tiny noisy textures, no blur. Warm bark brown, dark charcoal, moss olive, ivory horn/spines, rust-orange leaf details, tiny amber eye.
Background: REAL TRANSPARENT alpha, zero opacity. No background, checkerboard, floor, cast shadow, glow, text, titles, labels, borders, gore, extra objects or other characters. Exactly 24 discrete separated poses of the SAME creature.

Subject: ONLY the TOP RIGHT thornback in the reference: lean four-legged woodland animal with a narrow leftward snout, small head, an iconic fan of five long pale botanical spines on its back, bark brown body, moss olive, small rust neck tuft. No large facial horns, skull mask, or boss mane. All FIVE spines stay attached and fit fully inside each cell. Attack: hunch and compress in frames1-2; raise and spread the back-spine fan sharply in frames3-4 to launch toward LEFT; settle fan in5-6. No detached spines or projectiles (engine draws those separately). Special anticipation poses compress body, bracing rear legs with raised spines.

### forest-elite

Use case: stylized-concept. Asset type: ONE production-oriented transparent pixel-art animation sprite sheet for an original medieval mobile auto-battler.
Input reference: the supplied four-enemy atlas is an IDENTITY AND STYLE REFERENCE. Animate ONLY the specified creature; do not include any of the other three species. Preserve its distinctive anatomy, silhouette, eye, horns/spines and colors consistently in all frames.
Layout: landscape 3:2 canvas, requested 1536x1024 pixels, EXACTLY SIX EQUAL COLUMNS AND FOUR EQUAL ROWS, 24 frames, reading left to right then top to bottom. Each invisible square cell is 256x256. Never draw grid lines or labels. Every cell contains one complete full-body pose. Character and every horn, tail and claw must remain inside the middle 72 percent of cell width and 70 percent of cell height. Leave conspicuous transparent gaps. Standing feet baseline is at 85 percent of each cell height. Keep same body size, same camera, same central body anchor in every frame; do not resize the character to fit a pose. Death naturally lowers the body but feet/body floor remain at same baseline. All poses face THREE-QUARTER LEFT, nose and eyes pointing to screen-left. NO rotation to face right, front or back.
Animation map:
Row 1: columns 1-4 are a subtle four-frame idle breathing loop (calm, inhale, full inhale, settle), columns 5-6 are two hit-reaction frames (recoil away from leftward attacker, recover; face remains LEFT).
Row 2: columns 1-6 are a true six-frame walking cycle toward LEFT: visibly different alternating foreleg and hindleg positions, contact/down/passing/up/opposite contact/return. Animate legs rather than just translate/resize one drawing. The body center stays anchored in its cell.
Row 3: columns 1-6 are a six-frame attack sequence: anticipation, deepest windup, explosive LEFTWARD release, follow-through, recoil, return. Frames 3-4 carry the attack. No detached projectiles or motion effects.
Row 4: columns 1-4 are a four-frame non-gory death: stagger, forelegs buckle, collapse low, fully fallen still; head remains on the LEFT. Columns 5-6 are two dedicated charged-attack anticipation poses: lowered center of gravity, then deeper committed ready stance, not an attack release.
Rendering: match the reference's compact charming woodland pixel art, chunky hard square pixel clusters, dark outline and flat 3-4 tone ramps. About 48-64 logical pixel units per character, enlarged crisp. No smooth painted detail, no photorealism, no 3D, no tiny noisy textures, no blur. Warm bark brown, dark charcoal, moss olive, ivory horn/spines, rust-orange leaf details, tiny amber eye.
Background: REAL TRANSPARENT alpha, zero opacity. No background, checkerboard, floor, cast shadow, glow, text, titles, labels, borders, gore, extra objects or other characters. Exactly 24 discrete separated poses of the SAME creature.

Subject: ONLY the BOTTOM LEFT charge hornbeast in the reference: heavy low four-legged ram-like bark beast with huge angular shoulder plates, TWO LONG forward-swept ivory horns, low left-facing head, thick forelegs, small rust leaf collar. Preserve forward-swept horizontal horns, long low body, short twig tail. No full boss mane or mask. Attack is a brutal horn shove: brace forelegs and tuck head1-2, extend horn tips strongly LEFT3-4 while hindlegs push, recover5-6. Row4columns5-6 especially important: a distinctly lowered head with horns leveled LEFT, front paw scraping back, then both hindlegs braced for a charge; preserve feet baseline.

### forest-boss

Use case: stylized-concept. Asset type: ONE production-oriented transparent pixel-art animation sprite sheet for an original medieval mobile auto-battler.
Input reference: the supplied four-enemy atlas is an IDENTITY AND STYLE REFERENCE. Animate ONLY the specified creature; do not include any of the other three species. Preserve its distinctive anatomy, silhouette, eye, horns/spines and colors consistently in all frames.
Layout: landscape 3:2 canvas, requested 1536x1024 pixels, EXACTLY SIX EQUAL COLUMNS AND FOUR EQUAL ROWS, 24 frames, reading left to right then top to bottom. Each invisible square cell is 256x256. Never draw grid lines or labels. Every cell contains one complete full-body pose. Character and every horn, tail and claw must remain inside the middle 72 percent of cell width and 70 percent of cell height. Leave conspicuous transparent gaps. Standing feet baseline is at 85 percent of each cell height. Keep same body size, same camera, same central body anchor in every frame; do not resize the character to fit a pose. Death naturally lowers the body but feet/body floor remain at same baseline. All poses face THREE-QUARTER LEFT, nose and eyes pointing to screen-left. NO rotation to face right, front or back.
Animation map:
Row 1: columns 1-4 are a subtle four-frame idle breathing loop (calm, inhale, full inhale, settle), columns 5-6 are two hit-reaction frames (recoil away from leftward attacker, recover; face remains LEFT).
Row 2: columns 1-6 are a true six-frame walking cycle toward LEFT: visibly different alternating foreleg and hindleg positions, contact/down/passing/up/opposite contact/return. Animate legs rather than just translate/resize one drawing. The body center stays anchored in its cell.
Row 3: columns 1-6 are a six-frame attack sequence: anticipation, deepest windup, explosive LEFTWARD release, follow-through, recoil, return. Frames 3-4 carry the attack. No detached projectiles or motion effects.
Row 4: columns 1-4 are a four-frame non-gory death: stagger, forelegs buckle, collapse low, fully fallen still; head remains on the LEFT. Columns 5-6 are two dedicated charged-attack anticipation poses: lowered center of gravity, then deeper committed ready stance, not an attack release.
Rendering: match the reference's compact charming woodland pixel art, chunky hard square pixel clusters, dark outline and flat 3-4 tone ramps. About 48-64 logical pixel units per character, enlarged crisp. No smooth painted detail, no photorealism, no 3D, no tiny noisy textures, no blur. Warm bark brown, dark charcoal, moss olive, ivory horn/spines, rust-orange leaf details, tiny amber eye.
Background: REAL TRANSPARENT alpha, zero opacity. No background, checkerboard, floor, cast shadow, glow, text, titles, labels, borders, gore, extra objects or other characters. Exactly 24 discrete separated poses of the SAME creature.

Subject: ONLY the BOTTOM RIGHT Ashmane alpha BOSS in the reference: colossal broad hunched forest beast with huge bark forearms and planted claws, enormous rust-orange leaf mane framing shoulders, pale elongated wooden face plate pointing LEFT with amber eye, two massive up-curved ivory horns, moss patches and diagonal shoulder scar. Preserve boss's broad arm-heavy silhouette in all frames, never shrink to grunt proportions. Entire horns and claws inside each cell. Idle breath lifts shoulders and mane subtly. Walk swings huge forearms alternately. Attack: lower and draw nearest forearm back1-2, drive heavy clawed forearm LEFT/down3-4, recover5-6. Death: shoulders sag then forearms buckle and huge body lies low, head LEFT. Row4columns5-6 are dedicated LINE-CHARGE warnings: both forepaws plant, head lowers and horns tilt LEFT, mane lifts, body compresses then deeper poised launch. Keep silhouette threatening.
