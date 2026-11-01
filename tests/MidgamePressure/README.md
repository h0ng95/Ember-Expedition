# Midgame pressure probe

Run from the repository root with Windows PowerShell and the .NET Framework C# compiler.

```powershell
& tests/MidgamePressure/run.ps1 -FirstSeed 67101 -Count 2 -Output '.local-tools/v067-pressure'
& tests/MidgamePressure/run.ps1 -FirstSeed 67101 -Count 2 -GrowthOrder OffenseBalanced -Output '.local-tools/v067-offense'
& tests/MidgamePressure/run.ps1 -FirstSeed 67501 -Count 2 -SelectedOnly -Output '.local-tools/v067-holdout'
& tests/MidgamePressure/run.ps1 -FirstSeed 67501 -Count 2 -Production -Output '.local-tools/v067-production'
```

Each run uses four starters, seven evolution/combination policies, three research paths, two research spending policies and the requested seeds. It starts a fresh expedition, spends actual gold through production APIs, and stops after completion or exhausted chances. There are no HP, gold, summon or reward grants.

- Default comparison: legal progression is frozen to rules 5 (`normal-052`). At the first naturally reached waves 15, 18 and 19, replay the same saved party with one placement or enemy stat change. Isolated results never feed rewards or progress back into a route.
- `-SelectedOnly`: compare that saved party with adopted rules 6 (`normal-067`), using separate seeds from the candidate search.
- `-Production`: play rules 6 from start to finish. No isolated candidates. This measures subsequent route effects, which cannot be inferred from a single encounter replay.
- `Balanced` retains the historical investment order. `OffenseBalanced` branches healers and ranged attackers before completing extra final priests. Neither is a solved optimal player.
- Research `early` spends on available path nodes from wave 6; `delayed` starts at wave 11 and buys at most one node per preparation. These specific greedy policies do not represent all early/late research choices.
- Starting placement is spread, and changes to compact after the first lost chance. Explicit compact/spread comparisons are additionally applied to identical anchors.
- Combat steps use the production engine's 1/60-second internal tick and the same 75-second limit.

`results.json` separates real progression fights and isolated fights, and records final party contributions, death timing, remaining enemies, incoming damage and shield generation. Generated shield totals include shields that may expire unused; the production report's **protected** column counts only damage actually blocked.

Seeds are deliberately shared across policies. Routes and repeated anchors are correlated; their percentages are **not estimates of human win rates**. Comparing two growth policies also changes which parties reach each wave, so use identical-party candidate pairs for causal stat comparisons.

Do not reuse an output directory for different arguments: saved `legal-*.json` UI fixtures retain the first matching encounter. Delete only that known task output or choose a fresh output path. The first archived `v067-pressure` report predates the extra `acolyte-power95` exploratory candidate; other candidates and progression policies are unchanged.

Unity validation: `Expedition.EditorTools.MidgameFlowValidation.ValidateAndBuild` checks all 30 old/new encounter definitions, score history migration, and four legal outcome fixtures, then builds Android. Its forced wins only reach definitions; they are not included in balance results. `MidgameReportAudit` is development-only and requires an explicit one-shot flag, blocks account writes, renders the real production UI, and restores original screen fields in `finally`.
