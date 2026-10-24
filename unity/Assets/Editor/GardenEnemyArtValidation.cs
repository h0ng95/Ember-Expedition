using System;
using System.Collections.Generic;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class GardenEnemyArtValidation
    {
        const string Folder = "Assets/Resources/Art/Enemies/v030";
        const string ManifestFile = Folder + "/garden-manifest.json";
        static int assertions;

        static void Check(bool result, string message)
        {
            assertions++;
            if (!result) throw new InvalidOperationException("Garden enemy art: " + message);
        }

        [MenuItem("Expedition/Garden Enemies/Generate Manifest")]
        public static void GenerateManifest()
        {
            ForestEnemyManifest manifest = ForestEnemyArtValidation.BuildManifest(
                Folder, "0.30", GardenEnemyArt.Species);
            System.IO.Directory.CreateDirectory(Folder);
            System.IO.File.WriteAllText(ManifestFile, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(ManifestFile, ImportAssetOptions.ForceUpdate);
            Debug.Log("Garden enemy manifest generated: " + ManifestFile);
        }

        [MenuItem("Expedition/Garden Enemies/Validate")]
        public static void Validate()
        {
            assertions = 0;
            ForestEnemyManifest fresh = ForestEnemyArtValidation.BuildManifest(
                Folder, "0.30", GardenEnemyArt.Species);
            TextAsset saved = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestFile);
            Check(saved != null, "manifest is missing; run Generate Manifest");
            ForestEnemyManifest stored = JsonUtility.FromJson<ForestEnemyManifest>(saved.text);
            Check(stored != null && stored.version == "0.30" && stored.sheets != null &&
                stored.sheets.Length == GardenEnemyArt.Species.Length,
                "manifest must contain four garden sheets");
            for (int i = 0; i < GardenEnemyArt.Species.Length; i++)
            {
                ForestEnemySheet expected = fresh.sheets[i], actual = stored.sheets[i];
                Check(actual != null && actual.id == GardenEnemyArt.Species[i] && ForestEnemyArt.Valid(actual),
                    "mapping or bounds for " + GardenEnemyArt.Species[i]);
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
                Check(GardenEnemyArt.SilhouetteHeight(actual.id) > 0f &&
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
                bool gotBoss = art.TryGetBounds(GardenEnemyArt.Species[3], 0, Vector2.zero, out boss);
                bool gotGuard = art.TryGetBounds(GardenEnemyArt.Species[0], 0, Vector2.zero, out guard);
                Check(gotBoss && gotGuard && boss.width > guard.width &&
                    Mathf.Abs(boss.height - 250f) < .1f,
                    "garden boss keeps the large fixed silhouette scale");
            }
            finally { art.Release(); }
            Debug.Log("Garden enemy art validated: " + assertions + " assertions, four sheets.");
        }

        static void ValidateMapping()
        {
            CatalogDatabase database = CatalogDatabase.FromJson(
                Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var battle = new CatalogBattle(database);
            CatalogBattleUnit guard = battle.AddEnemy("garden-guard-1", 100, 10, 0, 1, 1, 1,
                false, 0, 0, CatalogEnemyRole.MineWorker);
            CatalogBattleUnit spitter = battle.AddEnemy("garden-spitter-1", 100, 10, 0, 1, 3, 1,
                false, 0, 0, CatalogEnemyRole.GardenSpitter);
            CatalogBattleUnit keeper = battle.AddEnemy("garden-keeper", 100, 10, 0, 1, 1, 1,
                false, 0, 0, CatalogEnemyRole.Supervisor);
            CatalogBattleUnit boss = battle.AddEnemy("garden-boss", 100, 10, 0, 1, 1, 1,
                true, 0, 0, CatalogEnemyRole.GardenLord, true);
            Check(GardenEnemyArt.SpeciesFor(guard) == GardenEnemyArt.Species[0], "guard mapping");
            Check(GardenEnemyArt.SpeciesFor(spitter) == GardenEnemyArt.Species[1], "spitter mapping");
            Check(GardenEnemyArt.SpeciesFor(keeper) == GardenEnemyArt.Species[2], "keeper mapping");
            Check(GardenEnemyArt.SpeciesFor(boss) == GardenEnemyArt.Species[3], "boss mapping");
            Check(ForestEnemyArt.SpeciesFor(guard) == null &&
                ForestEnemyArt.SpeciesFor(spitter) == null &&
                ForestEnemyArt.SpeciesFor(keeper) == null &&
                ForestEnemyArt.SpeciesFor(boss) == null,
                "garden and forest art routes remain separate");
        }

        static void ValidateFrames()
        {
            string guard = GardenEnemyArt.Species[0], keeper = GardenEnemyArt.Species[2];
            string boss = GardenEnemyArt.Species[3];
            Check(Frame(guard, 0, 0, 0, 0, 0, 0, 0, null) == 0, "idle frame");
            Check(Frame(guard, 1, .2f, 0, 0, 0, 0, 0, null) >= 6, "walk frame");
            Check(Frame(guard, 2, 0, .2f, 0, 0, .2f, 0, null) == 13,
                "attack windup frame");
            Check(Frame(guard, 2, 0, 0, .18f, 0, 0, 0, "BASIC") == 14,
                "attack release follows damage event");
            Check(Frame(guard, 3, 0, 0, 0, 0, 0, 0, null) == 4, "ordinary hit frame");
            Check(Frame(guard, 4, 1f, 0, .18f, 0, 0, 0, null) == 21,
                "death owns terminal frame");
            Check(Frame(keeper, 3, 0, 0, .18f, 0, 0, 0, "MINE_GUARD") == 22 &&
                Frame(keeper, 3, 0, 0, .08f, .11f, 0, 0, "MINE_GUARD") == 23,
                "keeper guard uses dedicated support poses");
            Check(Frame(boss, 3, 0, .25f, .18f, 0, .7f, 1f, "BASIC") == 22,
                "briars warning survives hit and basic release");
            Check(Frame(boss, 2, 0, .25f, 0, 0, .2f, .3f, null) == 23,
                "late briars warning uses second pose");
            Check(Frame(boss, 3, 0, 0, .18f, 0, 0, 0, "GARDEN_BRIARS") == 14,
                "briars impact releases on event");
            Check(Frame(boss, 3, 0, 0, 0, 0, 0, 0, null) == 0,
                "boss does not play received-hit pose");
        }

        static void ValidateTelegraph()
        {
            CatalogDatabase database = CatalogDatabase.FromJson(
                Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var battle = new CatalogBattle(database);
            CatalogBattleUnit boss = battle.AddEnemy("garden-boss", 4800, 80, 26, 1.8f, 1.1f,
                .48f, true, 2.2f, 0f, CatalogEnemyRole.GardenLord, true);
            for (int i = 0; i < 5; i++)
                battle.AddUnit("ally-" + i, CatalogTeam.Ally, "B-KN", null, null, -.4f,
                    (i - 2) * .35f);
            int warnings = 0, impacts = 0;
            bool poisoned = false;
            var targets = new HashSet<string>();
            var points = new List<Vector2>();
            battle.Event += e =>
            {
                if (e.SourceId != boss.Id) return;
                if (e.Kind == CatalogEventKind.BossTelegraph && e.AbilityId == "GARDEN_BRIARS")
                {
                    warnings++;
                    Check(!string.IsNullOrEmpty(e.TargetId) && targets.Add(e.TargetId),
                        "each thorn warning targets a different ally");
                    Check(boss.SlamWarningRemaining > 0f && boss.SlamRadius == 1.25f,
                        "current thorn warning exposes its radius");
                    points.Add(new Vector2(boss.SlamTargetX, boss.SlamTargetY));
                }
                if (e.Kind == CatalogEventKind.BossImpact && e.AbilityId == "GARDEN_BRIARS")
                {
                    impacts++;
                    Check(warnings == impacts && impacts <= 3 &&
                        points[impacts - 1] == new Vector2(boss.SlamTargetX, boss.SlamTargetY),
                        "thorn impacts follow the three stored warnings");
                }
                if (e.Kind == CatalogEventKind.Poison && e.AbilityId == "POISON")
                {
                    CatalogBattleUnit target = battle.Get(e.TargetId);
                    Check(target != null && target.PoisonRemaining > 0f,
                        "briars poison a surviving target");
                    poisoned = true;
                }
            };
            for (int i = 0; i < 180 && impacts < 3 && !battle.Ended; i++)
                battle.Step(.05f);
            Check(warnings == 3 && impacts == 3 && targets.Count == 3,
                "garden briars warn and strike three distinct allies in order");
            Check(poisoned, "garden briars apply poison after impact");
        }

        static int Frame(string id, int state, float stateTime, float windupAge,
            float releaseRemaining, float releaseAge, float attackWindupRemaining,
            float slamWarningRemaining, string releaseAbilityId)
        {
            return GardenEnemyArt.FrameIndex(id, state, stateTime, windupAge, releaseRemaining,
                releaseAge, attackWindupRemaining, slamWarningRemaining, releaseAbilityId);
        }

        public static void BuildAndroid()
        {
            GenerateManifest();
            Validate();
            FrostEnemyArtValidation.BuildAndroid();
        }
    }
}

