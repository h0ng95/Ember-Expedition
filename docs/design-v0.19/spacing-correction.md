# 정예·보스 프레임 간격 보정

첫 생성 결과에서 정예·보스의 공격·쓰러짐 일부 프레임 사이에 불투명 픽셀이 남아, 직사각형 프레임 구획이 이웃 그림을 자를 수 있었다. 내장 imagegen으로 각 시트의 간격을 넓히는 수정을 1회씩 수행했다. 원본은 생성 경로에 보존하고, 선택한 결과를 프로젝트의 해당 PNG에 복사했다. 뿔짐승·가시등은 첫 결과를 사용한다.

- 정예 최종 원본: `C:/Users/whwog/.codex/generated_images/01a14e3b-5142-7871-8565-822e92917bb8/exec-00361247-029f-440a-9b79-8f05f9e0b523.png`
- 보스 최종 원본: `C:/Users/whwog/.codex/generated_images/01a14e3b-5142-7871-8565-822e92917bb8/exec-3f088de7-b0b8-413a-a04e-5c691264ffe9.png`
- 입력: 각 캐릭터의 첫 애니메이션 시트. 도구: `image_gen.imagegen`, `transparent_background: true`.

## 공통 수정 프롬프트

Use case: precise-object-edit.
Edit target: the supplied 24-frame pixel-animation sheet.
Make ONE targeted technical correction: increase transparent spacing between the poses. Preserve the character identity, the LEFT-facing orientation, the pixel style, the exact 24 animation poses and their order. Preserve all horns, tails and claws. Do not add or redesign a creature or change any pose sequence.
Output a LANDSCAPE 1536x1024 TRANSPARENT PNG arranged as EXACTLY 6 equal columns by 4 equal rows, exactly 24 full-body sprites. Each cell is 256x256. REDUCE EVERY CHARACTER'S SIZE TO ABOUT 65 PERCENT OF ITS CURRENT SIZE, using the same consistent reduction for all 24 frames, so that even the widest attack and fallen-death silhouette fits within the central 60 percent width of its own cell. Maximum complete sprite width 154 pixels, maximum complete sprite height 154 pixels. Center the body in each cell at x=128. Align standing feet and death floor at local y=210. Leave approximately 50 pixels of absolutely empty transparent space on each LEFT and RIGHT side of each sprite. Leave at least 50 pixels transparent between every neighboring pose. This generous space matters more than using the whole image.
Grid locations: six cell centers x=128,384,640,896,1152,1408; four row foot baselines y=210,466,722,978. ALL 24 complete poses must fit in their own cell, with no pixel crossing cell boundaries. Keep the 6-column4-row arrangement, never fewer rows or columns. No cell lines, no text or numbering.
Row1 idle4/hit2, row2 walk6, row3 attack6, row4 death4/charge-ready2, SAME as input.
REAL alpha transparency with zero-alpha background. Remove any background haze, color field or shadows; transparent areas should be wholly empty. Crisp coarse pixel art, no new textures, no labels. This is a technical spacing correction to make the atlas importable.
