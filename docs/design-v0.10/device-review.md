# v0.10 독립 검증 기록

2026-10-19 · Astra 검증 · 최종 4차 APK 실기기 회귀 완료

## 전투와 성장 규칙

SOL 구현과 별도로 작성한 [재현용 검증](../../tests/CatalogCombat/Review/README.md)을 최신 `Catalog*.cs`와 런타임 JSON으로 다시 컴파일해 실행했다.

| 검증 | 결과 |
| --- | --- |
| RosterReview | 64/64 통과 |
| BattleReview | 10/10 통과 |
| EdgeReview | 16/16 통과 |
| TimingReview | 19/19 통과 |
| 독립 검증 합계 | **109/109 통과** |
| CatalogCombat 기본 회귀 | 110개 통과 |
| 이전 CombatCoreSmoke | 20 시나리오, 326개 통과 |

확인한 주요 조건은 6결합의 모든 분기 쌍, 최종형 12개 분기의 보존과 실제 스킬 발동, 단계별 성장 제한, 소환 이후 Undo, 첫 타격 방어와 곱 피해 감소, 피해 증가 합산, 대상 동률 순서, 보호막 흡수 기여, 화상 틱과 처치 귀속, 연발 중 평타 중지, 조건 특성별 독립 재사용 시간, 자신과 다른 아군의 지원 범위다.

검증 중 발견한 결함은 개발 담당이 수정한 뒤 같은 프로브로 재실행했다. 특성의 실제 발동과 경계 조건을 검증하는 일부 프로브는 reflection과 내부 상태 설정을 사용한다. 실제 화면 입력과 프레임 속도 검증을 대신하지 않는다.

## 밸런스 결과 재현

최종 후보 데이터로 `BalanceProbe.cs`를 별도 컴파일하고 임시 출력 경로에서 **14,400회**를 재실행했다. 저장된 보고서와 독립 재실행 결과가 바이트 단위 해시까지 일치했다.

- 주요 편성 비교: 24행 × 100시드
- 30형태와 최종형 양쪽 분기: 72행 × 100시드
- 6결합과 원본 두 분기 비교: 48행 × 100시드
- 결과 SHA-256: `F1149DB66364BF0AFC405F96934F3BA191FFD0BFA438EFA9E6A3CBB1F08730A6`

이 결과는 정해진 적·배치·편성 조건의 재현성을 확인한다. 전체 원정, 소환 운, 유물, 모든 전승 조합, 역할별 최적 배치를 시험한 결과는 아니다. 개별 형태 비교는 티어별 총 소환 비용도 서로 다르므로 출시 밸런스 확정으로 해석하지 않는다.

## 아트 데이터

- 30개 고유 ID, 660개 프레임 사각형, 연결된 PNG 존재를 독립 확인했다.
- 모든 사각형이 원본 이미지 범위 안에 있고 피벗이 0–1 범위에 있다.
- 문서와 Unity Resources의 manifest가 동일하다.
- manifest SHA-256: `5F9B67DC017BC6F90B05DD59CD8A49A15FFE8B687324BD94650014CDA5F0E121`

원본 시트의 포즈를 표본 검토했다. 파일 수·좌표·정확한 중복 검사는 연속 동작의 부드러움이나 손·무기 일관성을 보장하지 않는다. 모바일 표시 확인은 아래 기록에 별도로 남긴다.

## 실기기

대상은 USB 디버깅이 승인된 Buddy2(Samsung SM-M236L), 가로 2408×1080이다. 각 새 APK의 SHA-256을 확인하고 `adb install -r`로 설치했다. 아래에서 전체 검증을 한 3차와 표시 회귀를 한 최종 4차를 구분한다.

### 최종 4차 APK

- APK: `unity/Builds/Android/expedition-combat-prototype.apk`, 105,215,776 bytes.
- SHA-256: `CB1BAF629293E0DBD713EA4436151ADCFD6B36565367911962B41D2482861D2C`
- 새 APK 설치·실행, 맵에서 CATALOG LAB 진입 성공.
- `BRANCH` 버튼과 긴 이름을 포함한 5명 팀 카드의 한국어 이름·ID가 잘림 없이 표시됨.
- B-KN 미리보기, H-06 공격 갤러리의 좌우 그림, 5명 대 5명 실제 전투의 그림·HP·피해 표시가 정상 출력됨. 빈 스프라이트 결함은 재현되지 않음.
- H-06, F-KN, F-AR, F-PR, H-04를 추가하고 드래그 배치 후 BASIC 5 전투를 시작, 1.9초에서 일시정지함.
- 해당 앱 프로세스(PID 5448) 로그에서 Unity 오류·관리 코드 예외·AndroidRuntime fatal·shader error가 검색되지 않음.

대표 최종 화면: [5 대 5 전투](device-evidence/build4-gameplay.png).
추가 증거: [준비 화면](device-evidence/build4-lab.png), [5명 팀 카드](device-evidence/build4-team.png), [공격 갤러리](device-evidence/build4-gallery.png), [앱 로그](device-evidence/build4-runtime.log).

### 3차 전체 기기 검증

