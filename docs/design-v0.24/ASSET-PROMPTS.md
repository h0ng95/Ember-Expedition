# v0.24 폐광·용광로 아트 제작 기록

내장 imagegen 사용. 신규 원본 PNG를 생성하고 픽셀은 수정하지 않았다. 좌표·투명 영역만 분석해 런타임 manifest를 만들었다. 외부 게임의 캐릭터나 참고 이미지는 입력하지 않았다.

## 산출물

- 생성일: 2026-10-24. 5장 모두 1536×1024 PNG.
- 적 시트: `unity/Assets/Resources/Art/Enemies/v024/`에 4종, 각 24프레임. 실제 투명 알파를 사용한다.
- 배경: `unity/Assets/Resources/Art/Environments/v024/furnace-battlefield.png`.
- 원본 생성 파일명·복사 경로·SHA-256은 [asset-records.json](asset-records.json)에 기록했다. 복사본과 원본 5장의 바이트 일치를 확인했다.
- `mine-manifest.json`은 Unity에서 투명 구간을 찾아 프레임 좌표를 추출한 메타데이터다. 원본 PNG를 잘라 저장하거나 다시 그리지 않았다.
- 표시 높이는 채굴병 90, 투척병 100, 감독관 145, 보스 244 기준 픽셀이다. 무기·보호막 효과가 뻗는 프레임도 동일한 배율을 유지한다.
- 대기·걷기·공격·피격·쓰러짐을 연결했다. 감독관의 마지막 2프레임은 보호막 시전, 보스의 마지막 2프레임은 폭발 예고에 쓴다. 보스 피격 포즈는 재생하지 않는다.

다음은 실제 생성에 사용한 프롬프트다.

## mine-worker

```text
Use case: stylized-concept. Asset type: original medieval fantasy mobile game enemy animation sprite sheet.
Create ONE landscape 1536x1024 PNG with genuine transparent alpha background. EXACTLY SIX COLUMNS and FOUR ROWS: 24 separate poses of ONE consistent character, all facing LEFT in near-side three-quarter game view. No camera-facing or right-facing poses. Each invisible cell is 256x256. Keep the COMPLETE sprite SMALL, inside the central 60% of each cell, including weapons and death poses. Center body near local x128 and place standing soles or fallen body floor near local y210. At least 45px clear transparent gutters between each sprite and cell edge. Same body scale in every pose. No grid, labels, lettering, background, floor, haze, cast shadow or watermark.
Style: charming but dangerous compact pixel-art enemy, large readable shapes, deliberate stepped pixel clusters, bold dark outline, 3-tone shading, restrained palette of soot charcoal, weathered copper, dark brown leather and small amber embers. This is a medieval abandoned mine/foundry, no modern mining helmet or electronics. Original invented design, no existing franchise characters, insignias or borrowed game artwork. Each frame must be an animation pose, not a separate character.
FRAME CONTRACT in reading order: row1 frames0–3 four idle breathing poses; row1 frames4–5 two received-hit recoil poses. Row2 frames6–11 six visibly changing walk-cycle poses. Row3 frames12–17 six attack poses: first2 anticipation, next4 impact and recovery. Row4 frames18–21 four collapse/death poses ending as a small broken heap within the cell; final row4 frames22–23 two distinct special preparation poses. No pose may touch a neighboring cell.
CHARACTER: a small squat ash-clay mining automaton, short legs and broad rounded shoulders, simple cracked pale clay faceplate with two tiny amber eye holes looking left, no horns or skull. Brown leather hood and short work apron, thin copper bracers, one short rusty pickaxe held in both hands. Dark slag joints. Distinct small melee minion, plain silhouette. WALK carries pickaxe low. ATTACK raises pickaxe behind its head then brings it down toward LEFT in a short forceful mining blow. DEATH clay body crumbles. Final2 preparation poses brace and grip the pickaxe. No shield and no crown.
```

## mine-slinger

