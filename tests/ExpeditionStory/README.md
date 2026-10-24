# Expedition story checks

Run `./tests/ExpeditionStory/run.ps1` from the repository root on Windows.

The small C# suite checks the seven ordered story records, regional and boss mapping, journal unlocks after victories and losses, separate persistence across a new run, invalid journal data, copy isolation, and observation of valid version-two ten-wave saves. It uses the real core catalog and run code. Unity's `JsonUtility` round trip is checked by `ExpeditionStoryValidation` in the Editor.
