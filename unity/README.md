# IGNIS: ASHEN OATH · Unity 전투 시제품

> **2026-10-24 v0.30:** 황혼 정원 21~25전투, 적 4종과 배경·이야기, 후열 최대 셋의 위치에 연속으로 솟는 가시뿌리 보스를 추가했다. 독은 4초 지속하며 받는 회복량을 35% 줄인다. 이전 20전투 완료는 50G와 함께 21전투 준비로 이어진다. [변경·검증 기록](../docs/design-v0.30/HANDOFF.md). **NORMAL**만 플레이할 수 있고 **HARD**는 준비 중이다. 저장은 v6, v2~v5 이행을 지원한다. 아래 이전 버전 기록은 시제품 이력이다.

## v0.30 실행

1. 앱 홈에서 **새 원정**을 누른다. NORMAL과 준비 중인 HARD를 보여주는 창에서 시작한다. 이전 원정이 있으면 교체 안내를 확인한다.
2. 시작 40G에서 **동료 소환 · 20G**로 대기석에 동료를 얻고 전장으로 끌어 배치한다. 출전 최대 5명, 대기 최대 7명이다.
3. 재료 동료를 키울 동료 위에 끌어 약 0.45초 유지한다. 결과 미리보기에 ‘놓으면 실행’이 표시되면 놓아서 성장 +1을 얻는다. 진화 조건 달성 시 선택창이 자동으로 열린다. 특수 결합은 준비 화면의 **결합 가능** 버튼을 열어 결과를 확인하고 실행한다. 상세창에서 재료 보호와 특성 전승을 선택할 수 있다. 준비 중에는 **되돌리기**로 직전 행동을 취소할 수 있다.
4. 승리하면 골드를 받고 다음 전투로 이동한다. 패배하면 등불 1개를 잃고 같은 전투를 재도전한다. 보스는 5/10/15/20/25전투에 나온다. 저장은 소환·성장·배치·전투 시작·결과 때 처리한다.
5. **이어하기**는 준비·결과 상태를 복구하고, 중단된 전투는 동일한 시작 편성으로 다시 재생한다.
6. 홈의 **원정 기록** 또는 준비 화면의 **지역 이야기**에서 열린 기록을 읽는다. 이미 열린 기록은 새 원정을 시작해도 유지된다.
7. 홈의 **원정 성과**에서 Normal 최고 격파와 구간 완료를 확인한다. 이전 15·20전투 완료 기록은 새 원정·패배·앱 재시작 후에도 유지된다. Hard는 Normal 30전투 완료 후 도전 자격을 주는 계획이며 실제 전투는 아직 없다.
8. 보스 준비 오른쪽의 **보스 준비 점검**은 현재 출전·대기·성장에 따라 바뀐다. 기존 조작 설명은 **배치·성장 도움말**로 펼친다. 안내는 선택을 돕고 전투 시작을 막지 않는다. 순수 안내 검사는 `tests/ExpeditionPreparationHints/run.ps1`로 실행한다.

`tests/ExpeditionRun/run.ps1`은 순수 C# 원정 검사를, `tests/ExpeditionStory/run.ps1`은 이야기 기록 검사를, `tests/ExpeditionProgress/run.ps1`은 Normal 저장·성과·Hard 제한 검사를 실행한다. Unity 메뉴 **Expedition → Validate Normal Progress**는 실제 JsonUtility로 이전 v3/v4 및 v5 저장 6종과 성과 저장을 검사한다. `-executeMethod Expedition.EditorTools.ExpeditionProgressValidation.BuildAndroid`는 새 성과 검사와 기존 이야기 저장, 아군 크기·동작, 다섯 지역의 적 프레임·가림·결과 전환·드래그 입력·JSON 검사를 통과한 뒤 APK를 만든다. **Validate Saved Expedition**의 표본 원정 검사도 별도로 사용할 수 있다. 폐광 manifest는 원본 PNG에서 생성한다. 기존 저장을 사용하는 검사는 종료 시 원본을 복구한다.

2026-10-18 · Unity 6.3 LTS + C# · Android / Galaxy Buddy2 기준

## 현재 상태

Unity 6000.3.24f1에서 **임포트·C# 컴파일·씬 생성, Android APK 빌드와 Galaxy Buddy2의 첫 조합 전투를 확인했다.** 방패 기사·등불 사제·불꽃 마법사의 배치, 기사 훈련·특성 전승·분기 진화, 성문 골렘의 예고와 전투 승리가 현재 범위다. 장시간 성능과 발열·배터리는 아직 측정하지 않았다. 이 프로젝트는 완성된 게임이나 스토어 제출용 앱이 아니다.

- 아군 기사·궁수 2명과 적 1명의 전투 계산: 이동, 사거리, 공격 준비, 근접 명중, 투사체, HP, 사망과 종료.
- 표시 코드: 가로 전장, 전투 전 드래그 배치, 피격 플래시·밀림·피해 숫자·투사체, 1/2배속·일시정지·30/60fps·음소거.
- 직접 합성한 효과음 5종. 외부 음원 샘플을 사용하지 않았다.
- 전투 계산은 Unity 없이 실제 컴파일·실행했고 6개 시나리오, 261개 assertion을 통과했다. 상세 계약은 `../tests/CombatCoreSmoke/README.md` 참조.
- WAV의 길이, mono / 44.1kHz / PCM16, 피크와 양끝 페이드를 확인했다. 실제 청취와 효과음 품질 평가는 아직 하지 않았다.

