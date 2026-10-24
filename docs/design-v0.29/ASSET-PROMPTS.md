# v0.29 서리 성당 아트 제작 기록

제작일: 2026-10-24. 내장 imagegen 사용. 외부 게임 캐릭터나 이미지 입력 없이 신규 생성했다. 아래 수리 단계에는 이번에 생성한 자체 시트만 입력했다. 실제 게임에는 생성된 PNG를 그대로 복사하며, 프레임 경계·발 기준점은 별도 메타데이터로 지정한다.

## frost-guard

```text
Use case: stylized-concept. Asset: original medieval dark fantasy pixel art game enemy animation sprite sheet. Generate a wide 1536x1024 transparent RGBA PNG. SIX equal columns and FOUR equal rows, exactly 24 isolated full-body frames of SAME creature. Every pose faces LEFT, feet at bottom of each cell, small 3-head-tall game proportions, crisp pixel clusters, dark contour, detailed restrained shading. Consistent size and identity. Wide completely transparent gutters between every cell, no outlines or labels on grid, no opaque backdrop, no cast shadows. Each cell inset 30px; bodies max 165px wide and 185px high within a 256 square cell; weapon may extend but never cross cell. Row1: idle breathing4 then flinch2. Row2: six distinct walking steps. Row3: attack anticipation2 then attack strike and followthrough4. Row4: dying/collapse4 then two special-charge poses. Actual frame-specific limb movement, not a repeated static image. No text, logos, copyrighted characters. Subject: frostbound cathedral infantry, short sturdy knight in weathered blue gray plate, sealed slit visor, icy blue eye, pale tabard, broad small shield on LEFT and iron sword pointed LEFT. Old stone-guardian human-shaped limbs, frosted edges. Swing sword in attack. Specialposes brace shield. Strong readable compact silhouette.
```

## frost-acolyte

```text
Use case: stylized-concept. Asset: original medieval dark fantasy pixel art game enemy animation sprite sheet. Generate a wide 1536x1024 transparent RGBA PNG. SIX equal columns and FOUR equal rows, exactly 24 isolated full-body frames of SAME creature. Every pose faces LEFT, feet at bottom of each cell, small 3-head-tall game proportions, crisp pixel clusters, dark contour, detailed restrained shading. Consistent size and identity. Wide completely transparent gutters between every cell, no outlines or labels on grid, no opaque backdrop, no cast shadows. Each cell inset 30px; bodies max 165px wide and 185px high within a 256 square cell; weapon may extend but never cross cell. Row1: idle breathing4 then flinch2. Row2: six distinct walking steps. Row3: attack anticipation2 then attack strike and followthrough4. Row4: dying/collapse4 then two special-charge poses. Actual frame-specific limb movement, not a repeated static image. No text, logos, copyrighted characters. Subject: slender frostbound cathedral acolyte caster, angular navy hood concealing face except cyan eyes, pale icyblue long robes with short legs visible, silver bracers, carrying small crystalline wand toward LEFT. Hands thrust LEFT to launch tiny frostbolt on attack. No trails outsidecell. Specialposes lift a small frostcrystal. Clear original readable silhouette.
```

## frost-cantor

```text
Use case: stylized-concept. Asset: original medieval dark fantasy pixel art game enemy animation sprite sheet. Generate a wide 1536x1024 transparent RGBA PNG. SIX equal columns and FOUR equal rows, exactly 24 isolated full-body frames of SAME creature. Every pose faces LEFT, feet at bottom of each cell, small 3-head-tall game proportions, crisp pixel clusters, dark contour, detailed restrained shading. Consistent size and identity. Wide completely transparent gutters between every cell, no outlines or labels on grid, no opaque backdrop, no cast shadows. Each cell inset 30px; bodies max 165px wide and 185px high within a 256 square cell; weapon may extend but never cross cell. Row1: idle breathing4 then flinch2. Row2: six distinct walking steps. Row3: attack anticipation2 then attack strike and followthrough4. Row4: dying/collapse4 then two special-charge poses. Actual frame-specific limb movement, not a repeated static image. No text, logos, copyrighted characters. Subject: frost cathedral cantor elite, broad shoulders with white carved stone shoulderarmor, silver tall narrow closed helmet, dark teal tabard, one hand swinging a small blue glowing bell censer on short chain toward LEFT, other carrying compact staff. Large feet. Attack leftward censer swing. Lasttwo specialposes raise glowing censer and cast blue protective blessing. Strong original design.
```

