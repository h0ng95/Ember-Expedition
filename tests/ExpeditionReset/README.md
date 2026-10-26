# 원정 초기화·개인 기록 검증

`./tests/ExpeditionReset/run.ps1`은 Unity와 독립된 코어 검사다.

- 4개 시작 편성 × 20시드: 신규/이전 연구 완료 계정 입력의 실제 첫 전투 80쌍 비교.
- 연구 구매 조건, 전투 간 유지, 저장 복원, 새 원정 초기화.
- 30전투 저장 진행 경로 및 30개 패배 종료 경계. 이 부분의 강제 승패는 밸런스 검사가 아니다.
- v8 이전 원정 효과 보존, v9 영구 효과 차단, 이전 PT 보관 및 신규 지급 중단.
- 점수 산식, 중복 종료, 포기 제외, 최근 30개 제한, 최고 기록 별도 보존, 변조된 기록 거부.

Unity `JsonUtility`의 빈 참조 처리와 실제 화면의 NewRun/Save 경로는 `Expedition.EditorTools.ExpeditionResetValidation.ValidateAndBuild`에서 별도로 검사한다. 실제 전투의 연속 진행·중단 복원은 `tests/ExpeditionRun/run-end-to-end-balance.ps1 -Interruptions`로 검사한다.

검증 범위·결과는 [v0.45 인계 문서](../../docs/design-v0.45/HANDOFF.md)에 기록한다.
