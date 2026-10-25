# 서리 파수꾼 · 전신 4프레임 공격

2026-10-25 · built-in `image_gen`, 투명 배경. 사용자가 팔 분리 방식을 원하지 않아, 각 프레임에 팔이 붙어 있는 완성된 캐릭터 전체를 그렸다. 이후 원화보다 재질이 밝고 매끈해졌다는 피드백을 받아 원화를 참고해 재질·색감을 보정했다.

- 적용 파일: `unity/Assets/Resources/Art/Enemies/v039/frost-boss-full-motion.png`.
- 원화 참고: `unity/Assets/Resources/Art/Enemies/v039/frost-boss-floating.png`.
- 재생: 대기 → 준비 → 타격 → 복귀. 팔 분리·회전·이동 처리 없음. 전신 이미지 한 장씩 교체한다.
- 일반 공격과 서리 공격은 같은 전신 공격 프레임을 사용한다. 서리 공격의 긴 예고 시간과 얼음 효과는 기존 스킬 이벤트를 따른다.
- 네 자세는 같은 스케일로 그린다. 숙인 자세를 대기와 같은 높이로 늘리지 않는다. 하단 부유 기준점을 맞추고 원본 종횡비를 유지한다.
- 재질: 회색 철갑·낡은 황동 테두리·남색 천·푸른 결정. 생성 과정의 재질 차이를 줄인 보정이며 원화와 픽셀 단위로 동일한 텍스처를 복사한 것은 아니다.

## 전신 모션 생성 프롬프트

```text
Use case: identity-preserve.
Asset type: full-character PIXEL ART ATTACK ANIMATION SPRITE SHEET, exactly FOUR complete full-body sprites in a clean 2 by 2 grid. Transparent alpha, no background, no text, no labels, no borders. Each grid cell is the same size, with a generous safety margin. Do NOT split any body parts.
Input is the exact approved full character. Keep its compact broad weathered grey armored torso, muted brass edge trims, squat cathedral helmet, huge crystal shoulders, huge gauntlets, bright small cyan chest core, short navy trailing cloth, floating icy keel and three small bottom fragments. NO legs or long skirt. The guardian faces LEFT in three-quarter view in ALL FOUR frames.
CRITICAL: Both arms are visibly physically CONNECTED to the torso at the shoulders through solid continuous armor. NO detached gauntlets, floating fists, dismembered arms, separate parts, detached weapon, motion trails or smeared limbs. This is not a cutout rig or exploded diagram. Every frame is a finished coherent creature drawing.
Animation frames, row-major:
TOP LEFT / frame 1: neutral hovering idle, both fists held low beside torso, matching the reference.
TOP RIGHT / frame 2: anticipation. Whole heavy torso rocks back slightly to the RIGHT, shoulders twist, the near left-facing fist pulls up close to the chest. Elbow bends naturally and remains visibly attached; rear arm braces. Helmet and chest turn as one solid body. No arm across face.
BOTTOM LEFT / frame 3: impact. Whole body leans forward toward the LEFT and the near fist drives left and slightly downward toward an opponent at chest/waist level. CLEARLY DIFFERENT WHOLE-BODY SILHOUETTE. Shoulder follows the punch, upper arm and elbow clearly connect to the fist. Arm extends at a believable length, not a detached rocket punch. Rear arm drawn back, ice keel and navy cloth trail to the RIGHT. Keep punching fist well inside the cell.
BOTTOM RIGHT / frame 4: recoil/recovery. Whole torso rises back toward the idle position, elbow bends to draw punching arm back, both arms remain continuously attached. Chest armor/core/helmet/ice shards retain exactly the same design and relative size.
Same camera angle, lighting, pixel scale, helmet/torso size and hovering baseline in all cells. Keep every full figure visible including fists and bottom ice fragments. Torso should be at a consistent horizontal center within each cell so attack space lies on its LEFT. The impact may be wider than idle but must not be scaled down. Preserve original crisp detailed pixel cluster shading and original muted grey/navy/ice-blue palette. Avoid smooth vector or plastic 3D rendering, bright yellow gold, additional spikes, effects, floor or shadow. Four complete sprites, one per cell.
```

## 재질 보정 프롬프트

```text
Use case: lighting-weather / precise-object-edit.
INPUT 1 IS THE EDIT TARGET: the four-frame full-character attack sheet.
INPUT 2 IS THE MATERIAL AND COLOR REFERENCE: the original single floating armored frost guardian approved before animation.
Keep EXACTLY the four full-body poses, layout, silhouette, continuous attached arms, helmet/core/cloth/ice fragment positions, and transparent background from input 1. Do not redesign, rearrange, split, remove, resize or add limbs. Change ONLY the armor surface rendering, color and material to match input 2 faithfully in ALL FOUR frames.
The user says input 1's body material changed. Correct this drift: restore DARK CHARCOAL-GREY weathered IRON plate with rough mottled hammered metal and tiny irregular chipped/scuffed grey pixels; a restrained MATTE finish with localized cool pale-grey edge wear. Broad armor surfaces must be the darker steel of input 2, not bright smooth silver, shiny plastic or gilded plate. Reduce the saturation and brightness of yellow/golden trim; make it MUTED OLD BRASS narrow edging as in input 2. Brass must not dominate torso or shoulders. Preserve the original deep NAVY blue cloth, cold clear blue crystals, and small bright icy cyan chest core from input 2. No warm overall color cast. Keep sharp detailed pixel clusters, dense grain and weathering from input 2, not smooth gradients or simplified glossy cartoon material.
Very important: This is a surface/material/color matching edit, NOT another character design. Use input 2's grey armor texture and restrained metal palette, painted onto the existing four poses of input 1. Maintain transparent alpha; no background, no ground, no shadow, no text, no effects or trails. No separate arm parts. Each frame remains a complete attached full-body character.
```
