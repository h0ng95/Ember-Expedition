# 원정 연구 지역 조건과 v0.46 비교

저장소 루트의 PowerShell에서 실행한다. Windows 기본 .NET Framework C# 컴파일러를 사용하고, 대량 실행에는 Node.js가 필요하다.

```powershell
./tests/ResearchRegions/run.ps1
```

지역 보스 격파 전후·골드·상위 선행 연구·패배·이어하기·새 원정 초기화, 실제 공격 계수, 이전 원정의 수치 유지, v0.45/v0.46 최고 기록 분리와 JSON 왕복을 검사한다. 일부 상태 검사는 전투 승리를 직접 지정하므로 실제 전투 밸런스 사례 수에 포함하지 않는다.

## 같은 시드로 변경 전후 비교

```powershell
./tests/ResearchRegions/run-matrix.ps1 -FirstSeed 47001 -Count 40 -Rules v045 -Output .local-tools/recheck-before
./tests/ResearchRegions/run-matrix.ps1 -FirstSeed 47001 -Count 40 -Rules current -Output .local-tools/recheck-after
```

각 명령은 4개 시작 편성 × 7개 성장 목표 × 8개 지출 경로 × Count개 시드를 실행한다. Count=40이면 각 버전 8,960회다. 20개 작업을 최대 4프로세스로 나눠 실행하며 출력 폴더에 JSON, 로그, 해당 실행의 컴파일 결과를 남긴다. 서로 다른 출력 폴더를 사용한다.

`v045`는 v9 / RulesVersion 1 저장에서 시작해 이전 기술·연구 수치를 사용한다. `current`는 새 원정의 v10 / RulesVersion 2다. 기존 기준 실행 파일과 현재 코드의 이전 프로필을 448회, 조정 후보와 현재 프로필을 448회 비교해 주요 종료 결과 일치를 확인했다.

정책 final은 최종 진화, H-01~H-06은 각 결합체 확보 목표다. 성장 순서는 Balanced이고 모든 분기·임의 배치를 탐색하지 않는다. none/selective는 계열별로 중복 실행하지 않는다. selective는 5명 확보 후 목표 역할에 맞는 기초 연구 1개를 선택하므로 표의 defense 열만 보고 항상 방어 연구라고 해석하면 안 된다.

계열별 구매 순서:

- defense: 방벽 훈련 → 방패술 → 정밀 사격
- precision: 정밀 사격 → 약점 공략 → 방벽 훈련
- arcana: 술식 연구 → 잔불 증폭 → 방벽 훈련

early는 6전투부터 가능한 연구를 먼저 구매하고, delayed는 11전투부터 전투당 하나씩 구매한다. 위 순서의 연구가 잠겨 있으면 다음 구매 가능한 연구를 확인한다.

## 특정 실패와 재접속 검사

```powershell
./tests/ExpeditionRun/run-end-to-end-balance.ps1 -FirstSeed 47007 -Count 1 -Starter frost -Policy final -Spending early -GrowthOrder Balanced -ResearchBranch defense -Rules current -ReportPath .local-tools/frost-47007.json
./tests/ExpeditionRun/run-end-to-end-balance.ps1 -FirstSeed 48001 -Count 4 -Starter frost -Policy final -Spending delayed -GrowthOrder Balanced -ResearchBranch arcana -Rules current -Interruptions -ReportPath .local-tools/frost-resume.json
```

실제 전투 계산, 패배 등불 차감, 승리 보상, 합법적인 성장·소환·연구와 종료 조건을 사용한다. 상세 trace는 첫 시드 전체와 나머지 시드의 패배 전투를 보관한다. 집계는 trace를 줄이기 전에 계산한다.

[v0.46 측정 결과와 한계](../../docs/design-v0.46/HANDOFF.md) · [26,880회 전체 표](../../docs/design-v0.46/balance-results.md)