캐릭터와 배경은 앞선 UI 시안의 임시 이미지다. **6종 캐릭터 모두 출시 검토 전 시안**이며, 생성 기록 확인만으로 비침해를 보장하지 않는다. 캐릭터별 기존 IP 유사성·이름·이용 권리 검토를 완료한 뒤 출시 자산을 정한다. 출처와 상태는 `Assets/Resources/Art/ASSET-PROMPTS.md`에 있다. 기존 게임의 캐릭터·스프라이트를 추출하거나 변형해 쓰지 않는다.

## 확인한 동작 · 2026-10-18

첫 Android 개발 빌드와 전체 재빌드한 수정본의 점검 결과다. 수정본에서도 드래그 배치·전투·일시정지·2배속·승리를 다시 확인했다. 중간 증분 빌드의 오류와 대응은 아래에 별도로 기록한다.

| 대상 | 확인 결과 |
| --- | --- |
| Unity 에디터 | `6000.3.24f1` 임포트·C# 컴파일·설정 완료. 캐릭터·배경, 이동·공격·HP 감소, 일시정지·재개·2배속·승리 확인 |
| Android APK | IL2CPP / ARM64 / OpenGL ES 3 개발 빌드 성공, 종료 코드 0. 버전 `0.0.1`, 최소 API 26 / 대상 API 36 |
| 실기기 실행 | Galaxy Buddy2 `SM-M236L`, Android 14에 설치·실행. 가로 `2408×1080` 화면 확인 |
| 터치 배치 | 실기기에서 ADB의 1000ms 터치 드래그로 기사 위치 이동 확인 |
| 전투와 조작 | 1배속 전투, 전투 시간 3.3초에서 일시정지 확인. 이후 2배속·30fps 제한 버튼·음소거 조작, 16.9초 승리와 RESET 확인 |
| 백그라운드 복귀 | 재개 후 홈 화면으로 약 2초 나갔다가 돌아옴. 화면 회전 전환 뒤 전투 상태가 유지되고 4.7초에서 일시정지된 상태 확인 |
| UI 수정 | 음소거 버튼을 `MUTED`로 줄였으며 최종 APK의 실기기 화면에서 글자 잘림 없이 표시됨 |
| 오류 기록 | 중간 증분 APK에서 엔진 코드 누락과 GUI 예외 발견. 전체 재빌드한 최종 APK의 실행·전투 로그에는 해당 오류, Unity 오류와 FATAL 기록이 관찰되지 않음 |

30fps 버튼은 프레임 상한 설정 동작을 확인한 것이며, 지속 프레임을 측정한 결과가 아니다. 앱 프로세스 종료 뒤 복원, 장시간 발열·배터리, 실제 효과음 청취 품질은 미검증이다.

## Unity 설치 후 처음 열기

1. Unity Hub에서 Unity 6.3 LTS와 **Android Build Support / Android SDK & NDK Tools / OpenJDK**를 설치한다. 프로젝트에 기록한 기준 버전은 `6000.3.24f1`이다. 최신 패치라고 보장하는 의미는 아니며, 다른 6.3 패치를 선택하면 설치 버전으로 프로젝트를 열고 검증 결과를 기록한다.
2. Hub의 프로젝트 추가에서 이 `unity` 폴더를 선택한다. 상위 `game` 폴더가 아니다.
3. 첫 임포트 후 설정 코드가 `Assets/Scenes/CombatPrototype.unity`를 만든다. 자동 준비가 안 되면 메뉴 **Expedition → Prepare Combat Prototype**을 실행한다.
4. 해당 씬을 열고 Play를 누른다. Console의 첫 오류부터 확인한다. Game 화면은 16:9 및 Buddy2의 긴 가로 비율을 비교한다.
5. 준비 중 기사·궁수를 왼쪽 배치 영역으로 드래그한다. **FIGHT**로 전투를 시작한다. 영어 버튼은 개발 확인용 임시 UI다.

