# End-to-end expedition balance probe

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/ExpeditionRun/run-end-to-end-balance.ps1 -FirstSeed 1 -Count 20 -GrowthOrder Balanced -Spending all -ReportPath .local-tools/v033-end-to-end.json
```

The probe uses `ExpeditionRun.New`, legal summons, transfers, evolutions, combinations, deployment, tactic purchases, `BeginBattle`, `ResolveBattle`, and `Continue`. It saves and restores every preparation. A defeat spends a lantern and awards no gold. The next attempt uses compact placement; the following attempt uses spread placement. An unfinished battle at 75 seconds is a loss, matching the expedition lab. The run ends on defeat or wave 30 completion. It verifies that repeating a seed gives the same result.

`-Policy` can select `final` or one of `H-01` through `H-06`. `-Spending` can select `none`, `early` (buys all affordable generic nodes as soon as available), `delayed` (one generic node per preparation from wave 11), or `selective` (one role-matched base node when a full party is deployed). `-GrowthOrder` is `Balanced` by default; `TargetFirst` and `FrontFirst` are available for comparison. All strategies are fixed automated policies. Their outcomes are diagnostics, not player win-rate estimates.

`rows` counts true completed runs and waves reached. `completedWithHybrid` counts only runs that both deployed the target H form and completed wave 30; `completedWithoutHybrid` keeps the remaining completions visible. `seedResults` retains the full first-seed trace for each policy; other seeds retain losses to keep the report bounded. The separate `HybridBalanceProbe` deliberately assumes victory rewards after a loss for later-wave comparisons, so its per-wave wins must not be read as completed runs.
