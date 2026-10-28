# Crown Drake spacing regression

Run `./tests/DrakeSpacing/run.ps1` from the repository root with PowerShell and the Windows .NET Framework C# compiler.

The suite covers rules 0–4, three approach angles, small and batched simulation steps, close-range targets, frontline death/retargeting, normal and enraged breath/bomb/landing cycles, stable aim after warning, unrelated bosses, fresh saves and legacy saves. Current basic attacks use short firebolts at 3.2 range; old rules retain 1.1 melee range.

`qa-roster-v048.json` is an anonymous, generated visual QA account: a fixed seed was advanced to wave 30 with forced victories and legal summon/growth operations. It contains no user account or device data. Its 18 placement/strength/frontline variants are tested under both versions for 36 paired encounter diagnostics. This is not a live progression sample or an estimate of human win rate. Results are written to `docs/design-v0.50/spacing-checks.json`.
