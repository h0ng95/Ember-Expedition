# Account checkpoint validation

Run `./tests/AccountSave/run.ps1` at the repository root. This compiles the production Core code and writes isolated test saves under the ignored `.local-tools/` directory.

The test covers 90 battle/preparation/result checkpoints across 30 encounters, reward idempotency on reload, research persistence, mismatched account components, five truncated-write boundaries, missing slots, invalid content with a valid checksum, corruption of both slots, and unsupported future versions. Preference range/serialization checks are included. Victories are supplied as fixtures to test saving, not to measure combat balance.

`Expedition.EditorTools.AccountSettingsValidation.ValidateAndBuild` additionally exercises Unity JsonUtility and the actual screen's legacy migration, save/load, blocked overwrite, research reward and settings paths. It preserves the Editor's old PlayerPrefs and uses unique test directories in `unity/Logs/`.

The store alternates complete snapshots in two files and flushes each before verifying it. A write can lose the most recent action if it is interrupted, but does not overwrite the previous valid generation. SHA-256 detects accidental damage; it does not authenticate player scores. Deleting app data, uninstalling, loss of the device, and corruption of both files require a separate external backup/cloud design.
