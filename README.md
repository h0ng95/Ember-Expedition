# Ember Expedition · 잔불 원정대

가로 화면의 도트 그래픽, 전투 전 드래그 배치, 자동 전투와 랜덤 선택을 결합하는 Android 게임 시제품이다. **Ember Expedition / 잔불 원정대는 개발 가칭**이며, 출시명과 캐릭터 디자인은 별도 권리 검토 후 확정한다.

- 개발: **Unity 6.3 LTS / C#**
- 첫 테스트 기기: **Galaxy Buddy2 (SM-M236L)**
- 상품 계획: 광고가 있는 무료 앱 + 광고 없는 별도 유료 앱, 인앱결제 없음
- 현재 목표: 기사·궁수 2명과 적 1명의 실제 전투를 에디터와 Android에서 실행하고 타격감·배치 조작 검증

## 시작하기

[Unity 프로젝트 안내](unity/README.md)에 설치 모듈, 프로젝트 열기, 조작과 테스트 APK 절차를 정리했다. Unity Hub에는 저장소 안의 `unity` 폴더를 등록한다.

엔진과 분리된 전투 계산은 다음 명령으로 검증한다. Windows 기본 .NET Framework C# 컴파일러를 사용하며 Unity와 추가 패키지 설치가 필요 없다.

```powershell
./tests/CombatCoreSmoke/run-smoke.ps1
```

## 프로젝트 구성

| 폴더 | 내용 |
| --- | --- |
| `unity/` | 실제 게임 시제품 소스, 설정, 컨셉 아트와 합성 효과음 |
| `tests/CombatCoreSmoke/` | 명중·투사체·표적 변경·배속·전투 종료 규칙 검증 |
| `tools/` | 효과음 제작 도구 |
| `ui/` | 브라우저에서 검토하는 초기 화면 시안 |
| `docs/` | 게임 기획서와 전투 연출 분석 |

[현재 기획서](docs/mobile-autobattler-plan-v0.3.md) · [읽기용 기획서](docs/mobile-autobattler-plan-v0.3.html) · [UI 시안](ui/README.md)

## 아트와 출처

현재 캐릭터 6종과 배경은 **출시 검토 전 컨셉**이다. AI 생성 기록 확인은 비침해 보증이나 출시 승인에 해당하지 않는다. 기존 캐릭터와의 유사성·이름·이용 권리를 검토하고, 우려가 남는 디자인은 다시 설계한다. 자세한 제작 기록은 `unity/Assets/Resources/Art/ASSET-PROMPTS.md`에 있다.

참고작의 로컬 캡처는 `docs/references/`에만 보관하며 Git 추적과 게임 배포에서 제외한다. 분석 문서에는 원본 공개 페이지·영상의 링크를 남긴다. 효과음은 외부 음원 없이 코드로 합성했다.

## Git 관리

- `main`에서 작동하는 단위로 커밋한다. 작업 규모가 커지면 별도 작업 브랜치를 사용한다.
- Unity의 `Assets/`, `Packages/`, `ProjectSettings/`와 모든 `.meta` 파일을 함께 관리한다. 첫 에디터 임포트에서 생성되는 `.meta`도 커밋한다.
- `Library/`, `Temp/`, `Builds/`, 로그, 인증 정보, 앱 서명키는 커밋하지 않는다.
- Git 원격 저장소 연결과 Unity 실행 검증 상태는 실제 작업 결과로 확인한다. 원격 연결 전에는 이 PC에만 이력이 저장된다.

아직 영입·보상·승급·원정 진행·저장·광고를 연결한 완성 게임은 아니다. 단계별 검증 후 확장한다.
