# IGNIS: ASHEN OATH · 이그니스: 잿빛 서약

가로 화면의 도트 그래픽, 전투 전 드래그 배치, 자동 전투와 랜덤 선택을 결합하는 Android 게임 시제품이다. **IGNIS: ASHEN OATH / 이그니스: 잿빛 서약은 개발 가칭**이며, 출시명과 캐릭터 디자인은 별도 권리 검토 후 확정한다.

- 개발: **Unity 6.3 LTS / C#**
- 첫 테스트 기기: **Galaxy Buddy2 (SM-M236L)**
- 최신 구현: **v0.25 원정 이야기·UI 개선**. 이그니스(Ignis)의 조각을 되찾는 이야기와 별도 저장되는 기록 7개, 숯색·황동 패널과 양피지 기록창을 추가했다. 영어판 용어·이야기 초안을 준비했으며 전체 언어 전환은 후속이다. [변경·검증 기록](docs/design-v0.25/HANDOFF.md). 현재 15전투와 폐광 적은 [v0.24](docs/design-v0.24/HANDOFF.md), 성채 난도는 [v0.23](docs/design-v0.23/HANDOFF.md), 아군 몸 크기는 [v0.22](docs/design-v0.22/HANDOFF.md), 부분 겹침·보스 표시는 [v0.20](docs/design-v0.20/HANDOFF.md), 성장·결합 조작은 [v0.16](docs/design-v0.16/HANDOFF.md)을 따른다.
- 비교 검토 이력: [초반 난도·시작 인원·직업 해금 비교](docs/design-v0.16/early-progression-review.md). 난도 변경은 v0.17에 반영했으며, 시작 인원·골드·직업 해금 제안은 미적용이다.
- 적 미술: 숲·성채·폐광 각각 4종, 총 12종은 **개별 애니메이션 첫 제작본**을 사용한다. [숲 전투 기록](docs/design-v0.19/HANDOFF.html) · [성채 전투 기록](docs/design-v0.21/HANDOFF.html) · [폐광 제작·전투 기록](docs/design-v0.24/HANDOFF.html).
- 첫 마켓 출시 목표: **한 원정 30전투, 5전투마다 보스**. 현재 15전투이며 20→30전투로 확장한다. 100전투·무한 모드는 출시 후 후보다. [출시 범위와 개발 순서](docs/design-v0.16/early-progression-review.md#10-첫-마켓-출시-범위-30전투).
- 설계안: [v0.13 노션용 문서](docs/design-v0.13/expedition-economy.md) · [읽기용 문서](docs/design-v0.13/expedition-economy.html). 신규 졸개 미술·실제 플레이 난도 조정·원정 연구는 후속 개발 범위다.
- 상품 계획: 광고가 있는 무료 앱 + 광고 없는 별도 유료 앱, 인앱결제 없음
- 현재 흐름: **새 원정·이어하기 → 준비·골드 소환·배치·전승·진화·결합 → 15전투 → 원정 결과**. 기존 별점 지도는 본편 진입에서 제외했다. 30형태 시험실은 홈의 개발용 메뉴에 남겼다.
- 기준 기획: [v0.8 게임 기획과 디자인 재정립](docs/design-v0.8/ember-expedition-replan.md) · [읽기용 문서](docs/design-v0.8/ember-expedition-replan.html) · [화면 구성 시안](docs/design-v0.8/ui-concept.html). v0.11은 버튼 소환과 연속 원정을 먼저 연결한 축약판이다. 당시 12전투안은 설계 이력이며, 현재 첫 출시 목표는 30전투다. 유물·영구 해금은 후속 범위다.
- 직업 상세안: [v0.9 직업·진화·결합체 설계](docs/design-v0.9/unit-catalog.md) · [읽기용 직업 도감](docs/design-v0.9/unit-catalog.html). 기본·분기·최종·특수 결합 총 30형태의 능력치와 스킬을 정리한 검증 전 초안이다.
- 새 전투 시험실: **CATALOG LAB / 30**에서 30형태의 실제 스킬·전승·진화·결합과 첫 애니메이션을 시험한다. [v0.10 제작 기록](docs/design-v0.10/production-notes.md) · [움직이는 도감](docs/design-v0.10/animation-gallery.html) · [밸런스 실험](docs/design-v0.10/balance-report.md). 30개 시트에 대기·걷기·공격·피격·쓰러짐 총 660프레임을 제작했으며, 출시 전 동작과 한 원정 전체의 균형은 계속 다듬는다.

## 시작하기

[Unity 프로젝트 안내](unity/README.md)에 설치 모듈, 프로젝트 열기, 조작과 테스트 APK 절차를 정리했다. Unity Hub에는 저장소 안의 `unity` 폴더를 등록한다.

엔진과 분리된 전투 계산은 다음 명령으로 검증한다. Windows 기본 .NET Framework C# 컴파일러를 사용하며 Unity와 추가 패키지 설치가 필요 없다.

```powershell
./tests/CombatCoreSmoke/run-smoke.ps1
./tests/CatalogCombat/run.ps1
./tests/CatalogCombat/Review/run-review.ps1
./tests/CatalogCombat/run-balance.ps1
./tests/ExpeditionRun/run.ps1
./tests/ExpeditionRun/run-balance.ps1
./tests/ExpeditionRun/run-early-balance.ps1
./tests/ExpeditionStory/run.ps1
```

## 프로젝트 구성

| 폴더 | 내용 |
| --- | --- |
| `unity/` | 실제 게임 시제품 소스, 설정, 컨셉 아트와 합성 효과음 |
| `tests/CombatCoreSmoke/` | 기본 전투와 보호막·화상·진화·전승·보스 공격 규칙 검증 |
| `tools/` | 효과음 제작, Unity 검증과 기획 문서 생성 도구 |
| `ui/` | 브라우저에서 검토하는 초기 화면 시안 |
| `docs/` | 게임 기획서와 전투 연출 분석 |

[1-1 플레이 방법과 검증 범위](unity/FIRST-SLICE.md) · [월드맵·스테이지 기획 v0.7](docs/stage-plan-v0.7/잔불원정대-월드맵-스테이지-기획.md) · [중세 판타지 기획서 v0.6](docs/ember-expedition-design-v0.6.md) · [읽기용 기획서](docs/ember-expedition-design-v0.6.html) · [Slotbound 비교](docs/slotbound-comparison-v0.4.md) · [이전 동양풍 검토안](docs/ember-expedition-design-v0.5.md) · [UI 시안](ui/README.md)

현재 기획의 중심은 **조합·연쇄 효과의 성장과 배치·보스 공략**이다. 테마는 중세 판타지다. v0.11에서는 별 수집 대신 원정 중의 부대 성장을 저장한다. 1-1·1-2와 별점 지도의 이전 구현, v0.6·v0.7 문서는 설계 이력으로 보존한다.

## 아트와 출처

현재 캐릭터 6종과 배경은 **출시 검토 전 컨셉**이다. AI 생성 기록 확인은 비침해 보증이나 출시 승인에 해당하지 않는다. 기존 캐릭터와의 유사성·이름·이용 권리를 검토하고, 우려가 남는 디자인은 다시 설계한다. 자세한 제작 기록은 `unity/Assets/Resources/Art/ASSET-PROMPTS.md`에 있다.

참고작의 로컬 캡처는 `docs/references/`에만 보관하며 Git 추적과 게임 배포에서 제외한다. 분석 문서에는 원본 공개 페이지·영상의 링크를 남긴다. 효과음은 외부 음원 없이 코드로 합성했다.

## Git 관리

- `main`에서 작동하는 단위로 커밋한다. 작업 규모가 커지면 별도 작업 브랜치를 사용한다.
- Unity의 `Assets/`, `Packages/`, `ProjectSettings/`와 모든 `.meta` 파일을 함께 관리한다. 첫 에디터 임포트에서 생성되는 `.meta`도 커밋한다.
- `Library/`, `Temp/`, `Builds/`, 로그, 인증 정보, 앱 서명키는 커밋하지 않는다.
- 원격 저장소는 [h0ng95/Ember-Expedition](https://github.com/h0ng95/Ember-Expedition)이며 `main`의 최초 업로드를 완료했다. Unity 실행 검증 상태는 `unity/README.md`에 기록한다.

현재 기본 진입은 15전투 원정이며, 동료 30형태와 숲·성채·폐광 적 12종의 애니메이션을 본편에서 사용한다. 보스는 5·10·15전투에 등장한다. 지속적인 동작·난도 조정·16~30전투·원정 연구·광고·출시 설정은 후속 단계다.
