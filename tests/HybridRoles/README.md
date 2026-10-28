# Six hybrid role audit

Run from the repository root:

```powershell
./tests/HybridRoles/run.ps1 -ReportPath .local-tools/v053/baseline.json -KeepBinary .local-tools/v053/baseline.exe
./tests/HybridRoles/run.ps1 -ReportPath .local-tools/v053/holdout.json -Matrix holdout
node tests/HybridRoles/summarize.cjs docs/design-v0.53 .local-tools/v053/baseline.json .local-tools/v053/holdout.json
```

Create the parent of `KeepBinary` first when using a fresh folder. Default rules: 5 (`normal-052`). Filter individual recipes with `-Hybrids 'H-05,H-06'`.

Uses the real C# combat, recipe and enemy encounter implementations. Three cloned arms have identical purchased supply, reserve and region-legal research:

1. Three common units + two branch materials, reserve benched.
2. Same units, materials combined into one hybrid, reserve still benched (4 deployed).
3. Same combined squad, existing reserve deployed in the consumed unit's position (5 deployed).

Recruit/Transfer/Evolve/Combine use actual roster operations; each material costs 3 recruits. The reserve is paid for in every arm. Summon-equivalent gold is only a nominal material cost, excluding research; it does not prove that the roster was acquired on that wave. Research cost is equal within each paired case. No inherited traits or permanent research are used.

Main: 6 recipes × 11 encounters × 4 common teams × 4 branch pairs × 2 layouts × 4 research policies = **8,448 cases / 25,344 fights**.

Holdout: 6 recipes × 8 encounters × 3 different common teams × 4 branch pairs × 1 forward layout × 4 research policies = **2,304 cases / 6,912 fights**. This is a different composition matrix, not unseen random recruitment seeds. The decision to retain combat values was made after main results and checked against holdout results.

Main waves: 9, 10, 14, 15, 19, 20, 24, 25, 28, 29, 30. Holdout: 8, 12, 16, 18, 22, 26, 27, 30. There are 7 distinct common teams, including teams with no spare knight. Every branch recipe pair is covered. Research: none, defense, precision, arcana; nodes are filtered by RequiredBossWave. Common unit maturity increases in brackets by wave.

Encounters are reached with forced victories **only to obtain their definitions**. These are controlled roster snapshots, not natural acquisition runs or difficulty estimates. Identical hybrid states can recur across branch pairs, so rows are not independent samples and no confidence intervals are inferred. The layouts are fixed, not optimized per fight. The reserve is a base archer or priest, not a universally optimal refill.

Simulation uses the engine's fixed timestep, requested in 0.25s batches. Opening health is sampled every 0.25s; damage events are counted exactly. Opening DPS uses the first 20 seconds (or the battle's duration if shorter). Time limit is 75 seconds; timeouts count as non-wins. First death is censored at 75s if no ally dies. Damage includes absorbed shields but excludes overkill, healing is effective restored HP, protection counts absorbed shield damage rather than shields granted. Every 211th row is repeated for determinism: 41 main + 11 holdout extra fights.

Raw reports are gzip-compressed JSON; summaries include breakdowns by encounter, team, branch, layout and research. They support deciding when fusion is useful, not guaranteeing every fusion or every build wins. Use actual progression probes and new-player observation for acquisition frequency, comprehension and release difficulty.