```text
Use case: stylized-concept. Asset type: original medieval fantasy mobile game enemy animation sprite sheet.
Create ONE landscape 1536x1024 PNG with genuine transparent alpha background. EXACTLY SIX COLUMNS and FOUR ROWS: 24 separate poses of ONE consistent character, all facing LEFT in near-side three-quarter game view. No camera-facing or right-facing poses. Each invisible cell is 256x256. Keep the COMPLETE sprite SMALL, inside the central 60% of each cell, including weapons and death poses. Center body near local x128 and place standing soles or fallen body floor near local y210. At least 45px clear transparent gutters between each sprite and cell edge. Same body scale in every pose. No grid, labels, lettering, background, floor, haze, cast shadow or watermark.
Style: charming but dangerous compact pixel-art enemy, large readable shapes, deliberate stepped pixel clusters, bold dark outline, 3-tone shading, restrained palette of soot charcoal, weathered copper, dark brown leather and small amber embers. This is a medieval abandoned mine/foundry, no modern mining helmet or electronics. Original invented design, no existing franchise characters, insignias or borrowed game artwork. Each frame must be an animation pose, not a separate character.
FRAME CONTRACT in reading order: row1 frames0–3 four idle breathing poses; row1 frames4–5 two received-hit recoil poses. Row2 frames6–11 six visibly changing walk-cycle poses. Row3 frames12–17 six attack poses: first2 anticipation, next4 impact and recovery. Row4 frames18–21 four collapse/death poses ending as a small broken heap within the cell; final row4 frames22–23 two distinct special preparation poses. No pose may touch a neighboring cell.
CHARACTER: a lean little soot-clay ember thrower, much slimmer than a worker, hooded angular clay face with a single horizontal amber eye notch looking left, ragged dark brown apron. Carries a small riveted copper coal bucket at rear hip, and swings a short handheld leather sling with one glowing ember stone. Two thin bent legs, copper elbow bands. Clear ranged silhouette through the raised sling and bucket. WALK bucket bobs. ATTACK first loads one coal stone from bucket, winds sling above shoulder, releases toward LEFT, recovers. Keep ember effect tiny inside its cell and no separate flying projectiles outside cell. DEATH spills only a few coals beside collapsed body within cell. Final2 poses lift sling and kindle ember. Not a bow/crossbow or gun.
```

## mine-elite

```text
Use case: stylized-concept. Asset type: original medieval fantasy mobile game enemy animation sprite sheet.
Create ONE landscape 1536x1024 PNG with genuine transparent alpha background. EXACTLY SIX COLUMNS and FOUR ROWS: 24 separate poses of ONE consistent character, all facing LEFT in near-side three-quarter game view. No camera-facing or right-facing poses. Each invisible cell is 256x256. Keep the COMPLETE sprite SMALL, inside the central 60% of each cell, including weapons and death poses. Center body near local x128 and place standing soles or fallen body floor near local y210. At least 45px clear transparent gutters between each sprite and cell edge. Same body scale in every pose. No grid, labels, lettering, background, floor, haze, cast shadow or watermark.
Style: charming but dangerous compact pixel-art enemy, large readable shapes, deliberate stepped pixel clusters, bold dark outline, 3-tone shading, restrained palette of soot charcoal, weathered copper, dark brown leather and small amber embers. This is a medieval abandoned mine/foundry, no modern mining helmet or electronics. Original invented design, no existing franchise characters, insignias or borrowed game artwork. Each frame must be an animation pose, not a separate character.
FRAME CONTRACT in reading order: row1 frames0–3 four idle breathing poses; row1 frames4–5 two received-hit recoil poses. Row2 frames6–11 six visibly changing walk-cycle poses. Row3 frames12–17 six attack poses: first2 anticipation, next4 impact and recovery. Row4 frames18–21 four collapse/death poses ending as a small broken heap within the cell; final row4 frames22–23 two distinct special preparation poses. No pose may touch a neighboring cell.
CHARACTER: an imposing iron-chain mine overseer, broad upright body, squat powerful legs, battered dark iron mask with two narrow amber slits facing LEFT, asymmetrical copper shoulder plate, thick brown leather apron, short iron chain wrapped around forearm, heavy short-handled forge hammer. Other hand carries a small hexagonal copper ward lantern with pale turquoise light, the defining SUPPORT device. Larger sturdier silhouette than ordinary miners, no cape and no crown. ATTACK swings short hammer hard toward LEFT. WALK shifts heavy weight. DEATH drops hammer and broken lantern beside a slumped body. Final2 SPECIAL poses raise the ward lantern high at chest/head level, visibly glowing turquoise and projecting a small contained geometric shield glow around its own arm; all inside the cell.
```

