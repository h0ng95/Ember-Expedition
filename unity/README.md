# Ember Expedition · Unity 전투 시제품

2026-10-18 · Unity 6.3 LTS + C# · Android / Galaxy Buddy2 기준

## 현재 상태

Unity에서 열기 위한 프로젝트 소스를 준비했다. **이 PC에 Unity가 설치되어 있지 않아 에디터 컴파일·화면·터치·APK·실기기 성능은 아직 검증하지 않았다.** 이 프로젝트는 완성된 게임이나 스토어 제출용 앱이 아니다.

- 아군 기사·궁수 2명과 적 1명의 전투 계산: 이동, 사거리, 공격 준비, 근접 명중, 투사체, HP, 사망과 종료.
- 표시 코드: 가로 전장, 전투 전 드래그 배치, 피격 플래시·밀림·피해 숫자·투사체, 1/2배속·일시정지·30/60fps·음소거.
- 직접 합성한 효과음 5종. 외부 음원 샘플을 사용하지 않았다.
- 전투 계산은 Unity 없이 실제 컴파일·실행했고 6개 시나리오, 261개 assertion을 통과했다. 상세 계약은 `../tests/CombatCoreSmoke/README.md` 참조.
- WAV의 길이, mono / 44.1kHz / PCM16, 피크와 양끝 페이드를 확인했다. 실제 청취와 Android 재생은 미검증이다.

캐릭터와 배경은 앞선 UI 시안의 임시 이미지다. **6종 캐릭터 모두 출시 검토 전 시안**이며, 생성 기록 확인만으로 비침해를 보장하지 않는다. 캐릭터별 기존 IP 유사성·이름·이용 권리 검토를 완료한 뒤 출시 자산을 정한다. 출처와 상태는 `Assets/Resources/Art/ASSET-PROMPTS.md`에 있다. 기존 게임의 캐릭터·스프라이트를 추출하거나 변형해 쓰지 않는다.

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
3. 성공하면 `Builds/Android/expedition-combat-prototype.apk`가 생성된다. 이 절차 자체도 현재는 미검증이다.

설정은 가로 양방향, ARM64 / IL2CPP, OpenGL ES 3, 개발용 빌드다. 앱 ID `com.expeditionprototype.combatdemo`와 이름은 테스트용이며 출시용으로 확정한 것이 아니다. 개발 빌드의 프레임 결과를 출시 성능으로 해석하지 않는다. 실제 광고 SDK와 유료판 구분·배포 서명·스토어 등록은 아직 구성하지 않았다.

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

첫 실행에서 씬 생성·셰이더·텍스처·폰트·배치·공격 타이밍과 효과음을 확인한다. 그다음 Buddy2에서 터치와 백그라운드 복귀를 확인한다. 캐릭터는 정지 이미지를 움직이는 단계이며 걷기·공격의 프레임 애니메이션은 아직 없다. 영입·보상·승급·원정 진행·저장·광고도 후속 작업이다. 앱을 닫으면 현재 전투가 보존되지 않는다.
