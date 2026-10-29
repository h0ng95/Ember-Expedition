# IGNIS: ASHEN OATH · 이그니스: 잿빛 서약

가로 화면의 도트 그래픽, 전투 전 드래그 배치, 자동 전투와 랜덤 선택을 결합하는 Android 게임 시제품이다. **IGNIS: ASHEN OATH / 이그니스: 잿빛 서약은 개발 가칭**이며, 출시명과 캐릭터 디자인은 별도 권리 검토 후 확정한다.

- 개발: **Unity 6.3 LTS / C#**
- 첫 테스트 기기: **Galaxy Buddy2 (SM-M236L)**
- 최신 용 연출: **v0.57 긴 예고는 연속 불기둥, 원형 예고는 공중 브레스**. 브레스 끝을 예고 원에 고정하고 일반 공격의 입가 불꽃을 제거했다. 피해량·판정·예고 시간은 유지한다. [변경·실기기 영상](docs/design-v0.57/HANDOFF.html) · [원문](docs/design-v0.57/HANDOFF.md).
- 최신 준비·성장 UI: **v0.56 전장 확대·간결한 적 요약·진화와 결합의 큰 모습 비교·완료 연출**. 상세 능력은 유지하고 드래그·범위·튜토리얼 좌표를 통합했다. Buddy2 조작 및 15·30 보스전 1×/2× 확인. [변경·화면·성능 표본](docs/design-v0.56/HANDOFF.html) · [원문](docs/design-v0.56/HANDOFF.md).
- 최신 미술: **v0.55 남은 상위 18형태 도트 통일과 장비 연속성 검수**. 기준 방패 기사와 앞서 바꾼 11종을 포함해 전체 30형태를 비교한다. 사람 키로 크기를 맞추고 등불 소품을 없앴으며 공격의 빛은 유지했다. 장비 오류가 있는 8슬롯은 온전한 전체 자세를 재사용한다. [움직이는 비교](docs/design-v0.55/art-review.html) · [변경·검증 기록](docs/design-v0.55/HANDOFF.html).
- 직업 역할: **v0.53 직업 역할·결합 선택 안내와 6종 비교**. 유닛 상세·결합 결과에 ‘강점 · 주의점’ 탭을 추가했다. 같은 투자량의 유지/결합/보충 편성 10,752조건·32,256전투를 비교하고, 편성에 따른 손익이 달라 전투 수치는 유지했다. [역할·비교·검증](docs/design-v0.53/HANDOFF.html) · [원문](docs/design-v0.53/HANDOFF.md).
- 지원 배치·용 조정: **v0.52 지원 범위별 동료 목록·수호룡 기본 공격 조정**. 준비 화면에서 번호·그림·이름으로 범위 안/밖을 확인한다. 2,016개 정상 원정 시나리오와 같은 편성 비교로 새 원정의 불덩이 계수만 0.9로 조정하고 브레스·비행·착지는 유지했다. 이전 원정과 기록은 보존한다. [화면·비교·검증](docs/design-v0.52/HANDOFF.html) · [원문](docs/design-v0.52/HANDOFF.md).
- 최신 전투 UI: **v0.51 왼쪽 아군 초상화·체력 목록과 중앙 보스 체력바**. 피해 잔상·상태 아이콘·카드 선택·전투 불능 표시를 연결했다. Buddy2 1×/2×와 Unity 484조건 및 글자 배치 검사. 별도 672개 정상 진행 원정으로 15·30전투를 재검증했으며 이번에는 전투 수치를 바꾸지 않았다. [화면·검증·난도 비교](docs/design-v0.51/HANDOFF.html) · [원문](docs/design-v0.51/HANDOFF.md).
- 최신 전투 행동: **v0.50 수호룡 원거리 기본 공격**. 기본 사거리 1.1→3.2와 도트 화염탄으로 과도한 접근을 줄였다. 새 원정부터 적용하고 이전 원정·기록은 보존한다. 8,769조건·36회 비교와 Buddy2 1×/2× 확인. 공격 기회가 늘어 난도는 추가 비교가 필요하다. [변경·검증·실기기 화면](docs/design-v0.50/HANDOFF.html) · [원문](docs/design-v0.50/HANDOFF.md).
- 최신 전투 연출: **v0.49 수호룡 화염 브레스**. 더 두꺼운 도트 불꽃·입 기준점 보정·분출 뒤 연기 분리. 피해 판정은 유지하며 Buddy2 1×/2× 확인. [미리보기·실기기 영상](docs/design-v0.49/breath-preview.html) · [구현·검증 기록](docs/design-v0.49/HANDOFF.html).
- 최신 구현·밸런스: **v0.48 폐광 압박·서리 난도 흐름·보스 빙결 연쇄**. 새 원정부터 적용하며 진행 중인 원정과 버전별 최고 기록을 보존한다. 별도 자동 원정 3,584회·69,227전투 비교, 같은 편성 비교와 Buddy2 확인. 15보스의 격차는 남아 있다. [변경·검증·실기기 화면](docs/design-v0.48/HANDOFF.html) · [원문](docs/design-v0.48/HANDOFF.md).
- 최신 난도 점검: **v0.47 분석 기록**. 1~30전투 집계와 같은 편성 비교에서 15전투의 급상승, 20보스의 낮은 생존 압박, 지역 첫 전투의 큰 완화를 확인했다. 일괄 증원안은 기각했으며 게임 수치는 v0.46 그대로다. [판별 수치·조정 우선순위](docs/design-v0.47/difficulty-audit.html) · [분석 원문](docs/design-v0.47/difficulty-audit.md) · [재현 도구](tests/DifficultyCurve/README.md).
- 연구·동료 밸런스: **v0.46 지역 돌파 조건·서리/술식 조정·버전별 최고 기록 보존**. 숲 5전투 이후 기초, 성채 10전투 이후 상위 연구를 골드로 선택하며 새 원정에서 초기화한다. 연구의 컬러 도트 문양 6종과 중앙 정렬·설명 글자 배치도 수정했다. 변경 전후 자동 원정 26,880회·497,293전투를 비교했다. 개선과 악화 경로를 함께 기록했으며 출시 밸런스 확정은 아니다. [결과와 한계](docs/design-v0.46/HANDOFF.md) · [실기기 화면](docs/design-v0.46/HANDOFF.html) · [재현 방법](tests/ResearchRegions/README.md).
- 원정 초기화·기록: **v0.45 매 원정 연구 초기화·영구 능력치 강화 제거·개인 기록**. 새 원정은 같은 4개 시작 편성 선택권과 40G·연구 0개로 시작한다. 이전 원정은 원래 규칙으로 보존하며, 점수는 분리한다. 자동 원정 440회와 Buddy2 저장·화면 확인. [구현·검증·남은 과제](docs/design-v0.45/HANDOFF.md) · [실기기 화면 포함](docs/design-v0.45/HANDOFF.html) · [개편 기준](docs/design-v0.45/expedition-reset-plan.html).
- 최신 안정성·설정: **v0.44 원정 통합 백업·자동 복구, 공통 설정, 전투 상하단 문구 수정**. 옛 PlayerPrefs 이행과 손상 기록 보관, 음량·피격 흔들림·번쩍임 감소, 거점·일시정지 접근을 연결했다. [검증과 점수 순위 제안](docs/design-v0.44/HANDOFF.md) · [실기기 화면 포함](docs/design-v0.44/HANDOFF.html).
- 최신 밸런스: **v0.43 성가 기수·원정 유격대장 결합 손익 비교**. 같은 재료량의 결합 전/후/기존 대기 동료 투입 2,112사례를 전후 비교하고 512개 연속 원정으로 교차 확인했다. 성가 기수 긴급 회복, 유격대장 직접 지휘 사격을 추가했다. [판단·검증·원시 자료](docs/design-v0.43/HANDOFF.md) · [화면 포함 읽기용](docs/design-v0.43/HANDOFF.html).
- 최신 UI 구현: **v0.42 첫 성장·진화·전술 안내, 배속 저장, 원정 종료 요약**. 필요한 순간의 강조 안내·건너뛰기·다시 보기, 같은 원정의 배속 유지, 골드와 신규 연구 PT 집계를 연결했다. 신규 코어 5,744조건과 Buddy2 조작 확인. [변경·검증 기록](docs/design-v0.42/HANDOFF.md) · [실기기 화면 포함](docs/design-v0.42/HANDOFF.html).
- 동료 정보: **v0.41 동료 30형태 정보·진화·결합 미리보기**. 도트 대기·공격, 실제 시작 스탯, 기술·특성·연구 조건, 소모 재료와 출전 인원 변화를 확인하고 선택한다. 계산 306사례·1,887조건, Unity 217조건 및 Buddy2 조작 확인. [변경·검증 기록](docs/design-v0.41/HANDOFF.md) · [실기기 화면 포함](docs/design-v0.41/HANDOFF.html).
- 최신 검증: **v0.40 새 원정부터 종료까지 3,360개 시나리오·77,836회 전투**. 저장 경계 50,138개, 회귀 5,665개, 실제 Unity 직렬화 5,180개 검증 통과. Buddy2 신규 원정·강제 종료·패배 종료·연구 해금도 확인했다. [결과·개선 우선순위](docs/design-v0.40/HANDOFF.md) · [화면 포함 읽기용](docs/design-v0.40/HANDOFF.html). 자동 정책의 결과는 사람의 완주율과 다르다.
- 전투 구현: **v0.39 전투 HUD·보스 6종 도트 공격 애니메이션·로컬 테스트 기록**. 고정 보스 정보, 피해·상태 구분, 지역 적 가독성과 고유 공격 연출을 보완했다. [변경·검증 기록](docs/design-v0.39/HANDOFF.md) · [화면 포함 읽기용](docs/design-v0.39/HANDOFF.html) · [공격 애니메이션 미리보기](docs/design-v0.39/boss-effects-preview.html). Normal 30전투만 플레이 가능하며 Hard 전투는 준비 중이다. [스플래시·기사 아이콘](docs/design-v0.38/HANDOFF.md) · [첫 조작 안내](docs/design-v0.36/HANDOFF.md) · [결합체·난도 검토](docs/design-v0.34/HANDOFF.md).
- 출시 준비 실행 계획: **전투 가독성 → 첫 플레이테스트 → 밸런스·안내 → 저장·설정·영어·사운드 → 광고·출시 검증**. 1단계 구현 후 신규 플레이어 관찰과 기록 분석을 진행할 차례다. [순서·완료 기준·체크리스트·테스트 양식](docs/release-readiness-v0.38.md) · [읽기용 문서](docs/release-readiness-v0.38.html). [연구·전술 규칙](docs/design-v0.32/HANDOFF.md) · [연구 보드·전투 기여도](docs/design-v0.33/HANDOFF.md).
- 비교 검토 이력: [초반 난도·시작 인원·직업 해금 비교](docs/design-v0.16/early-progression-review.md). 난도 변경은 v0.17에 반영했으며, 시작 인원·골드·직업 해금 제안은 미적용이다.
- 적 미술: 숲·성채·폐광·서리 성당·황혼 정원·잿빛 왕궁 각각 4종, 총 24종은 **개별 애니메이션 첫 제작본**을 사용한다. [왕궁 제작·검증](docs/design-v0.31/HANDOFF.html).
- 첫 마켓 출시 목표: **한 원정 30전투, 5전투마다 보스**. 30전투 경로를 연결했으며 전체 난도·연출과 출시 준비는 진행 중이다. 100전투·무한 모드는 출시 후 후보다. [출시 범위와 개발 순서](docs/design-v0.16/early-progression-review.md#10-첫-마켓-출시-범위-30전투).
- 설계 이력: [v0.13 노션용 문서](docs/design-v0.13/expedition-economy.md) · [읽기용 문서](docs/design-v0.13/expedition-economy.html). 현재 연구와 재화 규칙은 v0.46 문서를 따른다.
- 상품 계획: 광고가 있는 무료 앱 + 광고 없는 별도 유료 앱, 인앱결제 없음
- 현재 흐름: **새 원정 편성 선택·이어하기 → 준비·골드 소환·원정 연구·배치·성장 → 30전투 → 점수·개인 기록**. 기존 별점 지도는 본편 진입에서 제외했다. 30형태 시험실은 홈의 개발용 메뉴에 남겼다.
- 기준 기획: [v0.8 게임 기획과 디자인 재정립](docs/design-v0.8/ember-expedition-replan.md) · [읽기용 문서](docs/design-v0.8/ember-expedition-replan.html) · [화면 구성 시안](docs/design-v0.8/ui-concept.html). v0.11은 버튼 소환과 연속 원정을 먼저 연결한 축약판이다. 당시 12전투안은 설계 이력이며, 현재 첫 출시 목표는 30전투다. 유물은 후속 범위이며 첫 영구 연구는 v0.32에 연결했다.
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
./tests/ExpeditionProgress/run.ps1
./tests/ExpeditionResearch/run.ps1
./tests/CatalogBattleReport/run.ps1
./tests/UnitPreview/run.ps1
./tests/ExpeditionLearning/run.ps1
./tests/AccountSave/run.ps1
./tests/ResearchRegions/run.ps1
./tests/HybridValue/run.ps1
./tests/HybridCombat/run.ps1
./tests/ExpeditionRun/run-end-to-end-balance.ps1 -FirstSeed 19001 -Count 40 -GrowthOrder Balanced -Spending all -ReportPath .local-tools/balance-holdout.json
./tests/ExpeditionPreparationHints/run.ps1
./tests/ExpeditionRun/run-hybrid-balance.ps1 -FirstSeed 1 -Count 40 -GrowthOrder TargetFirst
./tests/ExpeditionRun/run-hybrid-balance.ps1 -FirstSeed 1 -Count 40 -GrowthOrder FrontFirst
./tests/ExpeditionRun/run-hybrid-balance.ps1 -FirstSeed 1 -Count 40 -GrowthOrder Balanced
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

현재 기본 진입은 30전투 원정이며, 동료 30형태와 여섯 지역 적 24종의 애니메이션을 사용한다. 보스는 5·10·15·20·25·30전투에 등장한다. 전체 난도와 회복량의 추가 조정·유물·연구 확장·광고·출시 설정은 후속 단계다.
