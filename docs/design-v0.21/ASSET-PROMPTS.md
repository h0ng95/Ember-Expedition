# 성채 적 시트 제작 프롬프트 · v0.21

2026-10-23 · 내장 imagegen 사용. 외부 게임의 캐릭터 이미지를 사용하지 않았다.

## 원본 시안

저장 위치: `assets/castle-enemy-concepts-v1.png`.

```text
Use case: stylized-concept.
Asset type: original enemy character concept sheet for Ember Expedition, a side-view medieval pixel-art mobile autobattler.
Create FOUR distinct ORIGINAL ruined-castle stone construct enemies together in a clean 2 by 2 sheet on genuinely transparent background. Each quadrant contains ONE complete full-body character, all looking and facing LEFT in a readable near-side three-quarter game view, feet visible, with very generous transparent gaps between all four. No labels, no text, no scenery, no floor, no shadow puddle. Keep each figure completely within its own quadrant.
Shared art direction: charming but dangerous compact fantasy pixel sprites, bold dark 2–3-pixel outlines, deliberate stepped pixel clusters, crisp silhouettes, selective 3-tone shading, no smooth vector edges, no photorealism. Cool slate stone, dusty ivory chipped edges, rusty iron, muted oxblood cloth, small warm amber rune slits. Faces are nonhuman angular stone/iron masks, never skulls or human faces. No blue/gold heroic uniforms. Same family of old castle masonry, each role clearly different at thumbnail size.
TOP LEFT: small stone infantry, squat chipped stone torso and short legs, chipped iron open-face helmet with one horizontal amber eye slit, small rough round stone buckler on leading arm, short broad chipped stone cleaver pointed LEFT. Plain silhouette, tiny cloth scrap, no crest, no excessive shoulder armor.
TOP RIGHT: slim stone crossbow sentry, clearly thinner than infantry, narrow angular hoodlike stone head, long rusty crank crossbow aimed LEFT and held with both hands, small masonry limbs and slightly hunched aiming posture. Crossbow is the defining silhouette, no shield.
BOTTOM LEFT: elite armored stone statue, broader and visibly more imposing than infantry, heavy stepped shoulders, low iron mask with amber eye slit, large rectangular crenellated tower shield held forward LEFT, sturdy square legs, short stone mace in rear hand. Distinct shield-charge silhouette, not a smaller version of boss.
BOTTOM RIGHT: enormous gate golem, squat wide fortress body with a recessed pointed arch gate and iron portcullis chest, battlement crown, tiny recessed amber eyes beneath the crown, two huge square stone forearms and fists, block feet. Thick asymmetrical broken masonry and one hanging iron hinge. Its forearms must support a dramatic overhead ground slam. Make it broad and monumental, not skinny and not humanoid knight armor.
All designs must be newly invented from these descriptions. No existing franchise characters, emblems, recognizable armor sets or borrowed game artwork. Preserve strong visual distinction across melee/ranged/elite/boss. Deliver one square concept sheet with alpha transparency.
```

## 생성 원본과 채택 파일

원본 폴더: `C:/Users/whwog/.codex/generated_images/01a14e3b-5142-7871-8565-822e92917bb8/`. 해당 원본을 유지하고 PNG 바이트를 그대로 복사했다.

| 용도 | 생성 원본 파일 | 채택 파일 |
| --- | --- | --- |
| 4종 시안 | `exec-681469c4-13cd-4907-9d77-4cbbb8ac75b4.png` | `assets/castle-enemy-concepts-v1.png` |
| 석병 | `exec-7ea36aed-3856-4ab7-b4ad-a3ec961761cc.png` | `unity/Assets/Resources/Art/Enemies/v021/castle-infantry.png` |
| 석궁병 | `exec-4535d704-1e53-449c-af04-e7e8f460f370.png` | `unity/Assets/Resources/Art/Enemies/v021/castle-crossbow.png` |
| 방패 정예 | `exec-94eced69-408b-45e1-9cec-ae26e43e88ca.png` | `unity/Assets/Resources/Art/Enemies/v021/castle-elite.png` |
| 성문 골렘 | `exec-9ebdbb7a-bad3-4038-aa9e-6eccf76f70a5.png` | `unity/Assets/Resources/Art/Enemies/v021/castle-boss.png` |

시안 경로는 이 문서 기준, Unity 경로는 저장소 루트 기준이다. 사전 검사에서 4장 모두 1536 × 1024 RGBA이며, 알파값 102/255 초과를 기준으로 96프레임의 불투명 영역과 빈 간격을 확인했다.

## 동작 시트

