# Equal-investment hybrid comparison

Run `./tests/HybridValue/run.ps1 -ReportPath .local-tools/paired.json` at the repository root. This compiles the current combat code and writes per-case results. `-KeepBinary` optionally keeps the executable for a later baseline comparison; use its matching catalog JSON.

## Controlled variables

The matrix contains H-05/H-06 × 11 encounters (9, 10, 14, 15, 19, 20, 24, 25, 28, 29, 30) × four background teams × four recipe branch pairs × three placements × two research/tactic states: **2,112 paired cases**. Each case compares:

1. Three common members plus the two recipe members; an already-owned basic reserve remains benched.
2. The same three common members plus the combined unit; the reserve remains benched.
3. The combined team with that same reserve occupying the consumed member's position.

All forms are created by actual `Recruit`, `Transfer`, and `Evolve` calls. Each branch costs three recruits; the hybrid consumes two branches (six recruits). The same capture is restored for every arm. `Combine` must preserve total recruited supply. Common units, inherited traits (none), formation, encounter, and research are fixed. The reserve is B-AR when a common priest exists, otherwise B-PR; it is paid for in **all** arms. No free post-combination summon is added.

Common progression is deliberately controlled: encounters 9–10 use a branch tank and two basics; 14–15 a final tank and two branches; 19–20 two finals and a branch; later encounters three finals. Recipe branches stay at the same investment. Placement uses compact, spread, and forward anchor arrangements, without optimizing each arm separately.

The researched condition applies all permanent nodes and marksmanship, plus exploitation after wave 11. It therefore measures a combined research/tactic state, not the isolated effect of permanent research. Both arms receive identical effects. Enemy definitions come from the shipped `ExpeditionRun` encounters, including boss patterns. A test-only template advances with simulated wins to obtain those definitions; **this is not legal acquisition history or a full-run completion test**.

`equivalentSummonGold` is nominal `(supply - 3) × 20`, crediting three starting members. It excludes tactic and permanent-research costs. It does not assert those exact families can be recruited for that price under every seed.

## Metrics and interpretation

- Win, duration, surviving count and remaining total HP fraction.
- Damage is effective enemy HP loss plus shield absorption, excluding overkill. Healing is actual restored HP, not nominal casts. Protection is shield damage actually blocked.
- Opening DPS and average common-member HP fraction use the first `min(20 seconds, battle duration)`. Death remains zero HP in the latter average.
- Full-fight healing/protection depend on fight duration and damage pressure. A bigger healing total alone is not evidence of a stronger build.
- First death is capped at 75 seconds when nobody dies. It is censored, not an observed death time.
- Every 211th case is repeated for determinism. This adds 11 verification battles outside the 6,336 experimental battles.
- Recipe branches can produce the same hybrid state. Paired rows are design scenarios, not 2,112 independent random samples; do not calculate confidence intervals as if they were.

Compare before/after only when case IDs, supply, and all separate-arm outputs match. Use the separate `tests/ExpeditionRun/run-end-to-end-balance.ps1` for actual randomized recruitment and failure termination. Neither automation estimates human win rates.
