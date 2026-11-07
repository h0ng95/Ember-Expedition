# v0.72 · 게스트, Google 계정과 클라우드 저장

2026-11-07 · IGNIS: ASHEN OATH

게스트는 기존처럼 기기에 저장한다. 홈의 **계정 / Account**에서 Google 계정을 연결하면 Unity Cloud Save에 원정·도감·기록·튜토리얼 상태를 백업한다. 다른 설치에서 같은 계정으로 로그인하면 기기 기록과 서버 기록을 비교하고 불러올 수 있다. 연구는 여전히 원정 안에서만 유지된다.

## 플레이어 흐름

1. 로그인 없이 시작할 수 있다. 새 게스트에게 서버 계정을 자동 생성하지 않는다.
2. Google 로그인은 Android Credential Manager의 계정 선택 창을 이용한다. 게임에서 비밀번호를 받지 않는다.
3. 연결 후 준비 화면의 변경을 모아 저장한다. 전투 중에는 동기화를 시작하지 않으며 변경이 없는 기록은 다시 업로드하지 않는다.
4. 통신이 실패해도 로컬 저장을 유지한다. 재접속은 간격을 늘려 재시도하고, 계정 화면에서 수동 동기화도 가능하다.
5. 두 기록이 달라지면 전투 번호·골드·최고 도달 전투·저장 시간을 비교한다. 골드나 연구를 합산하지 않는다. 기기 기록으로 서버를 교체하려면 한 번 더 확인한다.
6. 서버 기록을 불러오기 전에 기존 로컬 기록을 별도 두 슬롯에 보관한다. 로그아웃해도 로컬 진행은 유지한다.

복구되는 범위는 **마지막으로 서버 저장에 성공한 기록**이다. 게스트 상태나 오프라인에서만 진행한 미전송 기록은 앱 데이터 삭제 후 복구할 수 없다. Android의 일반적인 ‘캐시 삭제’와 ‘앱 데이터 삭제’는 다르며, 저장 초기화·재설치 복구는 같은 Google 계정으로 연결했을 때를 대상으로 한다.

## 이번에 연결한 개발 환경

| 항목 | 값 |
| --- | --- |
| Unity 프로젝트 | IGNIS Ashen Oath |
| Unity Project ID | `42dff428-51fe-4602-b9ae-713a582b9a8e` |
| UGS 환경 | `development` |
| Google 프로젝트 | `ignis-ashen-oath` |
| Google OAuth | 외부 앱 / 테스트 중 |
| Android 패키지 | `com.expeditionprototype.combatdemo` |
| 계정 SDK | Core 1.18.0 / Authentication 3.8.1 / Cloud Save 3.4.2 |
| Google Android SDK | Credentials 1.6.0 / Google ID 1.2.1 |

승인된 개발자 계정을 OAuth 테스트 사용자로 등록했다. 현재 Android 클라이언트는 개발 APK의 서명용이다. 출시 전에 Play App Signing 인증서와 출시 패키지를 확인해 추가 등록해야 한다. 공개 클라이언트 ID는 설정에 포함하지만 OAuth 비밀키·서비스 계정 키는 앱이나 저장소에 넣지 않는다. ID 토큰도 로그나 게임 저장 파일에 기록하지 않는다.

유료 플랜이나 결제 수단을 새로 등록하지 않았다. 개발/정식 데이터를 분리하며, 정식 환경 전환과 무료 사용량 감시는 출시 단계에 진행한다. 무료 한도 내에서 설계했다는 뜻이며, 향후 사용량·정책 변화까지 무료를 보장하는 것은 아니다.

## 데이터 보호 방식

- 기존 로컬 저장 형식 v2와 두 슬롯 검증을 유지한다.
- 동기화 기준은 마지막으로 양쪽이 공유한 저장 버전과 내용 해시다. 기기 시계나 더 높은 점수만으로 최신 저장을 고르지 않는다.
- 기존 서버 기록을 바꿀 때 Cloud Save의 `writeLock`을 사용한다. 다른 기기에서 수정하면 덮어쓰기를 거부하고 다시 비교한다.
- 서버에는 현재 기록과 직전 기록을 한 문서에 보관한다. 원정 본문은 1 MiB, 문서 전체는 4 MiB 이하로 제한한다.
- 손상·미지원 버전·다른 계정 소유 기록은 적용하지 않는다. 서버 읽기 실패를 ‘저장 없음’으로 처리하지 않는다.
- 앱 설정·언어·음량은 기기에 남긴다.

