# Genuine v3 run saves

These six JSON files were generated from the committed Core at `97c916d` (v0.26), before `Difficulty` and `ContentWaves` were added. The writer used `ExpeditionRun.New(catalog, 27)`, the public run lifecycle methods, and .NET `JavaScriptSerializer` on `Capture()`.

- `preparation`: new run, three starting allies, 40 gold.
- `battle`: first battle started.
- `result-win`: first victory resolved, reward received.
- `result-loss`: first victory continued, second battle lost.
- `defeated`: second battle lost three times, no lanterns remain.
- `completed`: fifteen victory resolutions followed by `Continue()`.

The completion fixture exercises accepted battle results, not simulated combat wins. These are serializer/migration fixtures, not difficulty evidence. The pure C# tests and `ExpeditionProgressValidation` both load the original field shape rather than manufacturing v3 JSON from the new v4 type.

## Genuine v7 run saves

`v7-*.json` were generated with `ExpeditionRun.cs` from commit `f430538` (v0.31), before tactics, research snapshots and starting formations were added. The writer used seed 1367, two summons and public result/continue methods to reach battle 30. Preparation includes a real move undo; the other five snapshots cover battle, victory result, defeat result, completion and exhausted lanterns.

These six fixtures preserve the original v7 serializer field shape. They test migration and reward idempotency, and do not claim that the basic party can win thirty battles.