기존 Android Studio의 SDK/NDK를 Unity와 호환된다고 가정하지 않는다. 처음에는 해당 Unity 에디터에 맞게 Hub가 설치한 도구를 사용한다. [Unity Android 환경 설정](https://docs.unity3d.com/6000.3/Documentation/Manual/android-sdksetup.html), [기준 에디터 릴리스](https://unity.com/releases/editor/whats-new/6000.3.24f1)

## 테스트 APK 만들기

1. Build Profiles에서 Android를 활성 플랫폼으로 전환하고 임포트·스크립트 컴파일이 끝날 때까지 기다린다.
2. **Expedition → Build Android Test APK**를 실행한다.
3. 성공하면 `Builds/Android/expedition-combat-prototype.apk`가 생성된다. 이 프로젝트에서 실제 APK 빌드와 Buddy2 설치·실행까지 확인했다.

설정은 가로 양방향, ARM64 / IL2CPP, OpenGL ES 3, 개발용 빌드다. 앱 ID `com.expeditionprototype.combatdemo`와 이름은 테스트용이며 출시용으로 확정한 것이 아니다. 개발 빌드의 프레임 결과를 출시 성능으로 해석하지 않는다. 실제 광고 SDK와 유료판 구분·배포 서명·스토어 등록은 아직 구성하지 않았다.

### Android 재빌드 시 주의

UI 문구를 수정한 증분 빌드는 빌드 성공으로 끝났지만, 실기기에서 `Could not produce class with ID 115`와 기본 GUI 스킨 오류가 발생했다. 빌드 로그에는 기존 player data를 재사용한 기록이 남았다. 테스트 APK 메뉴는 이제 `BuildOptions.CleanBuildCache`로 전체 빌드하고, 이 시제품에서 `PlayerSettings.stripEngineCode`를 꺼서 필요한 엔진 코드를 보존한다. 수정 APK를 다시 설치해 UI·전투 정상 동작을 확인했다. APK 생성 성공 뒤에도 반드시 실기기에서 확인한다. 출시용 용량 최적화는 별도 검증 대상이다. [Unity 클린 빌드 API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/BuildOptions.CleanBuildCache.html)

이전 COMBAT LAB APK 기록(v0.10 이전): 55,727,510바이트. SHA-256: `D2E0ADBE66EEBB7B78832CF193E1EE86C91AB8DC7A27BA286D055358204F0082`. Galaxy Buddy2에 설치해 골렘이 왼쪽을 바라보는 준비 화면을 `Logs/buddy2/golem-facing-left.png`로 확인했다. 이전 첫 조합 APK에서는 준비·진화·전승·골렘 예고·반격·승리·재시도·새 원정 복원을 확인했고, `Logs/astra-device/final-runtime.log`에서 Unity 오류·예외·FATAL 패턴이 관찰되지 않았다. APK와 이 이전 실행 로그·화면 캡처는 Git에 포함하지 않는다.

## v0.10 · 30형태 전투와 애니메이션

지도 아래 **CATALOG LAB / 30**에서 새 전투 시험실을 연다. 최대 다섯 명을 자유롭게 편성하고 전승 특성, 최종형의 분기, 적 편성을 선택한다. 전투 전 드래그 배치, 전투·배속·일시정지·재도전을 지원한다. **ANIMATION QA**에서는 30형태의 대기·걷기·공격·피격·쓰러짐을 확인한다.

게임과 자동 대전은 `Assets/Scripts/Core/Catalog*.cs`와 `Assets/Resources/Content/unit-catalog-v010.json`을 공유한다. 새 시트는 `Assets/Resources/Art/Units/v010/`의 30개 PNG이며 총 660프레임이다. 로드할 때 원본의 낮은 알파 잔광을 별도 렌더 텍스처에서 제거하고, 원본 픽셀은 보존한다.

최신 APK 경로는 `Builds/Android/expedition-combat-prototype.apk`다. 최종 빌드 해시와 실기기 확인 범위는 [독립 검증 기록](../docs/design-v0.10/device-review.md)에 남긴다. [제작 기록](../docs/design-v0.10/production-notes.md)과 [밸런스 실험](../docs/design-v0.10/balance-report.md)도 함께 본다. 새 시트와 전투 규칙은 CATALOG LAB에 연결했고, 기존 1-1·1-2를 새 원정 진행으로 교체하지는 않았다.

## 구조와 다음 검증

| 경로 | 역할 |
| --- | --- |
| `Assets/Scripts/Core/CombatSimulation.cs` | 엔진과 분리된 전투 계산. 고정 1/60초 틱, 배속은 틱 수 변경 |
| `Assets/Scripts/Presentation/BattlePrototype.cs` | 화면·터치·효과·소리와 계산 연결 |
| `Assets/Editor/PrototypeSetup.cs` | 에디터 최초 설정, 아트 임포트, 테스트 APK 메뉴 |
| `Assets/Shaders/HitFlash.shader` | 피격 시 캐릭터 흰색 플래시 |
| `Assets/Resources/Art` | 기존 컨셉 아트와 제작 기록 |
| `Assets/Resources/Audio` | 합성 WAV와 출처 기록 |
| `../tools/synthesize-combat-audio.py` | 효과음 재생성 |
| `../tools/check-unity-project.ps1` | 설치된 에디터로 일괄 임포트·컴파일·설정 검증 |
| `../tests/CombatCoreSmoke/run-smoke.ps1` | Windows 기본 C# 컴파일러로 전투 규칙 검증 |

다음 검증은 효과음 청취와 장시간 플레이 중 프레임·발열·배터리 측정이다. 새 CATALOG LAB은 프레임 애니메이션을 사용하고 기존 스테이지는 이전 정지 아트 방식을 유지한다. 새 원정의 영입·보상·진행·저장·광고 연결은 후속 작업이다. 홈 화면에서 돌아오는 동안 상태는 유지됐지만, 앱 프로세스가 종료되면 현재 전투는 보존되지 않는다.
