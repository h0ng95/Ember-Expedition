# Dragon basic attack pressure

From the repository root:

```powershell
./tests/DrakePressure/run.ps1
./tests/DrakePressure/run.ps1 -FirstSeed 52101 -Count 8 -Output '.local-tools/v052-holdout'
./tests/DrakeSpacing/run.ps1
```

Uses the same legal preparation policies as `tests/BossGate`: four starters × seven growth goals × three research branches × two spending timings per seed. Routes are explicitly frozen to v050, even after a new balance release. Only the first actually reached wave-30 party is an anchor. No forced wins or extra rewards. Isolated comparisons never change the route. Growth goals do not guarantee acquiring their target hybrid.

Six candidates share the exact party, research, growth and placement. `BasicPowerScale` affects only the basic attack; boss base Power, breath, bombing and landing are unchanged. AttackInterval candidates affect only basic cooldown. Damage totals include shield absorption; longer survival changes the number of patterns seen.

Tuning seeds 51101–51104 reproduce v051's 220 reached parties and 112 current wins. Before inspecting holdout results, `power90` was selected: 139/220 wins, versus 182–189/220 for interval-only alternatives. Holdout seeds 52101–52108 supply 378 other reached parties: 207 current wins, 263 power90 wins. There are 57 gained wins and one lost win (timeout with 0.38% boss HP). These are fixed policy scenarios sharing seeds, not independent player samples.

The selected 0.90 basic coefficient ships in rules 5 / save v13 / `normal-052`, with the original 1.8 cooldown. Existing rules-4 runs retain 1.0. History schema v5 keeps previous best scores in their original balance category. The spacing regression also checks basic-only damage, unchanged breath/bomb/landing damage, legacy continuation and history migration. Its current report is written to `.local-tools/v052/spacing-checks.json`; archived v050 results are not overwritten.

Reports: `docs/design-v0.52/{tuning,holdout}.json` and their summaries. The holdout also records other predeclared candidates for reproducibility; they were not used to replace the tuning choice. These results do not establish final release difficulty.
