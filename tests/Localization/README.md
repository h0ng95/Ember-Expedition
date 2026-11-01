# Localization regression checks

From the repository root:

```powershell
node tools/localization/build.cjs --check
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Localization/run.ps1
```

The test compiles the shared Core sources with the Windows .NET Framework compiler. It checks both language tables, static models after a live language change, legacy preferences, unchanged run saves, and twelve pairs of battles initialized from identical snapshots. The battle comparisons test locale independence, not player win rates or balance.

`Expedition.EditorTools.LocalizationValidation.Validate` adds Unity JSON migration/round-trip and speech-bubble bounds checks using the game's font. `ValidateAndBuild` runs those checks and builds the Android development APK.

The optional development-only `LocalizationAudit` is activated by a one-use `localization-audit.flag` in the app's persistent data directory. It is not available in release builds. `auto` uses synthetic UI fixtures, blocks account/run writes, checks both languages and captures screens. `touch` and `verify` exercise a real English preference save and process restart; they require backing up and restoring the device's PlayerPrefs and save slots externally. Never run those modes against unbacked-up player data.

Device checks report text bounds as well as screenshots. Inspect screenshots separately: a passing text calculation does not prove every layout is clear or every sentence is natural English.
