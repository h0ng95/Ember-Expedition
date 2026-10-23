using System;
using System.IO;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class CastleEnemyArtValidation
    {
        const string Folder = "Assets/Resources/Art/Enemies/v021";
        const string ManifestFile = Folder + "/castle-manifest.json";
        static int assertions;

        static void Check(bool result, string message)
        {
            assertions++;
            if (!result) throw new InvalidOperationException("Castle enemy art: " + message);
        }

        [MenuItem("Expedition/Castle Enemies/Generate Manifest")]
        public static void GenerateManifest()
        {
            ForestEnemyManifest manifest = ForestEnemyArtValidation.BuildManifest(
                Folder, "0.21", CastleEnemyArt.Species);
            Directory.CreateDirectory(Folder);
            File.WriteAllText(ManifestFile, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(ManifestFile, ImportAssetOptions.ForceUpdate);
            Debug.Log("Castle enemy manifest generated: " + ManifestFile);
        }

        [MenuItem("Expedition/Castle Enemies/Validate")]
        public static void Validate()
        {
            assertions = 0;
            ForestEnemyManifest fresh = ForestEnemyArtValidation.BuildManifest(
                Folder, "0.21", CastleEnemyArt.Species);
            TextAsset saved = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestFile);
            Check(saved != null, "manifest is missing; run Generate Manifest");
            ForestEnemyManifest stored = JsonUtility.FromJson<ForestEnemyManifest>(saved.text);
            Check(stored != null && stored.version == "0.21" && stored.sheets != null &&
                stored.sheets.Length == 4, "manifest must contain exactly four castle sheets");
            for (int i = 0; i < CastleEnemyArt.Species.Length; i++)
            {
                ForestEnemySheet expected = fresh.sheets[i], actual = stored.sheets[i];
                Check(actual != null && actual.id == CastleEnemyArt.Species[i] &&
                    ForestEnemyArt.Valid(actual), "mapping or bounds for " + CastleEnemyArt.Species[i]);
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
                Check(CastleEnemyArt.SilhouetteHeight(actual.id) > 0f &&
                    actual.referenceHeight == actual.frames[0].height,
                    "missing fixed species scale");
            }
            ValidateMapping();
            ValidateFrames();
            var art = new ForestEnemyArt();
            try
            {
                Rect boss, infantry;
                bool gotBoss = art.TryGetBounds(CastleEnemyArt.Species[3], 0, Vector2.zero, out boss);
                bool gotInfantry = art.TryGetBounds(CastleEnemyArt.Species[0], 0, Vector2.zero, out infantry);
                Check(gotBoss && gotInfantry,
                    "runtime geometry missing from generated manifest");
                Check(boss.height > 144f && boss.width > infantry.width &&
                    Mathf.Abs(boss.height - 244f) < .1f,
                    "castle boss retains large fixed silhouette scale");
            }
            finally { art.Release(); }
            Debug.Log("Castle enemy art validated: " + assertions + " assertions, four sheets.");
        }

        static void ValidateMapping()
        {
            CatalogDatabase database = CatalogDatabase.FromJson(
                Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var battle = new CatalogBattle(database);
            CatalogBattleUnit infantry = battle.AddEnemy("stone-infantry-1", 100, 10, 0, 1, 1, 1,
                false, 0, 0, CatalogEnemyRole.None);
            CatalogBattleUnit crossbow = battle.AddEnemy("crossbow-raider-1", 100, 10, 0, 1, 3, 1,
                false, 0, 0, CatalogEnemyRole.Raider);
            CatalogBattleUnit elite = battle.AddEnemy("stone-elite", 100, 10, 0, 1, 1, 1,
                false, 0, 0, CatalogEnemyRole.Beast);
            CatalogBattleUnit boss = battle.AddEnemy("golem-boss", 100, 10, 0, 1, 1, 1,
                true, 0, 0, CatalogEnemyRole.Golem);
            CatalogBattleUnit forest = battle.AddEnemy("beast-1", 100, 10, 0, 1, 1, 1,
                false, 0, 0, CatalogEnemyRole.Beast);
            Check(CastleEnemyArt.SpeciesFor(infantry) == CastleEnemyArt.Species[0], "infantry mapping");
            Check(CastleEnemyArt.SpeciesFor(crossbow) == CastleEnemyArt.Species[1], "crossbow mapping");
            Check(CastleEnemyArt.SpeciesFor(elite) == CastleEnemyArt.Species[2], "elite mapping");
            Check(CastleEnemyArt.SpeciesFor(boss) == CastleEnemyArt.Species[3], "golem mapping");
            Check(CastleEnemyArt.SpeciesFor(forest) == null, "forest mapping stays separate");
        }

        static void ValidateFrames()
        {
            string infantry = CastleEnemyArt.Species[0], elite = CastleEnemyArt.Species[2];
            string boss = CastleEnemyArt.Species[3];
            Check(Frame(infantry, 0, 0, 0, 0, 0, 0, false, 0, 0, false, null) == 0, "idle frame");
            Check(Frame(infantry, 1, .2f, 0, 0, 0, 0, false, 0, 0, false, null) >= 6, "walk frame");
            Check(Frame(infantry, 2, 0, .2f, 0, 0, 0, false, .2f, 0, false, null) == 13,
                "attack windup frame");
            Check(Frame(infantry, 2, 0, 0, .18f, 0, 0, false, 0, 0, false, "BASIC") == 14,
                "attack release follows damage event");
            Check(Frame(infantry, 3, 0, 0, 0, 0, 0, false, 0, 0, false, null) == 4,
                "ordinary hit frame");
            Check(Frame(infantry, 4, 1f, 0, .18f, 0, 0, false, 0, 0, false, "BASIC") == 21,
                "death owns terminal frame");
            Check(Frame(elite, 3, 0, .25f, .18f, 0, .4f, false, .4f, 0, false, "BASIC") == 23,
                "elite charge warning survives hit and stale release");
            Check(Frame(elite, 3, 0, .25f, .18f, 0, 0, true, .13f, 0, false, null) == 15,
                "elite charge motion survives hit");
            Check(Frame(boss, 3, 0, .25f, .18f, 0, 0, false, .7f, 1f, false, "BASIC") == 22,
                "boss slam warning survives hit and basic release");
            Check(Frame(boss, 2, 0, .25f, 0, 0, 0, false, .2f, .3f, false, null) == 23,
                "boss late slam warning uses second pose");
            Check(Frame(boss, 2, 0, .25f, .18f, 0, 0, false, .6f, .6f, true, "GOLEM_SLAM") == 14,
                "first boss impact releases before aftershock warning");
            Check(Frame(boss, 2, 0, .25f, 0, 0, 0, false, .3f, .4f, true, null) == 23,
                "aftershock warning has distinct second pose");
            Check(Frame(boss, 3, 0, 0, .18f, 0, 0, false, 0, 0, false, "GOLEM_AFTERSHOCK") == 14,
                "aftershock impact releases on event");
            Check(Frame(boss, 3, 0, 0, 0, 0, 0, false, 0, 0, false, null) == 0,
                "boss does not play received-hit pose");
            Check(Frame(boss, 4, 0, 0, .18f, 0, 0, false, 0, .8f, true, "GOLEM_SLAM") == 18,
                "death overrides every warning and release");
        }

        static int Frame(string id, int state, float stateTime, float windupAge,
            float releaseRemaining, float releaseAge, float chargeWarningRemaining,
            bool chargeMoving, float attackWindupRemaining, float slamWarningRemaining,
            bool slamAftershock, string releaseAbilityId)
        {
            return CastleEnemyArt.FrameIndex(id, state, stateTime, windupAge, releaseRemaining,
                releaseAge, chargeWarningRemaining, chargeMoving, attackWindupRemaining,
                slamWarningRemaining, slamAftershock, releaseAbilityId);
        }

        public static void BuildAndroid()
        {
            GenerateManifest();
            Validate();
            ForestEnemyArtValidation.Validate();
            CombatSettlementValidation.Validate();
            BattleVisibilityValidation.Validate();
            BossHitReactionValidation.Validate();
            DragGrowthValidation.BuildAndroid();
        }
    }
}
