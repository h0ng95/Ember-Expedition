using System;
using System.Collections.Generic;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class PalaceEnemyArtValidation
    {
        const string Folder = "Assets/Resources/Art/Enemies/v031";
        const string ManifestFile = Folder + "/palace-manifest.json";
        static int assertions;

        static void Check(bool result, string message)
        {
            assertions++;
            if (!result) throw new InvalidOperationException("Palace enemy art: " + message);
        }

        [MenuItem("Expedition/Palace Enemies/Generate Manifest")]
        public static void GenerateManifest()
        {
            ForestEnemyManifest manifest = ForestEnemyArtValidation.BuildManifest(
                Folder, "0.31", PalaceEnemyArt.Species);
            System.IO.Directory.CreateDirectory(Folder);
            System.IO.File.WriteAllText(ManifestFile, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(ManifestFile, ImportAssetOptions.ForceUpdate);
            Debug.Log("Palace enemy manifest generated: " + ManifestFile);
        }

        [MenuItem("Expedition/Palace Enemies/Validate")]
        public static void Validate()
        {
            assertions = 0;
            ForestEnemyManifest fresh = ForestEnemyArtValidation.BuildManifest(
                Folder, "0.31", PalaceEnemyArt.Species);
            TextAsset saved = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestFile);
            Check(saved != null, "manifest is missing; run Generate Manifest");
            ForestEnemyManifest stored = JsonUtility.FromJson<ForestEnemyManifest>(saved.text);
            Check(stored != null && stored.version == "0.31" && stored.sheets != null &&
                stored.sheets.Length == PalaceEnemyArt.Species.Length,
                "manifest must contain four palace sheets");
            for (int i = 0; i < PalaceEnemyArt.Species.Length; i++)
            {
                ForestEnemySheet expected = fresh.sheets[i], actual = stored.sheets[i];
                Check(actual != null && actual.id == PalaceEnemyArt.Species[i] && ForestEnemyArt.Valid(actual),
                    "mapping or bounds for " + PalaceEnemyArt.Species[i]);
                Check(actual.resourcePath == expected.resourcePath &&
                    actual.width == expected.width && actual.height == expected.height &&
                    actual.referenceHeight == expected.referenceHeight &&
                    Mathf.Abs(actual.anchorX - expected.anchorX) < .01f,
                    "dimensions, resource, scale, or center changed for " + actual.id);
                for (int cut = 0; cut < 5; cut++)
                    Check(actual.rowCuts[cut] == expected.rowCuts[cut], actual.id + " row seam " + cut);
                for (int cut = 0; cut < 28; cut++)
                    Check(actual.columnCuts[cut] == expected.columnCuts[cut], actual.id + " column seam " + cut);
                for (int frame = 0; frame < 24; frame++)
                {
                    ForestEnemyFrame a = actual.frames[frame], b = expected.frames[frame];
                    Check(a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height,
                        actual.id + " frame " + frame + " is stale or empty");
                }
                Check(PalaceEnemyArt.SilhouetteHeight(actual.id) > 0f &&
                    actual.referenceHeight == actual.frames[0].height,
                    "missing fixed species scale");
            }
            ValidateMapping();
            ValidateFrames();
            ValidateTelegraph();
            var art = new ForestEnemyArt();
            try
            {
                Rect boss, guard;
                bool gotBoss = art.TryGetBounds(PalaceEnemyArt.Species[3], 0, Vector2.zero, out boss);
                bool gotGuard = art.TryGetBounds(PalaceEnemyArt.Species[0], 0, Vector2.zero, out guard);
                Check(gotBoss && gotGuard && boss.width > guard.width &&
                    Mathf.Abs(boss.height - 340f) < .1f,
                    "palace boss keeps the large fixed silhouette scale");
            }
            finally { art.Release(); }
            Debug.Log("Palace enemy art validated: " + assertions + " assertions, four sheets.");
        }

        static void ValidateMapping()
        {
            CatalogDatabase database = CatalogDatabase.FromJson(
                Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var battle = new CatalogBattle(database);
            CatalogBattleUnit guard = battle.AddEnemy("palace-guard-1", 100, 10, 0, 1, 1, 1,
                false, 0, 0, CatalogEnemyRole.MineWorker);
            CatalogBattleUnit arbalist = battle.AddEnemy("palace-arbalist-1", 100, 10, 0, 1, 3, 1,
                false, 0, 0, CatalogEnemyRole.Raider);
            CatalogBattleUnit sentinel = battle.AddEnemy("palace-sentinel", 100, 10, 0, 1, 1, 1,
                false, 0, 0, CatalogEnemyRole.Supervisor);
            CatalogBattleUnit boss = battle.AddEnemy("palace-boss", 100, 10, 0, 1, 1, 1,
                true, 0, 0, CatalogEnemyRole.CrownDrake, true);
            Check(PalaceEnemyArt.SpeciesFor(guard) == PalaceEnemyArt.Species[0], "guard mapping");
            Check(PalaceEnemyArt.SpeciesFor(arbalist) == PalaceEnemyArt.Species[1], "arbalist mapping");
            Check(PalaceEnemyArt.SpeciesFor(sentinel) == PalaceEnemyArt.Species[2], "sentinel mapping");
            Check(PalaceEnemyArt.SpeciesFor(boss) == PalaceEnemyArt.Species[3], "boss mapping");
            Check(ForestEnemyArt.SpeciesFor(guard) == null &&
                ForestEnemyArt.SpeciesFor(arbalist) == null &&
                ForestEnemyArt.SpeciesFor(sentinel) == null &&
                ForestEnemyArt.SpeciesFor(boss) == null,
                "palace and forest art routes remain separate");
        }

        static void ValidateFrames()
        {
            string guard = PalaceEnemyArt.Species[0], sentinel = PalaceEnemyArt.Species[2];
            string boss = PalaceEnemyArt.Species[3];
            Check(Frame(guard, 0, 0, 0, 0, 0, 0, CrownDrakePhase.Grounded, 0, 0, null) == 0, "idle frame");
            Check(Frame(guard, 1, .2f, 0, 0, 0, 0, CrownDrakePhase.Grounded, 0, 0, null) >= 6, "walk frame");
            Check(Frame(guard, 2, 0, .2f, 0, 0, .2f, CrownDrakePhase.Grounded, 0, 0, null) == 13,
                "attack windup frame");
            Check(Frame(guard, 2, 0, 0, .18f, 0, 0, CrownDrakePhase.Grounded, 0, 0, "BASIC") == 14,
                "attack release follows damage event");
            Check(Frame(guard, 3, 0, 0, 0, 0, 0, CrownDrakePhase.Grounded, 0, 0, null) == 4,
                "ordinary hit frame");
            Check(Frame(guard, 4, 1f, 0, .18f, 0, 0, CrownDrakePhase.Grounded, 0, 0, null) == 21,
                "death owns terminal frame");
            Check(Frame(sentinel, 3, 0, 0, .18f, 0, 0, CrownDrakePhase.Grounded, 0, 0, "MINE_GUARD") == 22 &&
                Frame(sentinel, 3, 0, 0, .08f, .11f, 0, CrownDrakePhase.Grounded, 0, 0, "MINE_GUARD") == 23,
                "sentinel guard uses dedicated support poses");
            Check(Frame(boss, 2, 0, .1f, 0, 0, .8f,
                CrownDrakePhase.BreathWarning, 0, .8f, null) == 12 &&
                Frame(boss, 2, 0, .2f, 0, 0, .3f,
                CrownDrakePhase.BreathWarning, 0, .3f, null) == 13,
                "ground breath opens before impact");
            Check(Frame(boss, 2, 0, 0, .18f, 0, 0,
                CrownDrakePhase.GroundWindow, 0, 0, "DRACONIC_BREATH") == 14,
                "breath release follows impact event");
            Check(Frame(boss, 0, 0, 0, 0, 0, 0,
                CrownDrakePhase.Rising, .5f, 0, null) == 22 &&
                Frame(boss, 0, .27f, 0, 0, 0, 0,
                CrownDrakePhase.AirWarning, 1f, 0, null) == 23 &&
                Frame(boss, 3, .27f, 0, 0, 0, 0,
                CrownDrakePhase.LandingWarning, .3f, 0, null) == 23,
                "wing poses own rise, air warning, and landing over hit pose");
            Check(Frame(boss, 3, 0, 0, 0, 0, 0,
                CrownDrakePhase.Grounded, 0, 0, null) == 0,
                "boss does not play received-hit pose");
            Check(Frame(boss, 4, 1f, 0, 0, 0, 0,
                CrownDrakePhase.AirWarning, 1f, 0, null) == 21,
                "death overrides flight and returns to ground frames");
        }

        static void ValidateTelegraph()
        {
            CatalogDatabase database = CatalogDatabase.FromJson(
                Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var battle = new CatalogBattle(database);
            CatalogBattleUnit boss = battle.AddEnemy("palace-boss", 4800, 80, 26, 1.8f, 1.1f,
                .48f, true, 2.2f, 0f, CatalogEnemyRole.CrownDrake, true);
            for (int i = 0; i < 5; i++)
                battle.AddUnit("ally-" + i, CatalogTeam.Ally, "F-KN", "E-KN-G", null, -.4f,
                    (i - 2) * .35f);
            var sequence = new List<string>();
            battle.Event += e =>
            {
                if (e.SourceId != boss.Id) return;
                if (e.Kind != CatalogEventKind.BossTelegraph && e.Kind != CatalogEventKind.BossImpact)
                    return;
                sequence.Add((e.Kind == CatalogEventKind.BossTelegraph ? "warn:" : "hit:") + e.AbilityId);
                if (e.Kind == CatalogEventKind.BossTelegraph && e.AbilityId == "DRACONIC_BREATH")
                {
                    Vector2 origin = new Vector2(boss.BreathOriginX, boss.BreathOriginY);
                    Vector2 end = new Vector2(boss.BreathTargetX, boss.BreathTargetY);
                    Check(boss.BreathWarningRemaining > 0f &&
                        Mathf.Abs((end - origin).magnitude - 5.5f) < .01f &&
                        Mathf.Abs(boss.BreathHalfWidth - .65f) < .01f,
                        "breath exposes a full line warning");
                }
                if (e.Kind == CatalogEventKind.BossTelegraph && e.AbilityId == "DRACONIC_BOMB")
                {
                    Check(boss.DrakePhase == CrownDrakePhase.AirWarning && boss.IsAirborne &&
                        boss.SlamWarningRemaining > 0f && Mathf.Abs(boss.SlamRadius - 1.2f) < .01f,
                        "distant bomb warns while dragon is airborne");
                }
                if (e.Kind == CatalogEventKind.BossTelegraph && e.AbilityId == "DRACONIC_LAND")
                {
                    Check(boss.DrakePhase == CrownDrakePhase.LandingWarning &&
                        boss.SlamWarningRemaining > 0f && Mathf.Abs(boss.SlamRadius - 1.55f) < .01f &&
                        Mathf.Abs(boss.SlamTargetX - boss.X) < .01f &&
                        Mathf.Abs(boss.SlamTargetY - boss.Y) < .01f,
                        "landing warns around the dragon's own feet");
                }
            };
            for (int i = 0; i < 360 && sequence.Count < 6 && !battle.Ended; i++)
                battle.Step(.05f);
            string[] expected = { "warn:DRACONIC_BREATH", "hit:DRACONIC_BREATH",
                "warn:DRACONIC_BOMB", "hit:DRACONIC_BOMB",
                "warn:DRACONIC_LAND", "hit:DRACONIC_LAND" };
            Check(sequence.Count >= expected.Length, "dragon completes ground, air, and landing phases");
            for (int i = 0; i < expected.Length; i++)
                Check(sequence[i] == expected[i], "dragon attack order " + i);
            Check(boss.DrakePhase == CrownDrakePhase.Grounded && !boss.IsAirborne,
                "dragon returns to the ground after landing");
        }

        static int Frame(string id, int state, float stateTime, float windupAge,
            float releaseRemaining, float releaseAge, float attackWindupRemaining,
            CrownDrakePhase phase, float flightLift, float breathWarningRemaining,
            string releaseAbilityId)
        {
            return PalaceEnemyArt.FrameIndex(id, state, stateTime, windupAge, releaseRemaining,
                releaseAge, attackWindupRemaining, phase, flightLift, breathWarningRemaining,
                releaseAbilityId);
        }

        public static void BuildAndroid()
        {
            GenerateManifest();
            Validate();
            GardenEnemyArtValidation.BuildAndroid();
        }
    }
}


