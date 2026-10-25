# 서리 보스 보행 부품 제작

Built-in `image_gen`, 2026-10-25. 선택한 원본을 캐릭터 참고로 사용했다. 관절 보행 실험용으로 제작했으나 채택하지 않았다. 사용자의 후속 요청에 따라 긴 판금 치마 디자인으로 대체했다. 부품 이미지는 screenshots/frost-rig-study.png에 보관한다.

```text
Use case: identity-preserve
Asset type: transparent 2D pixel-art character CUTOUT RIG PARTS atlas, not an animation frame sheet.
Reference: the attached first grey/navy frost armored knight is the approved identity. Match that exact muted dark steel armor, dull brass edge trim, dark navy cloth, blue ice crystals, cathedral helmet and cyan chest core. Preserve bulky proportions and detailed pixel shading. It faces LEFT in three-quarter view.
Create ONE transparent PNG with SIX separated sprites in a THREE-column TWO-row grid. Each cell 512x512, canvas1536x1024. This is a disassembled paper-doll kit for skeletal animation, not six complete characters. Actual transparent alpha; absolutely no background, ground shadow, numbers, labels, outlines around cells or text.
Cell1 TOP LEFT: complete upper body including original head, torso, shoulders, both fists, belt, and short hanging navy tassets. NO LEGS OR BOOTS. Reproduce the reference torso silhouette and colors closely. Lower garment ends just below the hip, leaves thighs free. Body should occupy ~430px wide and425px high centered in this cell.
Cell2 TOP MIDDLE: NEAR upper leg only, hip to knee, vertical relaxed orientation, approx125px wide x190px tall. Heavy rounded dark steel thigh armor, rounded dark top extension to overlap beneath belt. Anatomically complete even where previously covered by cloth. Include ornate kneecap at bottom. No boot or lower leg, no cloth.
Cell3 TOP RIGHT: NEAR lower leg with boot, from just above knee down to sole, approx145px wide x200px tall. Upright shin, oversized armored boot pointing LEFT, heel at lower right. Fully painted rounded upper knee connection for overlap. No thigh or cloth.
Cell4 BOTTOM LEFT: FAR upper leg only, same anatomical size as near thigh, vertical orientation, darkened slightly by depth. Fully painted hip and knee overlap. No lower leg/boot or cloth.
Cell5 BOTTOM MIDDLE: FAR lower leg and LEFT-pointing boot, same size as near lower leg, slightly darker. Entire leg piece fully painted with rounded knee connection. No thigh or cloth.
Cell6 BOTTOM RIGHT: separate small torn navy rear cape/tabard tails, hangs down and slightly right, about150x200px, matches reference. NO body parts.
Each sprite isolated within its cell with at least40px completely empty margin. All limb joints must have round fully opaque overlap areas, no holes, no cut-away gaps. All legs straight enough to rotate, same rendering scale and style. Do not assemble the character, do not repeat full knights. No swords, shield or extra props. Original game asset.
```