## frost-boss

```text
Use case: stylized-concept. Asset: original medieval dark fantasy pixel art game enemy animation sprite sheet. Generate a wide 1536x1024 transparent RGBA PNG. SIX equal columns and FOUR equal rows, exactly 24 isolated full-body frames of SAME creature. Every pose faces LEFT, feet at bottom of each cell, small 3-head-tall game proportions, crisp pixel clusters, dark contour, detailed restrained shading. Consistent size and identity. Wide completely transparent gutters between every cell, no outlines or labels on grid, no opaque backdrop, no cast shadows. Each cell inset 30px; bodies max 165px wide and 185px high within a 256 square cell; weapon may extend but never cross cell. Row1: idle breathing4 then flinch2. Row2: six distinct walking steps. Row3: attack anticipation2 then attack strike and followthrough4. Row4: dying/collapse4 then two special-charge poses. Actual frame-specific limb movement, not a repeated static image. No text, logos, copyrighted characters. Subject: massive cathedral bell guardian boss. A walking ancient silver-blue stone and bronze bell tower construct, enormous forearms, heavy squat legs, upper torso shaped as cracked cathedral bell housing, glowing frozen blue crystal heart. Face a visor facing LEFT, dominantleft shoulder and leftfacing stance. Ancient ice-encrusted angulararmor, no fire. Attack swings mighty LEFT fist. Last2 chargeposes brace arms, open bell housing to reveal small bright icycore. Fullbody no cropping, same body height as other sheetcells; game will upscale it.
```

## frost-battlefield

```text
Use case: stylized-concept. Original crisp detailed pixel art medieval fantasy side-view autobattler background, wide1536x1024. Ruined frost cathedral nave, blue gray stone gothic arches, cracked stained glass windows, hanging bronze bells, ice creeping up columns, distant snowy dusk seen through ruinedwall, faintsmallwarmambercandles. Sideview slightly elevated battlefield composition: architecture and scenery ONLY upperhalf, continuous flat wide empty stonefloor lowerhalf with depth, natural flagstones sparsefinefrost, broad unobstructed horizontal staging space. NO characters enemies creatures userinterface text numbers logos. No central pedestal, no obstacles in battlefloor. Landscape game environment restrained atmospheric fantasy pixelclusters. Match detailed pixelgame art with dark outlines and mutedbluepalette, clear lighterfloorforunitreadability.
```

## 두 시트 간격 수리

첫 guard/cantor 시트는 공격 프레임 사이에 투명한 간격이 없어 프레임 검사에 실패했다. 아래 지시로 imagegen 수정본을 생성했다. 초기 원본은 생성 폴더에 남아 있으며 게임에서는 수리본을 사용한다.

```text
Edit this ORIGINAL game sprite sheet. Preserve SAME character identity, LEFT-facing orientation and the 6 columns x 4 rows =24 frame sequence with distinct idle hit walk attack death special poses. REPAIR THE SPACING: each sprite including sword, staff, censer, wisps and effects MUST fit entirely inside the central 170x190 pixels of its own 256x256 cell. Make all characters uniformly about 20 percent smaller to leave HUGE gutters. Every 256-pixel vertical and horizontal grid boundary needs at least 35 pixels of completely alpha-zero transparent space on BOTH SIDES for all 4 rows, especially THIRD ROW attack poses. Shorten weapon thrust and remove all large flying arc effects that cross a cell. No pixel can connect neighboring frames. Finalcanvas1536x1024 transparent PNG. Character bodies retain consistent scale acrossposes. Fullbody. No labels gridlines text or backgrounds. Maintain crisp detailed pixel clusters and existing colors. This is sprite grid technical repair, no changes to character design.
```