- APK: 105,212,720 bytes, SHA-256 `CC2635CA2501863FD323E04EB051369A57BC714C04E9A5C9EEBF21F6617B3D6D`.
- 갤러리 30개를 모두 직접 선택하고 30장 캡처를 확인했다. 선택한 ID에 해당하는 그림이 표시되고, ALLY/RIGHT와 ENEMY/LEFT가 서로 반전되며, 배경에 시트 사각형이나 큰 흐린 테두리가 보이지 않았다. 증거 파일은 `device-evidence/final-gallery-00.png`부터 `final-gallery-29.png`까지다. 파일명의 `final`과 무관하게 이 30장은 **3차 APK 증거**다.
- H-06의 IDLE/WALK/ATTACK/HIT/DEATH를 각각 선택하여 서로 다른 자세와 쓰러진 자세가 표시됨을 확인했다. 660프레임 전부의 연속 동작을 육안 검수한 것은 아니다.
- H-06/H-02/H-03/H-04 팀 생성, H-03 드래그 위치 변경, 전투 시작, 2.3초에서 일시정지 후 별도 캡처에도 시간·HP·전투 자세 유지, 재개 후 승리, 2× 버튼 변경, RETRY SAME TEAM 후 0.4초로 재시작, RESET BATTLE 후 편집 상태 복귀를 확인했다.
- BOSS (BASIC) 프리셋의 커다란 골렘이 왼쪽을 보고 표시되고, HP 감소와 전투가 진행됨을 확인했다. 이는 기존 골렘 원화를 확대한 임시 그림이며 보스 전용 애니메이션 검증으로 해석하지 않는다.
- 공격 준비 프레임 0–2와 `BasicHit`/`SkillUsed`에서 시작하는 3–5 회복 프레임의 연결은 소스에서 확인했다. 기기 화면에서 공격·피격 자세와 피해 표시를 확인했으나, 프레임별 녹화 계측으로 명중 시점 오차나 정확한 1회 재생을 측정하지는 않았다.
- 앱 프로세스(PID 3868) 로그에 Unity 관리 코드 예외나 fatal은 없었다. Android의 `InteractionJankMonitor` 초기화 진단 예외가 있었으나 앱은 계속 실행되었고 게임 코드 스택은 없었다.

증거: [30번째 형태](device-evidence/final-gallery-29.png), [걷기](device-evidence/build3-state-walk.png), [공격](device-evidence/build3-state-attack.png), [피격](device-evidence/build3-state-hit.png), [죽음](device-evidence/build3-state-death.png), [드래그](device-evidence/build3-drag.png), [일시정지 A](device-evidence/build3-paused-a.png), [일시정지 B](device-evidence/build3-paused-b.png), [승리·2×](device-evidence/build3-speed.png), [재시작](device-evidence/build3-retry.png), [보스](device-evidence/build3-boss.png), [앱 로그](device-evidence/build3-runtime.log).

### 메모리와 남은 표시 한계

3차 APK의 `dumpsys meminfo` 순간 측정값이다. 장시간 누수 시험이나 FPS 계측은 수행하지 않았다.

| 시점 | TOTAL PSS (KB) | Graphics (KB) |
| --- | ---: | ---: |
| Lab 진입 | 312,636 | 88,568 |
| 30형태 갤러리 순회 후 | 325,068 | 91,392 |
| 여러 유닛 전투·보스 확인 후 | 345,817 | 107,632 |

증거: [시작](device-evidence/final-memory-start.txt), [30형태 후](device-evidence/final-memory-after30.txt), [전투 후](device-evidence/build3-memory-end.txt).

현재 화면은 개발용 전투 실험실이다. 유닛을 위쪽에 배치하면 HP바가 전장 상단 밖으로 나올 수 있고, 가까운 배치에서는 그림·이름·피해 숫자가 겹친다. [상단 HP바 예시](device-evidence/build3-paused-a.png). 이 표시는 플레이를 막지는 않았지만 최종 전투 화면의 가독성 개선 대상으로 남는다. 실측 FPS, 장시간 안정성, 모든 기기 해상도, 660프레임의 완성도를 이 결과로 보장하지 않는다.

### 두 번째 빌드의 사전 화면 검증

- APK: 105,210,492 bytes, SHA-256 `869A6ED47CCEB48EDCD7BA1C180078C0A1201E8058885A9CB7AC6E1D48DAED64`
- 설치 및 실행 성공. 기존 맵에서 CATALOG LAB 진입, 30형태 선택 화면의 한글·영문 표시 확인.
- H-01에 Shield 전승을 선택해 FUSE + ADD 실행 후 `inherited T-KN` 표시 확인.
- **발견 후 수정한 결함:** 파일은 `SOURCE ART + CROP DATA`로 로드되지만 캐릭터 미리보기와 갤러리가 비어 있었음. 개발 담당이 렌더링 경로를 수정했고 3·4차에서 실제 그림 표시를 재확인함.
- BRANCH 12 카테고리와 긴 영문 팀 카드 이름이 잘리는 작은 UI 결함도 전달함.

증거: [시작 맵](device-evidence/build2-launch.png), [미리보기 누락](device-evidence/build2-lab.png), [갤러리 누락](device-evidence/build2-gallery.png), [결합·전승](device-evidence/build2-fusion-added.png).

이 절은 수정 전 발견 이력이며 최종 수정본 결과는 위 4차 절에 기록했다.

HTML 갤러리는 이 검증 환경에서 사용할 수 있는 브라우저 표면이 없어 직접 조작하지 못했다. 파일과 manifest 연결 검토만 수행했다.
