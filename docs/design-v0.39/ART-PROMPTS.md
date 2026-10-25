# Boss VFX sprite sheets · v0.39

2026-10-25 · Built-in image_gen / transparent background. Original generated assets for this project; no third-party character references. Four columns × two rows per sheet. Prompts are retained below. Unity imports use Point/Clamp/no mip maps.

## golem-shatter.png

```text
Use case: stylized-concept
Asset type: transparent pixel-art 8-frame ROCK SLAM impact sprite animation sheet for medieval 2D game.
Single image2048x1024, FOUR equal columns and TWO equal rows, eight frames read row-major. Genuine transparent alpha. No text, grid, borders, ground tile, scene or character.
A heavy stone golem slams the ground: compact cluster of jagged grey-brown rocks bursts UP from fractured dirt, angular rock shards fly outward, low taupe dust puffs billow and disperse. Clear weight and three-dimensional shaded pixel volumes, NOT thin radial lines or outlines. Eight frames:1 initial tiny dirt fissure,2 rocks thrust halfway up,3 full jagged rock crown,4 rocks lifted and separating,5 rock chunks falling outward,6 rocks recede with dust,7 scattered dust/shards,8 small fading pebble motes.
Consistent fixed bottom-center anchor x50%,y86% per cell,8%blank margin on every edge, maximum80%cellwidth and65%cellheight. No pixels cross cell boundaries. Show side-view ground impact with a slightly elevated 3/4 perspective to fit a horizontal mobile battlefield.
Crisp chunky 16-bit pixel art,64x64native sprite appearance enlarged nearest neighbor, solid pixel clusters, restrained stone-grey/taupe palette with bright chipped edges and dark brown shadows. No glow blur, no smooth vector art. Original artwork.
```

## frost-eruption.png

```text
Use case: stylized-concept
Asset type: transparent pixel-art ICE ERUPTION 8-frame sprite atlas for a medieval 2D game.
Canvas2048x1024, exact FOUR equal columns and TWO equal rows, eight sequential frames row-major. Transparent alpha, no checkerboard, no text/grid/borders/scenery/characters.
A crystal cluster erupts UP from frost on the ground. Three major jagged translucent cyan ice spires and smaller shards; solid faceted pixel volumes with deep blue faces, pale cyan edges and brilliant white glints. Do NOT draw thin lines or wireframe triangles. Crisp chunky 16-bit pixel clusters,64x64 native sprite appearance enlarged nearest-neighbor; no blur or smooth vector shading.
Frames1 tiny frost shards and powder;2 spires half-height emerging;3 tall sharp crystal cluster;4 peak spread with crystalline glints;5 spires fracture into large chunks;6 shattering angular chips;7 falling fragments and low ice mist;8 a few fading ice chips. Coherent consistent shape evolution, same bottom-center origin x50%,y86% of every cell. Maximum80%width65%height, at least8%completely empty margin each edge, nothing crossing cells. Sideview slightly elevated 3/4 ground perspective. Original attack effect artwork only.
```

## charge-dust.png

```text
Use case: stylized-concept
Asset type: transparent 8-frame pixel-art DUST IMPACT PUFF atlas for a medieval 2D game beast charge.
Canvas2048x1024, FOUR equal columns and TWO equal rows, eight frames row-major. Genuine alpha transparent. Absolutely no text, grid, frames, character, background, ground tile.
A low explosive puff of chunky earthy dust, flying pebbles and dirt clods from a heavy beast hitting or skidding on the ground. Solid layered dusty cloud volumes in muted ochre/tan/brown, dark inner creases and lit upper pixel clusters. The cloud spreads horizontally, rises slightly, then dissolves.
Frames1 tiny grit kickup,2 small billowing puff,3 broader burst with outward rock chips,4 full wide low dusty cloud,5 rolling separated cloud lobes,6 diffuse smaller dust clumps,7 only a few clumps and pebbles,8 sparse dust flecks. Same bottom-center origin x50%,y86% every cell. Entire cloud fits within80%cellwidth and45%cellheight, generous transparent margins8%all sides. No streaks or radial lines or circles.
Crisp16-bit chunky pixel art, native64x64 appearance nearest-neighbor enlarged, compact flat shaded color clusters, no blur, no soft gradient, no photographic texture. Slightly elevated side-view ground perspective. Original effect artwork.
```

## frost-boss-walk.png · 사용자가 선택한 시안

사용자가 2026-10-25 첨부 이미지로 첫 번째 회색·남색 시안을 명시적으로 선택했다. 아래 원본 생성 결과를 그대로 사용하고, 뒤에 생성한 채도가 강한 시안과 다리 수정 시안은 적용하지 않았다. 디자인 참고로 보관한다. 보행의 양발 교차가 부족해 런타임 연결은 제거했으며 게임에는 적용하지 않았다.

