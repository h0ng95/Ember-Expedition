# Expedition learning and summary checks

Run `./tests/ExpeditionLearning/run.ps1` from the repository root. This uses the Windows .NET Framework compiler and deletes its temporary executable when finished.

The test checks eligible reserve donors and deployed recipients, material locks, evolution thresholds, tactic availability, existing-player guide state, and absence of mutations while finding examples. It also covers persisted battle speed and terminal gold/research totals across prior-best records 0–30, repeated progress observation, and save restoration.

Version 8 saves retain their version and add optional fields. Missing or invalid speed becomes 1×; missing or invalid research-baseline metadata makes per-run earned research unknown. Existing progress rewards are unchanged.

For Unity UI and APK verification, run `Expedition.EditorTools.ExpeditionLearningValidation.ValidateAndBuild`. Its exported `unity/Logs/v042-fixtures` checkpoints are for UI QA, not balance or completion-rate evidence. Back up and restore device PlayerPrefs when using these checkpoints.
