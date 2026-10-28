# 15 / 30 battle gates

Run `./tests/BossGate/run.ps1` from the repository root. Defaults: seeds 51101–51104 × four starting formations × seven growth policies × three research branches × early/delayed purchase = 672 legal expeditions.

Preparation uses existing legal summon, transfer, evolution, fusion, research and placement operations. Losses consume lanterns and do not award victory rewards. The route ends at exhaustion or completion; it never forces a victory. Growth order is fixed to Balanced. Research purchase timing is a policy, not a claim of optimal play.

At the first actually reached wave 15/30 preparation, the unchanged party also fights isolated comparisons:

- Compact placement instead of its current placement. No additional spending or growth.
- Wave 15: wave 14 enemies, without awarding a reward. This measures the gate with equal investment, not a playable second path.
- Wave 30: the previous v0.48 melee dragon rules instead of v0.50 ranged rules.

Only the current-rule main fight affects the live route. `actualBattles` and `isolatedBattles` are separate. Samples are conditional on reaching each gate; the two gates have different denominators. Several policies share each seed, so these are 672 scenarios, not 672 independent random seeds or a human win-rate estimate.

The first legal gate snapshots are written for repeatable visual QA. They contain generated game state only. They are not phone/account backups. Results and grouped counts are archived in `docs/design-v0.51/`.
