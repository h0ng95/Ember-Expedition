# 서리 파수꾼 · 부유하는 얼음 갑주

2026-10-25. 긴 철치마 디자인이 길쭉하고 이상해졌다는 사용자 피드백에 따라 재제작했다.

- 모드: built-in `image_gen`, `stylized-concept`, 투명 배경.
- 참고: 사용자가 선택했던 `frost-boss-walk.png`의 회색 철갑·남색 천·푸른 결정.
- 새 원본: `unity/Assets/Resources/Art/Enemies/v039/frost-boss-floating.png`.
- 실루엣: 넓은 어깨와 주먹, 짧은 몸통, 하체를 대신하는 얼음 핵과 부유하는 장갑 조각. 다리·긴 치마 없음.
- 적용: 높이 290, 원본 종횡비 유지. 5전투 보스와 실루엣 면적을 비슷하게 맞췄으며 장식을 제외한 체감은 프리뷰와 기기에서 판단한다.
- 후속 작업: 사용자 피드백을 반영해 [전신 4프레임 공격](FROST-FULL-MOTION-PROMPTS.md)을 만들고 원화 재질에 맞춰 보정했다. 팔 분리 방식은 미채택이다.

## 생성 프롬프트

```text
Use case: stylized-concept.
Asset type: ONE transparent full-body pixel art boss sprite for a landscape medieval 2D auto-battler.
Input image: reference only for dark weathered GREY steel, muted brass edge trim, deep navy cloth, clear blue ice crystals and glowing icy cyan chest core. Redesign the silhouette substantially.
Primary request: redesign the frost guardian as a formidable COMPACT, BROAD floating armored ice construct facing LEFT in a three-quarter side view. Its torso is short, deep and barrel-shaped, its shoulders and two huge plated fists create a wide top-heavy silhouette, and its squat pointed closed helmet is nested between its shoulders. Chest core is bright but small. Approximately as WIDE as TALL (overall silhouette width 95 percent of height), not a slim upright human. Keep fists separated visually from the chest, left fist forward, right fist lower/back. Large deliberate armor plates and clean cluster shading read at 260px high.
Lower body ends just below the waist as a compact jagged icy armored keel, with two or three short separate floating ice/iron fragments underneath. No human legs, no feet, no long skirt, no robe, no gown, no tapering tall dress, no long ghost tail. Lower section including floating fragments is at most one quarter of overall height. A little torn navy cloth can hang behind the waist to the RIGHT, but stays short. Pale frosty cracks link armor to the lower frozen core. This is magically levitating low above the battlefield, not a humanoid trying to walk.
Style: detailed restrained medieval PIXEL ART, crisp visible pixel clusters and stepped edges, no smooth 3D render, no bloom halo. Original game character, no recognizable franchise logos or motifs. Muted charcoal steel is dominant, bright blue ice accents limited to shoulders, chest and lower core.
Composition: exactly ONE complete sprite centered with ample transparent margin, helmet/crystals/fists/fragments all in view. Transparent alpha background; no environment, no floor, no ground shadow, no painted fog background, no words, no lettering, no framing or grid. The reference sheet is NOT a layout to reproduce.
```