## furnace-boss

```text
Use case: stylized-concept. Asset type: original medieval fantasy mobile game enemy animation sprite sheet.
Create ONE landscape 1536x1024 PNG with genuine transparent alpha background. EXACTLY SIX COLUMNS and FOUR ROWS: 24 separate poses of ONE consistent character, all facing LEFT in near-side three-quarter game view. No camera-facing or right-facing poses. Each invisible cell is 256x256. Keep the COMPLETE sprite SMALL, inside the central 60% of each cell, including weapons and death poses. Center body near local x128 and place standing soles or fallen body floor near local y210. At least 45px clear transparent gutters between each sprite and cell edge. Same body scale in every pose. No grid, labels, lettering, background, floor, haze, cast shadow or watermark.
Style: charming but dangerous compact pixel-art enemy, large readable shapes, deliberate stepped pixel clusters, bold dark outline, 3-tone shading, restrained palette of soot charcoal, weathered copper, dark brown leather and small amber embers. This is a medieval abandoned mine/foundry, no modern mining helmet or electronics. Original invented design, no existing franchise characters, insignias or borrowed game artwork. Each frame must be an animation pose, not a separate character.
FRAME CONTRACT in reading order: row1 frames0–3 four idle breathing poses; row1 frames4–5 two received-hit recoil poses. Row2 frames6–11 six visibly changing walk-cycle poses. Row3 frames12–17 six attack poses: first2 anticipation, next4 impact and recovery. Row4 frames18–21 four collapse/death poses ending as a small broken heap within the cell; final row4 frames22–23 two distinct special preparation poses. No pose may touch a neighboring cell.
CHARACTER: a monumental squat walking iron furnace guardian, an original medieval foundry monster. Huge broad blackened riveted iron kiln body with a round copper-rimmed fire door on its LEFT-facing front, blazing orange furnace core behind the grille, a low chimney leaning backward to the right, one hinged brow plate above two tiny bright eyes pointing LEFT. Massive unequal bellows-like forearms and heavy iron block feet, thick copper pipes, no stone castle walls, no crown, no human face, no knight armor. Broad powerful industrial-medieval silhouette. Maintain SAME boss proportions and size in all cells; never grow larger merely to fill a cell. ATTACK first2 poses raise both huge fists and open fire vent, next4 poses slam fists down LEFT and close vent during recovery. WALK shifts massive body. DEATH cracks and cools into low broken kiln pile. Final2 SPECIAL poses: frame22 closes vents while orange core charges; frame23 opens left-front furnace grille wide with a compact contained orange flame bloom. No long plume escaping the cell.
```

## furnace-battlefield

```text
Use case: stylized-concept. Asset type: playable side-view battlefield background for a landscape medieval pixel-art mobile autobattler, original art.
Create a wide landscape 1536x1024 pixel art environment: an abandoned underground mine opening into an old copper-and-iron foundry. Match chunky deliberate pixel clusters, dark outlines and restrained 3-tone material shading, no smooth photorealistic 3D. Upper half: layered rock cavern, old timber supports, a distant warm furnace arch at RIGHT, hanging chains, subdued copper pipes, blue-gray mineral shadows and small amber lamps. Upper details recede into atmosphere. Lower approximately 45 percent must be an open continuous nearly level ash-gray packed-earth and flat stone combat floor, broad unobstructed space across the entire width, weak sparse horizontal cracks, very low visual noise. Ground is equally usable at left and right. Keep the main readable ground plane between vertical55% and90% of the image, no high foreground objects.
Lighting warm orange accents from right with cool slate shadows, moderate contrast and enough ground brightness for dark outlined characters. Newly invented medieval mining setting, not futuristic, no modern machinery, no lava river through the floor, no pits, obstacles, statues, characters, enemies, text, UI, borders or watermark. Avoid bright orange floor patterns that could be mistaken for attack telegraphs. Deliver only the background.
```
