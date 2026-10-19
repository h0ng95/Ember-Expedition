# Independent catalog review

Run from the repository root on Windows:

```powershell
powershell -ExecutionPolicy Bypass -File tests/CatalogCombat/Review/run-review.ps1
```

These probes were written independently of the production implementation during Astra review. They compile the current `Catalog*.cs` sources and load the same runtime catalog as Unity. Each executable is built separately, returns a nonzero exit code on failure, and is removed after execution.

| Probe | Checks | Coverage |
| --- | ---: | --- |
| RosterReview | 64 | Six fusion recipes with all four branch pairs, fusion Undo, late summon preservation, staged growth, all twelve retained final branches |
| BattleReview | 10 | Armor and multiplicative reduction, additive damage bonuses, self support, inherited Harmony, missing heal target, HP tie ordering |
| EdgeReview | 16 | Volley timing, enemy catalog skills, independent hybrid trait cooldowns, actual shield credit, duplicate traits, on-hit conditions, command consumption, expiry and lethal shield break, co-located self support |
| TimingReview | 19 | Three burn ticks and refresh, dynamic zone targets and caster death, burn kill ownership, execution of all twelve final branch selections |
| **Total** | **109** | |

Some probes use reflection and internal state to create precise combat boundary conditions. These supplement normal end-to-end combat tests; they do not measure Unity rendering, input, frame rate, or full-run gameplay balance. The separate balance runner repeats the seeded formation experiments in `../run-balance.ps1`.
