# Verified NORMAL rankings

Development deployment: `IgnisRankings` Cloud Code C# module, `normal_r6_v1` leaderboard, UGS `development` environment. No paid plan or billing setup was enabled by this change. Production has not been configured.

## Trust boundary

- `StartRun` verifies the caller's Google-linked identity via Authentication, issues a cryptographic random seed, and stores the current ticket in Cloud Save **Protected** data. The owner is always `IExecutionContext.PlayerId`.
- `SubmitRun` takes only a bounded action transcript. It loads the server ticket, replays the shared `Expedition.Core` and embedded catalog at 60 Hz with the same 75-second battle timeout, then computes the score. It never accepts the client's score, roster snapshot, player ID, or board ID as authority.
- A compare-and-swap protects the accepted submission; the proof hash and accepted score make retries safe after a lost response. The leaderboard uses descending order and `keepBest`.
- `GetRanking` also requires Google. Only generated aliases, rank, and score leave the module; Google names/emails and authentication IDs are not returned in the ranking page.
- `Config/rankings.ac` denies **all Player access to Leaderboards**. Module calls use `ServiceToken`. Do not deploy only the module while leaving direct player score writes open. Default Cloud Save backup remains client-writable and is not evidence for rankings.

There is one active ticket per account/rules version. A new online start supersedes an unfinished ticket. A finished current run is submitted before the app requests a new online start. Local and old runs remain personal records. Gameplay proofs are saved with the current run and cloud backup, but omitted from history copies to bound backup size.

## Build, test, deploy

Use .NET SDK 8 or later and UGS CLI 2.0.0. In this workspace the SDK and CLI are installed under ignored `.local-tools/`; nothing secret is committed.

```powershell
dotnet run --project tests/RankedRuns/RankedRuns.csproj
dotnet publish server/Rankings/Rankings.csproj -c Release -o server/Rankings/bin/publish
Compress-Archive -Path server/Rankings/bin/publish/* -DestinationPath server/Rankings/bin/IgnisRankings.zip -Force
Copy-Item server/Rankings/bin/IgnisRankings.zip server/Rankings/bin/IgnisRankings.ccm -Force
ugs login --unity-hub
ugs deploy server/Config server/Rankings/bin/IgnisRankings.ccm -p 42dff428-51fe-4602-b9ae-713a582b9a8e -e development --dry-run
ugs deploy server/Config server/Rankings/bin/IgnisRankings.ccm -p 42dff428-51fe-4602-b9ae-713a582b9a8e -e development
```

Do not use `--reconcile` for this partial deployment. Check existing access policies before updating. Change the board/ticket version when score-affecting rules change; old proofs must not be replayed with new balance data.

Unity validation/build entry point: `Expedition.EditorTools.RankingValidation.ValidateAndBuild`.

Development-only phone test: place an empty `ranking-probe.request` in this game's `Application.persistentDataPath`, then restart the app with its normal Google session. `RankingDevelopmentProbe` uses the ordinary authenticated SDK, runs a separate simulated expedition and writes a development leaderboard score. It does not read private credential files or alter the player's current expedition/cloud backup. Read `ranking-probe-result.json` afterward. This probe is compiled out of non-development builds and must not be run against production. It supersedes the development account's current ranked ticket, so do not run it while testing a real ranked expedition.

## Limits before release

- Replay rejects impossible actions/results, not bots or external seed searching. Seeds are visible to the game for offline simulation. This is not complete anti-cheat.
- Command limit 2,048, proof JSON limit 256 KiB, replay CPU budget 6 seconds. Local play continues if a proof overflows, but that run cannot be submitted.
- First ticket creation has no existing write lock. Two concurrent first starts may supersede one ticket; only the stored ticket can submit. Two-device behavior and explicit stale-ticket recovery need further testing.
- The server records the accepted score before publishing to the board. If publication fails, retry the same transcript. Do not replace that ticket until retry succeeds.
- Add per-account abuse controls, spending/usage monitoring, production OAuth/signing configuration, account deletion and leaderboard deletion before a public launch. Refresh is limited to once per 30 seconds in the client, but that is not a security quota.
- Different platforms/versions may disagree on floating-point battle outcomes. Android IL2CPP and .NET server matched in the device smoke run, not an exhaustive 30-battle cross-platform audit. Do not loosen verification to accept client victories if a mismatch is found.

References: [Cloud Code modules](https://docs.unity.com/en-us/cloud-code/modules), [server access control](https://docs.unity.com/en-us/cloud-code/3.0.0/server-access-control), [UGS CLI authentication](https://docs.unity.com/en-us/services-cli/2.0.0/manual/general/get-started/get-authenticated).
