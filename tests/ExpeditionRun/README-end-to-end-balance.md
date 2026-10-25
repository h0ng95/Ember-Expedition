# End-to-end expedition balance probe

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/ExpeditionRun/run-end-to-end-balance.ps1 -FirstSeed 1 -Count 20 -GrowthOrder Balanced -Spending all -ReportPath .local-tools/v033-end-to-end.json
```

The probe uses `ExpeditionRun.New`, legal summons, transfers, evolutions, combinations, deployment, tactic purchases, `BeginBattle`, `ResolveBattle`, and `Continue`. It saves and restores every preparation. A defeat spends a lantern and awards no gold. The next attempt uses compact placement; the following attempt uses spread placement. An unfinished battle at 75 seconds is a loss, matching the expedition lab. The run ends on defeat or wave 30 completion. It verifies that repeating a seed gives the same result.

`-Policy` can select `final` or one of `H-01` through `H-06`. `-Spending` can select `none`, `early` (buys all affordable generic nodes as soon as available), `delayed` (one generic node per preparation from wave 11), or `selective` (one role-matched base node when a full party is deployed). `-GrowthOrder` is `Balanced` by default; `TargetFirst` and `FrontFirst` are available for comparison. All strategies are fixed automated policies. Their outcomes are diagnostics, not player win-rate estimates.

`rows` counts true completed runs and waves reached. `completedWithHybrid` counts only runs that both deployed the target H form and completed wave 30; `completedWithoutHybrid` keeps the remaining completions visible. `seedResults` retains the full first-seed trace for each policy; other seeds retain losses to keep the report bounded. The separate `HybridBalanceProbe` deliberately assumes victory rewards after a loss for later-wave comparisons, so its per-wave wins must not be read as completed runs.

Since v0.34, `-Account fresh` (default) starts without permanent research. `-Account researched` uses all six currently available nodes, costing 27 earned points. Both use the standard three-unit starter to isolate research stat effects. `-Account all` compares the two profiles; it does not simulate earning research over successive runs or evaluate the three alternative starters. The profile is included in every result and the research snapshot is checked after each save/restore.

Since v0.35, retained attempt traces for waves 23–30 include `firstAllyDefeated`, actual HP damage grouped by source/target, actual healing, and absorbed shields credited to their provider. `outgoingBySource` is HP damage only; unlike the in-game report, it does not include shield absorption. Power/haste buff contribution is not separately attributed. These passive event observers do not change targeting, RNG, simulation speed, or outcomes. Compare formation and roster together: retries can also spend remaining gold, so not every spread/compact pair is an isolated formation experiment.

`-Policy focus` selects the final-only control and H-01/H-05. `waves` aggregates all attempts before traces are trimmed: `reached` is the number of runs that reached that wave, `firstWins` counts wins on their first attempt, `wins` includes retries, and `timeouts` counts attempts reaching the 75-second limit. These are conditional rates; later-wave samples exclude earlier defeats. Hybrid shield absorption and skill casts are also recorded, without changing the simulation.

```powershell
./tests/ExpeditionRun/run-end-to-end-balance.ps1 -FirstSeed 34001 -Count 24 -GrowthOrder Balanced -Policy focus -Spending all -Account all -ReportPath .local-tools/v034-holdout.json
```
