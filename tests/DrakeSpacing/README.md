# Crown Drake spacing regression

Run `./tests/DrakeSpacing/run.ps1` from the repository root with PowerShell and the Windows .NET Framework C# compiler.

The suite covers rules 0–5, three approach angles, small and batched simulation steps, close-range targets, frontline death/retargeting, normal and enraged breath/bomb/landing cycles, stable aim after warning, unrelated bosses, fresh saves and legacy saves. Current basic attacks use short firebolts at 3.2 range; old rules retain 1.1 melee range. Rules 5 scale only basic damage to 90%; dedicated checks verify unchanged breath/bomb/landing damage and migration of v050 best scores into the archived category.

`qa-roster-v048.json` is an anonymous, generated visual QA account: a fixed seed was advanced to wave 30 with forced victories and legal summon/growth operations. It contains no user account or device data. Its 18 placement/strength/frontline variants are tested under rules 3 and 4 for 36 paired encounter diagnostics. This is not a live progression sample or an estimate of human win rate. Results are written to `.local-tools/v052/spacing-checks.json` (override with `-ReportPath`). Historical v050 results remain in `docs/design-v0.50/spacing-checks.json`. See `tests/DrakePressure/README.md` for the legal-progression v052 comparisons.