공통 참조: 위 원본 시안. 네 종류를 각각 독립 PNG로 생성한다. 출력 픽셀은 수정하지 않고 Unity에서 투명 간격과 프레임 범위만 읽는다.

### castle-infantry

```text
Use case: pixel game sprite sheet, precise reference-based pose animation.
Use only the specified ONE character from the supplied original concept sheet. Preserve its original appearance, stone/rust/red-cloth colors and gear. Other three characters must not appear.
Create a LANDSCAPE 1536x1024 TRANSPARENT PNG, EXACTLY SIX EQUAL COLUMNS and FOUR EQUAL ROWS. Exactly 24 distinct full-body animation frames in reading order left to right then top to bottom. Invisible cells 256x256, no drawn grid, labels, lettering, floor or background.
CRITICAL SPACING: Make every complete sprite including weapons and death poses fit inside the central 60 percent width and height of its own cell (maximum154px in either dimension). The sprite must be SMALL inside the cell. Center torso at local x128, standing feet/death floor at local y210. At least50px empty transparent space on each side. Leave clear completely empty horizontal and vertical gutters between all neighboring frames. Never allow weapon tips to enter neighboring cell. Same body scale for all24frames; no automatic enlargement of crouching or death frames.
Style: crisp low-resolution pixel art with deliberate stepped clusters, dark outlines and 3-tone shading, suitable for tiny mobile characters. All poses face LEFT, nose/eye direction pointing LEFT, near-side three-quarter left orientation. Never flip to right or directly face camera. No shadows below feet, no effects spilling outside cell.
FRAME CONTRACT: row1 frames0–3 subtle idle breathing4; row1frames4–5 two hit-recoil poses. row2frames6–11 six walking cycle poses with visibly changing feet and equipment bounce. row3frames12–17 six attack poses: first two clear anticipation, remaining four decisive strike and recover. row4frames18–21 four death poses gradually collapse to floor rubble. row4frames22–23 two special warning poses specified below. Exactly one complete figure in each cell, even final death. No duplicates of the whole sheet.
ONLY TOP LEFT concept: squat stone infantry with short chipped cleaver and round stone buckler. Row3 attack: pull cleaver back then a short powerful horizontal cleaver slash to LEFT, recover. Final2 warning poses: brace shield forward, lean into shield shove. Keep modest small basic-enemy silhouette.
```

### castle-crossbow

```text
Use case: pixel game sprite sheet, precise reference-based pose animation.
Use only the specified ONE character from the supplied original concept sheet. Preserve its original appearance, stone/rust/red-cloth colors and gear. Other three characters must not appear.
Create a LANDSCAPE 1536x1024 TRANSPARENT PNG, EXACTLY SIX EQUAL COLUMNS and FOUR EQUAL ROWS. Exactly 24 distinct full-body animation frames in reading order left to right then top to bottom. Invisible cells 256x256, no drawn grid, labels, lettering, floor or background.
CRITICAL SPACING: Make every complete sprite including weapons and death poses fit inside the central 60 percent width and height of its own cell (maximum154px in either dimension). The sprite must be SMALL inside the cell. Center torso at local x128, standing feet/death floor at local y210. At least50px empty transparent space on each side. Leave clear completely empty horizontal and vertical gutters between all neighboring frames. Never allow weapon tips to enter neighboring cell. Same body scale for all24frames; no automatic enlargement of crouching or death frames.
Style: crisp low-resolution pixel art with deliberate stepped clusters, dark outlines and 3-tone shading, suitable for tiny mobile characters. All poses face LEFT, nose/eye direction pointing LEFT, near-side three-quarter left orientation. Never flip to right or directly face camera. No shadows below feet, no effects spilling outside cell.
FRAME CONTRACT: row1 frames0–3 subtle idle breathing4; row1frames4–5 two hit-recoil poses. row2frames6–11 six walking cycle poses with visibly changing feet and equipment bounce. row3frames12–17 six attack poses: first two clear anticipation, remaining four decisive strike and recover. row4frames18–21 four death poses gradually collapse to floor rubble. row4frames22–23 two special warning poses specified below. Exactly one complete figure in each cell, even final death. No duplicates of the whole sheet.
ONLY TOP RIGHT concept: slim stone crossbow sentry with long rusty crossbow and red cloth, narrow stone head. Row3 attack: aim crossbow LEFT, squeeze trigger with short recoil, lower and crank reload, recover. Do not throw or turn crossbow. Final2 warning poses: steady long aimed shot, brighter small amber eye. Keep thin readable ranged-enemy silhouette.
```

### castle-elite