## 검증

- 순수 C# 동기화: **142개 검사 통과**. 통신 실패, 응답 유실 후 재시도, 손상/미래 버전, 계정·환경 전환, 양쪽 변경, 확인 도중 변경, 로컬 복원 실패 및 100개 분기 조합.
- 기존 로컬 저장: **150개 검사, 90개 여정 체크포인트 통과**.
- 한영 번역 회귀: **4,886개 검사 통과**.
- Unity: **32개 검사 통과**. 실제 JSON 변환, 기존 저장 호환, 계정 창 중앙 정렬, 한국어·영어 문구 높이.
- 실제 UGS development: 별도 합성 계정으로 **익명 인증 → 저장 → 읽기 → 이전 writeLock의 덮어쓰기 거부 통과**. 이 검사는 Google 로그인을 대신하지 않는다.
- Buddy2(Android 14): 개발 APK 설치 → 사용자가 Google 계정 선택·로그인 → **동기화 완료** 확인. 기존 5전투·30골드·이전 기록 4개를 서버에 저장했다.
- 연결 직후 로컬 기록·동기화 정보·설정을 별도 백업하고 저장 해시가 동기화 영수증과 일치함을 확인했다. 이후 **인증 세션을 유지한 채 진행과 동기화 기준만 비운 임시 상황**에서 앱을 다시 열었다. 서버 기록 안내와 한영 비교 창을 확인하고 ‘클라우드 기록 불러오기’를 눌렀다.
- 복원한 원정·도감·기록 본문 **8,968바이트가 원래 본문과 완전히 일치**했다. 연구·편성·골드를 합치지 않고 원본 하나로 복원했다. 한국어 설정으로 되돌렸으며 Google 연결은 유지했다.
- 이 실험은 실제 서버에서 빈 로컬 상태로 복구하는 검증이다. 앱 완전 삭제 후 새 인증 세션으로 재로그인하거나, 다른 물리 기기에서 동시 접속한 검증으로 표현하지 않는다.

[한국어 연결 완료](device-evidence/restored-ko.png) · [한국어 기록 선택](device-evidence/restore-choice.png) · [영어 기록 선택](device-evidence/restore-choice-en.png) · [영어 복원 완료](device-evidence/restored-en.png)

## 출시 전에 남은 것

1. **순위는 아직 구현하지 않았다.** 현재 계정 화면에도 준비 중으로 표시한다. 클라이언트가 쓸 수 있는 백업 데이터를 점수의 신뢰 근거로 쓰면 안 된다. 서버 검증과 순위 제출은 별도 단계다.
2. 최초 서버 저장 키를 두 기기에서 동시에 만드는 경우는 기존 키 수정과 다르다. 현재 Cloud Save API의 키 생성에는 create-only 잠금이 없어 최초 동시 업로드를 완전히 막지 못한다. 공개 출시 전 서버 조정 또는 최초 연결 절차 보강이 필요하다.
3. 계정 삭제, 서버 데이터 삭제, 개인정보처리방침, Google Play 데이터 보안, 정식 OAuth/출시 인증서와 Google 버튼 브랜드 점검이 남아 있다.
4. 장시간 오프라인, 인증 만료, 실제 두 기기 동시 사용, 정상 출시 서명으로 재설치 복원, 무료 한도 초과 응답을 추가 검증해야 한다.

## 재현과 파일

- `tests/CloudAccount/run.ps1`
- `Expedition.EditorTools.CloudAccountValidation.ValidateAndBuild`
- `Expedition.EditorTools.UgsConnectionValidation.Run` — 합성 데이터로 실제 development 서버 검증. Unity Play Mode에서 실행하며 테스트 후 에디터가 종료된다.
- 설정: `unity/Assets/Resources/Content/cloud-account-config.json`
- APK: `unity/Builds/Android/expedition-combat-prototype.apk`

공식 참고: [Unity Google 로그인](https://docs.unity.com/en-us/authentication/platform-signin/google), [Cloud Save SDK](https://docs.unity.com/en-us/cloud-save/tutorials/unity-sdk-sample), [Android Credential Manager](https://developer.android.com/identity/sign-in/credential-manager-siwg-implementation), [UGS 가격](https://unity.com/products/gaming-services/pricing).
