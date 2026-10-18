# Combat core smoke checks

From the repository root on Windows:

```powershell
./tests/CombatCoreSmoke/run-smoke.ps1
```

The runner compiles the real `CombatSimulation.cs` and `Program.cs` together using
the installed Windows .NET Framework compiler. It requires neither Unity nor a
.NET SDK and downloads no packages. Its temporary executable is removed after
the run.

Covered behavior:

1. HP changes only when an impact emits `Hit`; ranged attacks complete windup and
   travel before impact. Event positions match their source/target.
2. Dead targets cannot receive stale projectile hits, emit defeat once, and are
   replaced by living targets.
3. A target leaving melee range during windup cancels the pending attack.
4. One or two fixed 1/60 steps per render frame produce identical event logs and
   final states. The example 750 HP enemy encounter ends at 16.37 simulation
   seconds in both schedules.
5. Identical spawn positions separate without NaN positions or leaving bounds.
6. An already-finished battle emits its result once and does not advance time.

These checks cover combat logic. They do not measure Unity rendering, Android
performance, input, audio, installation, or gameplay balance.

## Presentation contract

- Call `Step(1f / 60f)` for each simulation tick; implement 2x by scheduling more
  ticks. Pause by stopping simulation ticks.
- `AttackProgress` is 0–1 during windup and zero otherwise.
- `Range` measures additional reach beyond both actors' body radii.
- `Cooldown` starts after melee impact or projectile release.
- The nearest living enemy is selected when a target is missing or dead, then
  remains locked until invalid. The current prototype has no taunt or retreat AI.
- A launched projectile tracks its original target. It is removed if that target
  dies; it does not transfer damage to a new target.
- `ProjectileLaunched.X/Y` is the source position. `Hit.X/Y` is the target's
  position at impact. `Hit.Damage` is actual HP lost, clamped to remaining HP.
- Events are dispatched synchronously during `Step`. HP has already changed when
  `Hit` is delivered; a fatal `Hit` is followed by `Defeated`.
- `BattleEnded` has no source/target ID. Read `Winner`; it is null if neither team
  has survivors.
- Use new `CombatUnit` objects for a new fight and keep definitions unchanged
  during a fight. Unit IDs must be unique.
- Movement has simple pair separation and bounds at x [-7, 7], y [-2.8, 1.3]. It
  does not implement navigation around obstacles or physically blocking tanks.
