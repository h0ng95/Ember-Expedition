# Unit information and growth projections

Run `./tests/UnitPreview/run.ps1` from the repository root.

The suite checks 306 valid cases against the real evolution/combination and battle factories: 30 forms, 12 branch choices, 12 retained final branches, six hybrid recipes, both recipe directions, four deployment arrangements and three research/tactic states. It also checks rejected material protection/unready growth and verifies the original full save (including RNG and Undo) is unchanged.

These are QA fixtures with simulated victory progression and prepared roster forms. They validate information/projections, not progression balance or win rate.

`Expedition.EditorTools.UnitPreviewValidation.ValidateAndBuild` adds Unity font, art, presentation text and cache checks, writes disposable UI checkpoints to ignored `unity/Logs/v041-fixtures/`, then builds Android.

When changing combat skills or traits, update `CatalogUnitInfo` alongside executable rules. Cooldown text reads `CatalogBattle.SkillCooldown`; other explanatory coefficients and conditions require review against `CatalogSkills` and `CatalogBattle`.
