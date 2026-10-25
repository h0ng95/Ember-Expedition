# 서리 파수꾼 · 공격 모션용 분리 아트 (미채택)

사용자 요청으로 팔 분리 방식은 제거했다. 현재 적용은 [전신 모션](FROST-FULL-MOTION-PROMPTS.md)이다.

2026-10-25 · built-in `image_gen`, 투명 배경. 새 부유형 보스 원화를 참고하여 몸통·앞팔·뒤팔을 분리한 원본을 만들었다.

- 원본: `docs/design-v0.39/screenshots/frost-parts-rejected.png`.
- 런타임은 이 시트의 세 부위를 각각 그린다. 몸통/실루엣을 바꾸지 않고 팔 회전과 이동으로 일반 공격을 표현한다.
- 일반 공격: 왼쪽 주먹을 당김 → 피해 이벤트 순간 왼쪽으로 내지름 → 4개 릴리스 구간에 맞춰 회수.
- 서리 공격: 예고 중 양팔을 들고, 발동 시 양팔을 내린다. 얼음 타격 이펙트는 기존 실제 스킬 이벤트를 따른다.
- 보완: 공격 팔을 몸통보다 앞에 그리며, 팔의 어깨 연결점은 모든 자세에서 고정한다. 팔만 따로 평행 이동하는 동작은 제거했다.
- 손으로 그린 연속 프레임 시트가 아니라 분리한 도트 아트를 움직이는 방식이다.

## 생성 프롬프트

```text
Use case: precise-object-edit.
Asset type: transparent THREE-PART animation atlas for the exact floating frost boss in the input image. Input is an edit target and character identity reference. Preserve its grey plate steel, muted brass edging, navy cloth, blue crystals, cyan chest core, three-quarter LEFT facing view, proportions and detailed crisp pixel art.
Create one wide transparent sheet with exactly THREE separate cutout components, laid out on a 3-column grid, each column has generous transparent margins. No components touch or overlap. Each component has the SAME art detail density and physical scale, do not enlarge the smaller pieces to fill their cells.
COLUMN 1: the complete central floating torso with helmet, both large crystal shoulder pauldrons, chest, short waist, icy lower core, all three floating lower fragments and short navy cloth. Remove ONLY the two giant forearm/gauntlet/fist assemblies: both forearms and fists are absent here, with clean finished dark shoulder sockets under the pauldrons. NO legs or long skirt. Torso stays compact and wide exactly like input.
COLUMN 2: the full detached attacking arm assembly from the LEFT side of the input: the left big grey wrist cuff with small crystals and the huge clenched armored fist. Preserve the three-quarter left/down perspective and exact identity. This is a complete cutout, including its upper edge so it can slide underneath the shoulder. No torso/head/shoulder here.
COLUMN 3: the full detached rear arm assembly from the RIGHT side of input: right big wrist cuff with blue crystals and clenched armored fist. Same exact perspective and identity as input. Complete cutout with a finished upper edge. No torso/head/shoulder here.
The two forearm pieces will be animated independently in code: drawn back and thrust LEFT toward the opponent. Put an opaque clean armor edge at the upper wrist rather than leftover body pixels. No additional pieces, no attack trails, no effects, no ground shadow, no layout markings, no text or labels, no background. Genuine transparent alpha. This is a production rig atlas, NOT three complete characters.
```