```text
Use case: pixel game sprite sheet, precise reference-based pose animation.
Use only the specified ONE character from the supplied original concept sheet. Preserve its original appearance, stone/rust/red-cloth colors and gear. Other three characters must not appear.
Create a LANDSCAPE 1536x1024 TRANSPARENT PNG, EXACTLY SIX EQUAL COLUMNS and FOUR EQUAL ROWS. Exactly 24 distinct full-body animation frames in reading order left to right then top to bottom. Invisible cells 256x256, no drawn grid, labels, lettering, floor or background.
CRITICAL SPACING: Make every complete sprite including weapons and death poses fit inside the central 60 percent width and height of its own cell (maximum154px in either dimension). The sprite must be SMALL inside the cell. Center torso at local x128, standing feet/death floor at local y210. At least50px empty transparent space on each side. Leave clear completely empty horizontal and vertical gutters between all neighboring frames. Never allow weapon tips to enter neighboring cell. Same body scale for all24frames; no automatic enlargement of crouching or death frames.
Style: crisp low-resolution pixel art with deliberate stepped clusters, dark outlines and 3-tone shading, suitable for tiny mobile characters. All poses face LEFT, nose/eye direction pointing LEFT, near-side three-quarter left orientation. Never flip to right or directly face camera. No shadows below feet, no effects spilling outside cell.
FRAME CONTRACT: row1 frames0–3 subtle idle breathing4; row1frames4–5 two hit-recoil poses. row2frames6–11 six walking cycle poses with visibly changing feet and equipment bounce. row3frames12–17 six attack poses: first two clear anticipation, remaining four decisive strike and recover. row4frames18–21 four death poses gradually collapse to floor rubble. row4frames22–23 two special warning poses specified below. Exactly one complete figure in each cell, even final death. No duplicates of the whole sheet.
ONLY BOTTOM LEFT concept: broad armored stone statue with heavy rectangular crenellated tower shield, red cloth and short stone mace. Row3 attack: wind up mace then strike LEFT and recover. Final2 warning poses: drop center of mass and firmly raise tower shield for a charge LEFT, second lean farther with shield leading. All shield edges remain inside cell. No crest logo or cross emblem, use plain red cloth instead.
```

### castle-boss

```text
Use case: pixel game sprite sheet, precise reference-based pose animation.
Use only the specified ONE character from the supplied original concept sheet. Preserve its original appearance, stone/rust/red-cloth colors and gear. Other three characters must not appear.
Create a LANDSCAPE 1536x1024 TRANSPARENT PNG, EXACTLY SIX EQUAL COLUMNS and FOUR EQUAL ROWS. Exactly 24 distinct full-body animation frames in reading order left to right then top to bottom. Invisible cells 256x256, no drawn grid, labels, lettering, floor or background.
CRITICAL SPACING: Make every complete sprite including weapons and death poses fit inside the central 60 percent width and height of its own cell (maximum154px in either dimension). The sprite must be SMALL inside the cell. Center torso at local x128, standing feet/death floor at local y210. At least50px empty transparent space on each side. Leave clear completely empty horizontal and vertical gutters between all neighboring frames. Never allow weapon tips to enter neighboring cell. Same body scale for all24frames; no automatic enlargement of crouching or death frames.
Style: crisp low-resolution pixel art with deliberate stepped clusters, dark outlines and 3-tone shading, suitable for tiny mobile characters. All poses face LEFT, nose/eye direction pointing LEFT, near-side three-quarter left orientation. Never flip to right or directly face camera. No shadows below feet, no effects spilling outside cell.
FRAME CONTRACT: row1 frames0–3 subtle idle breathing4; row1frames4–5 two hit-recoil poses. row2frames6–11 six walking cycle poses with visibly changing feet and equipment bounce. row3frames12–17 six attack poses: first two clear anticipation, remaining four decisive strike and recover. row4frames18–21 four death poses gradually collapse to floor rubble. row4frames22–23 two special warning poses specified below. Exactly one complete figure in each cell, even final death. No duplicates of the whole sheet.
ONLY BOTTOM RIGHT concept: monumental squat wide gate golem, battlement crown and recessed pointed-arch portcullis chest, immense stone fists. Rotate body to clearly face THREE-QUARTER LEFT, eyes and front arch directed slightly LEFT throughout. This is a boss but sheet figure still obeys small central60percent size/gutters; runtime enlarges it later. Row3 attack: two windups lift both forearms overhead, next two slam both fists into ground to LEFT, next two recover. Final2 warning poses: hunched anticipation with fists rising, then both fists raised overhead fully braced for ground slam, amber gate glow slightly stronger. Hit2 poses barely move, no wild recoil. No emitted particles crossing gutters.
```
