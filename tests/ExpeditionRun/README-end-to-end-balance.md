# End-to-end expedition balance probe

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/ExpeditionRun/run-end-to-end-balance.ps1 -FirstSeed 1 -Count 20 -GrowthOrder Balanced -Spending all -ReportPath .local-tools/v033-end-to-end.json
```

The probe uses `ExpeditionRun.New`, legal summons, transfers, evolutions, combinations, deployment, tactic purchases, `BeginBattle`, `ResolveBattle`, and `Continue`. It saves and restores every preparation. A defeat spends a lantern and awards no gold. The next attempt uses compact placement; the following attempt uses spread placement. An unfinished battle at 75 seconds is a loss, matching the expedition lab. The run ends on defeat or wave 30 completion. It verifies that repeating a seed gives the same result.

`-Policy` can select `final` or one of `H-01` through `H-06`. `-Spending` can select `none`, `early` (buys all affordable generic nodes as soon as available), `delayed` (one generic node per preparation from wave 11), or `selective` (one role-matched base node when a full party is deployed). `-GrowthOrder` is `Balanced` by default; `TargetFirst` and `FrontFirst` are available for comparison. All strategies are fixed automated policies. Their outcomes are diagnostics, not player win-rate estimates.

`rows` counts true completed runs and waves reached. `completedWithHybrid` counts only runs that both deployed the target H form and completed wave 30; `completedWithoutHybrid` keeps the remaining completions visible. `seedResults` retains the full first-seed trace for each policy; other seeds retain losses to keep the report bounded. The separate `HybridBalanceProbe` deliberately assumes victory rewards after a loss for later-wave comparisons, so its per-wave wins must not be read as completed runs.

Since v0.34, `-Account fresh` (default) starts without permanent research. `-Account researched` uses all six currently available nodes, costing 27 earned points. `-Account all` compares these two profiles. Since v0.40, `-Account unlocked` uses only the three starter unlocks (9 points, no stat bonuses). `-Starter` accepts `standard` (default), `tactical`, `ember`, or `frost`. Alternative starters require `unlocked` or `researched`; the real unlock validation is retained. These are explicit starting-account fixtures, not a simulation of earning research over successive runs. The profile and starter snapshot are checked after each save/restore.

Since v0.40, `-Interruptions` additionally serializes to JSON and restores preparation, battle, result, and next-wave state. Each interrupted battle restarts from saved preparation, then its exact final actors/time are compared to an uninterrupted 60 Hz control. It checks rejected actions during combat, one-time gold/lantern settlement, duplicate Resolve/Continue rejection, account research and journal persistence, and terminal-state restrictions. JSON object key order is normalized for comparison; array order and all values remain significant. Unity's actual `JsonUtility` is covered separately by `Expedition.EditorTools.ExpeditionRunValidation.Validate`. `boundaryChecks` includes the first-seed determinism reruns; `seedResults` excludes those reruns.

```powershell
./tests/ExpeditionRun/run-end-to-end-balance.ps1 -FirstSeed 41001 -Count 2 -Account all -Policy all -Spending all -Interruptions -ReportPath .local-tools/journey-interruptions.json
./tests/ExpeditionRun/run-end-to-end-balance.ps1 -FirstSeed 43001 -Count 6 -Account unlocked -Starter frost -Policy all -Spending all -ReportPath .local-tools/journey-frost.json
```

Since v0.35, retained attempt traces for waves 23–30 include `firstAllyDefeated`, actual HP damage grouped by source/target, actual healing, and absorbed shields credited to their provider. `outgoingBySource` is HP damage only; unlike the in-game report, it does not include shield absorption. Power/haste buff contribution is not separately attributed. These passive event observers do not change targeting, RNG, simulation speed, or outcomes. Compare formation and roster together: retries can also spend remaining gold, so not every spread/compact pair is an isolated formation experiment.

`-Policy focus` selects the final-only control and H-01/H-05. `waves` aggregates all attempts before traces are trimmed: `reached` is the number of runs that reached that wave, `firstWins` counts wins on their first attempt, `wins` includes retries, and `timeouts` counts attempts reaching the 75-second limit. These are conditional rates; later-wave samples exclude earlier defeats. Hybrid shield absorption and skill casts are also recorded, without changing the simulation.

```powershell
./tests/ExpeditionRun/run-end-to-end-balance.ps1 -FirstSeed 34001 -Count 24 -GrowthOrder Balanced -Policy focus -Spending all -Account all -ReportPath .local-tools/v034-holdout.json
```