```text
Use case: identity-preserve
Asset type: replacement WALK CYCLE sprite sheet for the frost armored boss shown in the reference.
Reference image role: ONLY character identity/style reference, NOT layout. Recreate that exact original frost boss design: bulky dark steel cathedral-shaped helmet, large rounded armored fists, blue ice clusters on shoulders/back, glowing cyan crystal inside chest, torn dark-blue cloth, short heavy armored legs. Do not redesign into another creature. No other characters.
Generate a NEW transparent sprite sheet with SIX full-body animation frames in exactly THREE columns and TWO rows. Canvas1536x1024. Genuine alpha background; no text, labels, separators, grid, shadows disconnected from feet, or scenery.
CRITICAL motion correction: a full alternating-legged leftward WALK cycle, NOT six near-identical poses with same front foot! Character faces LEFT in consistent three-quarter view in every frame. Frame1 LEFT LEG visibly forward, RIGHT LEG planted behind; frame2 weight onto left foot, right heel lifts; frame3 RIGHT LEG swings/passes left leg, torso nearly level; frame4 RIGHT LEG visibly forward, LEFT LEG planted behind; frame5 weight onto right foot, left heel lifts; frame6 LEFT LEG swings/passes right leg, loops toframe1. Clearly alternate the leg silhouettes and opposing fists. Heavy slow grounded walking, restrained vertical movement, no running/jumping/dancing. Knees bend, armored feet clear ground during swing and flatly contact ground when planted. Keep same helmet/shoulder/chest dimensions, proportions, colors and lighting in allframes.
Each cell has identical foot ground line y90%, centered stable chest around x50%. Character stays within 8%margin each cell, full crown and feet never cut. Use dark muted original palette and detailed crisp pixel-art shading matching reference, not cartoon thick outlines. Strong readable chunky square pixels, same substantial bulky body in every frame. Original game asset, no branding.
```

## garden-roots.png

```text
Use case: stylized-concept
Asset type: transparent pixel-art sprite sheet for a medieval 2D game attack animation.
Create ONE production sprite sheet, 2048x1024 pixels, exactly FOUR equal columns by TWO equal rows, EIGHT animation frames read left-to-right then next row. Invisible grid; absolutely no text, labels, borders, background, ground rectangle, or checkerboard. Genuine transparent alpha.
Subject: a cluster of three thick twisted thorny roots erupts upward from one small ground origin. Bark is dark purple-brown with mossy green lit faces and sharp pale lime thorns; tiny chunks of earth and poison motes. Rich solid pixel clusters, shaded material volume, angular curves and overlapping thorns. NOT thin lines or wireframes. Style: polished 16-bit pixel game VFX, clearly visible square pixels, 64x64 native-pixel appearance per cell enlarged by nearest-neighbor, no blur/glow haze/vector strokes.
All eight frames share the same bottom-center anchor at 50% cell width and 88% cell height. Keep ALL pixels inside each cell with at least 8% padding; nothing crosses cells.
Frame1: small cracked earth and green tips, frame2: roots emerge one-third height, frame3: half-height curling roots, frame4: full tall twisted thorn cluster, frame5: full roots change curl and thorns catch light, frame6: roots splinter and crumble, frame7: lower stump and scattered fragments, frame8: sparse dissolving dirt/green motes. Maximum extent about 75% cell width and 72% cell height. Shape continuity across frames. This is ONLY attack visual effects, no characters, no UI, no scene. Original artwork.
```

## dragon-breath.png

```text
Use case: stylized-concept
Asset type: transparent 8-frame pixel-art dragon FIRE BREATH animation atlas for a 2D medieval game.
Single image 2048x1024, exact FOUR columns and TWO rows of equal square cells, 8 successive frames row-major. No text, grid, borders, opaque background, or checkerboard. Actual transparent alpha.
Draw only a SIDE-VIEW fire plume moving RIGHT TO LEFT. Every frame has the same narrow flame nozzle at 88% cell width / 52% cell height, with flame expanding towards the LEFT. No dragon or character. Full plume fits between x10% and88% and y24% and76%. Keep 8% completely empty padding on all cell edges.
Frame1 short ignition near right nozzle; frame2 half length jet; frame3 full length turbulent fire; frame4 full length curling plume with large tongues; frame5 different full plume tongues; frame6 jet breaks into flame clumps; frame7 disconnected burning curls and sparks; frame8 sparse embers. The eight frames are a smooth onset, blast, dissipation animation with a fixed emission anchor.
Art: crisp chunky 16-bit pixel VFX, clear square pixels and compact flat color clusters, as if 96x64 native sprites enlarged with nearest neighbor. Solid voluminous yellow-white flame core, bright golden body, orange/rust outer tongues, little black-red smoke curls only at dissipating stage. Rich fire silhouettes and internal negative gaps. NOT thin outlines, straight beams, vector lines, gradients, soft glow, or photorealistic fire. Original game asset.
```

## dragon-firefall.png

```text
Use case: stylized-concept
Asset type: one transparent pixel-art FALLING FIREBALL AND IMPACT sprite atlas for an original medieval 2D game.
Canvas 2048x1024, exactly FOUR columns by TWO equal rows, eight frames total. No text, grid lines, boxes, scenery, characters, opaque background, checkerboard, glow haze. Genuine alpha transparency.
Top row frames 1–4: FOUR different animation frames of the SAME compact falling fireball, pointed flame tail rising upward, bright rounded yellow-white hot head at 50% cell width, 70% cell height. Frame-to-frame curling orange tongues and flying sparks. Head remains same center and size, only tail flickers. Fireball width about30% cell and height65% cell, all within8% margin.
Bottom row frames5–8: the fireball strikes the ground and expands into a rounded fiery blast at fixed bottom-center anchor x50%,y86%. Frame5 squat ignition flash, frame6 broad explosion fire crown, frame7 broken orange flame curls and embers, frame8 sparse dark red smoke curls and cooling sparks. Each blast stays within8%cell margin. No ground tile, no floating circle or shockwave lines.
Art style: strong crisp 16-bit PIXEL ART, chunky square pixel clusters, limited warm palette, sculpted bright core/orange body/dark rust shadows, not line art, not smooth painting. Native64x64 sprite appearance enlarged with nearest-neighbor. A game-ready coherent animation with transparent gaps and continuous shapes. Original artwork.
```
