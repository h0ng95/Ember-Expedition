# Forest enemy concepts v1 — generation record

- Date: 2026-10-23
- Tool: built-in `image_gen.imagegen` (not CLI).
- Use case: `stylized-concept`.
- Status: four-role concept atlas; not an animation sheet or production-ready pixel atlas.
- Output: `assets/forest-enemy-concepts-v1.png` (1254 × 1254, RGBA).
- `transparent_background: true`. Alpha inspected; corner alpha is 0.
- Original output: `C:/Users/whwog/.codex/generated_images/01a14e3b-5142-7871-8565-822e92917bb8/exec-ba820bee-e65f-4b3c-8746-be006d68daa8.png`.
- Workspace output: `C:/Users/whwog/project/game/docs/design-v0.18/assets/forest-enemy-concepts-v1.png`.
- Reference: `unity/Assets/Resources/Art/expedition-sprites-v2.png`, the project's existing original sheet, used only as a style reference. No external game sprites were used as generation inputs.
- One composed four-role sheet generated in one call; first output selected. No raster postprocessing or mirroring. CSS displays rectangular portions of the unchanged source in the preview.
- Generation provenance is a production record, not a legal clearance or a guarantee of non-infringement. Release review follows the project's existing asset policy.

## Inspection and display coordinates

Coordinates below use a top-left origin, measured at alpha > 100. Rectangles are inclusive bounds.

| Subject | Left | Top | Right | Bottom |
| --- | ---: | ---: | ---: | ---: |
| Horn cub | 154 | 311 | 552 | 583 |
| Thornback | 702 | 99 | 1194 | 583 |
| Charge hornbeast | 23 | 774 | 655 | 1154 |
| Ashmane alpha | 663 | 627 | 1235 | 1164 |

The elite extends past the nominal 627-pixel midpoint. Do not import this as four uniform grid sprites. The preview uses individual bounding rectangles with a small margin. Original pixel clusters still require consistency cleanup at the chosen production scale. All four subjects visibly face left.

## Exact prompt

Use case: stylized-concept.
Asset type: ONE cohesive four-character enemy concept atlas, transparent PNG, square canvas with an invisible 2 by 2 grid.
Input reference: expedition-sprites-v2.png is our project's original STYLE REFERENCE ONLY for medieval chibi proportions, chunky pixel clusters, dark contour weight, warm bone highlights and compact readable shapes. Do not reproduce the friendly characters. Develop four NEW woodland enemies as one related creature family.

LAYOUT is critical: exactly four full-body characters, one in each equal quadrant. Each creature stays within the central 75% width and 76% height of its quadrant. All feet end at the same local baseline, 87% down within each quadrant. Large genuinely transparent gaps between quadrants. No overlapping characters. Each creature faces unequivocally THREE-QUARTER LEFT (head, snout, gaze and attack direction toward screen-left). No front-facing portraits or right-facing creatures. Show idle-ready stance. Design the subjects at comparable atlas occupancy; their different in-game scale will be set separately. No text, labels or border.

TOP LEFT — HORN CUB, common melee grunt. A very SIMPLE small squat FOUR-LEGGED creature made of dark umber bark and mossy fur, stubby paws, rounded haunch, short exposed snout and two tiny ivory horn buds pointing forward. One small amber eye. A little ochre leaf tuft on the neck, only a few moss patches. No skull mask, no huge mane, no elaborate armor. A vulnerable low-rank animal silhouette, with its head clearly at the left and a short twig tail to the right. A small enemy must not look like a miniature boss.

TOP RIGHT — THORNBACK, ranged grunt. A lean low FOUR-LEGGED woodland creature with a pointed left-facing snout and small head; dark bark plates and muted olive moss; a conspicuous FAN OF FIVE LONG PALE THORN/SEED-POD SPINES rising diagonally backward from its back, like a botanical launching rack. The long pale back spines are its defining weapon, not horns on the face. Tiny amber eye and one ochre collar tuft tie it to the family. No skull mask, no giant mane. Entire spine tips visible with transparent margin. Distinctly different outline from the horn cub and the boss.

BOTTOM LEFT — CHARGE HORNBEAST, elite. A heavy forward-leaning FOUR-LEGGED ram-like woodland brute with prominent high shoulders, low head to the left, TWO LONG SWEPT-FORWARD IVORY HORNS, angular layered bark shoulder plates and thick forelegs. One amber eye, short rust-ochre neck bristles. Compact rear legs and a low planted charging stance. More threatening than either grunt but restrained: no royal crown, no boss mane or full skull mask. Its broad shoulders and horizontal horns read as a battering ram.

BOTTOM RIGHT — ASHMANE ALPHA, boss. A very imposing wide-bodied ancient forest beast, upright hunched gorilla-like stance on two huge forelimbs, small hind feet, broad shoulders. A distinctive asymmetrical pale wooden FACE PLATE, not a human skull, with a long downward muzzle facing LEFT, a single deep amber eye slit; TWO MASSIVE IVORY HORNS, one with a broken/notched tip, swept outward and up. Wide layered RUST-ORANGE/ASH-OCHRE LEAF MANE with dark charcoal roots frames its head and shoulders. Dark umber body, sparse moss patches, thick bark forearm armor, large planted claws. One diagonal old scar through bark on the shoulder. Powerful low poised posture, full silhouette visible, no weapons or accessories. The mane and asymmetrical horns belong to this boss alone.

Rendering: charming but formidable original 16-bit-style medieval fantasy pixel sprites. LARGE CRISP SQUARE PIXEL CLUSTERS, intentional stepped edges, clean bold near-black outline, 3-4 flat tones per material, no airbrush or painterly surface, no gradients, no photorealism, no glossy 3D, no tiny noisy textures. Think about 64x64 logical pixels for small creatures, 80x80 for elite and 96x96 for boss, enlarged with nearest-neighbor edges. Restrained common palette of bark-brown, charcoal, moss olive, ivory, rust ochre, tiny amber eyes. Enough value separation from a green forest battlefield.
Background: REAL transparent alpha, zero opacity. NO checkerboard drawn into the image, NO scenery, shadows, floor, smoke, glow particles, captions, typography, logos, watermarks or extra creatures. Exactly four original creatures, no recognizable franchise designs.
