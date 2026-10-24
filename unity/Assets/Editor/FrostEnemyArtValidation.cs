using System;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class FrostEnemyArtValidation
    {
        const string Folder = "Assets/Resources/Art/Enemies/v029";
        const string ManifestFile = Folder + "/frost-manifest.json";
        static int assertions;

        static void Check(bool result, string message)
        {
            assertions++;
            if (!result) throw new InvalidOperationException("Frost enemy art: " + message);
        }

        [MenuItem("Expedition/Frost Enemies/Generate Manifest")]
        public static void GenerateManifest()
        {
            ForestEnemyManifest manifest = ForestEnemyArtValidation.BuildManifest(
                Folder, "0.29", FrostEnemyArt.Species);
            System.IO.Directory.CreateDirectory(Folder);
            System.IO.File.WriteAllText(ManifestFile, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(ManifestFile, ImportAssetOptions.ForceUpdate);
            Debug.Log("Frost enemy manifest generated: " + ManifestFile);
        }

        [MenuItem("Expedition/Frost Enemies/Validate")]
        public static void Validate()
        {
            assertions = 0;
            ForestEnemyManifest fresh = ForestEnemyArtValidation.BuildManifest(
                Folder, "0.29", FrostEnemyArt.Species);
            TextAsset saved = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestFile);
            Check(saved != null, "manifest is missing; run Generate Manifest");
            ForestEnemyManifest stored = JsonUtility.FromJson<ForestEnemyManifest>(saved.text);
            Check(stored != null && stored.version == "0.29" && stored.sheets != null &&
                stored.sheets.Length == FrostEnemyArt.Species.Length,
                "manifest must contain four frost sheets");
            for (int i = 0; i < FrostEnemyArt.Species.Length; i++)
            {
                ForestEnemySheet expected = fresh.sheets[i], actual = stored.sheets[i];
                Check(actual != null && actual.id == FrostEnemyArt.Species[i] && ForestEnemyArt.Valid(actual),
                    "mapping or bounds for " + FrostEnemyArt.Species[i]);
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
                Check(FrostEnemyArt.SilhouetteHeight(actual.id) > 0f &&
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
                bool gotBoss = art.TryGetBounds(FrostEnemyArt.Species[3], 0, Vector2.zero, out boss);
                bool gotGuard = art.TryGetBounds(FrostEnemyArt.Species[0], 0, Vector2.zero, out guard);
                Check(gotBoss && gotGuard && boss.width > guard.width &&
                    Mathf.Abs(boss.height - 244f) < .1f,
                    "frost boss keeps the large fixed silhouette scale");
            }
            finally { art.Release(); }
            Debug.Log("Frost enemy art validated: " + assertions + " assertions, four sheets.");
        }

        static void ValidateMapping()
        {
            CatalogDatabase database = CatalogDatabase.FromJson(
                Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var battle = new CatalogBattle(database);
            CatalogBattleUnit guard = battle.AddEnemy("frost-guard-1", 100, 10, 0, 1, 1, 1,
                false, 0, 0, CatalogEnemyRole.MineWorker);
            CatalogBattleUnit acolyte = battle.AddEnemy("frost-acolyte-1", 100, 10, 0, 1, 3, 1,
                false, 0, 0, CatalogEnemyRole.Raider);
            CatalogBattleUnit cantor = battle.AddEnemy("frost-cantor", 100, 10, 0, 1, 1, 1,
                false, 0, 0, CatalogEnemyRole.Supervisor);
            CatalogBattleUnit boss = battle.AddEnemy("frost-boss", 100, 10, 0, 1, 1, 1,
                true, 0, 0, CatalogEnemyRole.FrostWarden, true);
            Check(FrostEnemyArt.SpeciesFor(guard) == FrostEnemyArt.Species[0], "guard mapping");
            Check(FrostEnemyArt.SpeciesFor(acolyte) == FrostEnemyArt.Species[1], "acolyte mapping");
            Check(FrostEnemyArt.SpeciesFor(cantor) == FrostEnemyArt.Species[2], "cantor mapping");
            Check(FrostEnemyArt.SpeciesFor(boss) == FrostEnemyArt.Species[3], "boss mapping");
            Check(ForestEnemyArt.SpeciesFor(guard) == null &&
                ForestEnemyArt.SpeciesFor(acolyte) == null &&
                ForestEnemyArt.SpeciesFor(cantor) == null &&
                ForestEnemyArt.SpeciesFor(boss) == null,
                "frost and forest art routes remain separate");
        }

        static void ValidateFrames()
        {
            string guard = FrostEnemyArt.Species[0], cantor = FrostEnemyArt.Species[2];
            string boss = FrostEnemyArt.Species[3];
            Check(Frame(guard, 0, 0, 0, 0, 0, 0, 0, null) == 0, "idle frame");
            Check(Frame(guard, 1, .2f, 0, 0, 0, 0, 0, null) >= 6, "walk frame");
            Check(Frame(guard, 2, 0, .2f, 0, 0, .2f, 0, null) == 13,
                "attack windup frame");
            Check(Frame(guard, 2, 0, 0, .18f, 0, 0, 0, "BASIC") == 14,
                "attack release follows damage event");
            Check(Frame(guard, 3, 0, 0, 0, 0, 0, 0, null) == 4, "ordinary hit frame");
            Check(Frame(guard, 4, 1f, 0, .18f, 0, 0, 0, null) == 21,
                "death owns terminal frame");
            Check(Frame(cantor, 3, 0, 0, .18f, 0, 0, 0, "MINE_GUARD") == 22 &&
                Frame(cantor, 3, 0, 0, .08f, .11f, 0, 0, "MINE_GUARD") == 23,
                "cantor guard uses dedicated support poses");
            Check(Frame(boss, 3, 0, .25f, .18f, 0, .7f, 1f, "BASIC") == 22,
                "nova warning survives hit and basic release");
            Check(Frame(boss, 2, 0, .25f, 0, 0, .2f, .3f, null) == 23,
                "late nova warning uses second pose");
            Check(Frame(boss, 3, 0, 0, .18f, 0, 0, 0, "FROST_NOVA") == 14,
                "nova impact releases on event");
            Check(Frame(boss, 3, 0, 0, 0, 0, 0, 0, null) == 0,
                "boss does not play received-hit pose");
        }

        static void ValidateTelegraph()
        {
            CatalogDatabase database = CatalogDatabase.FromJson(
                Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var battle = new CatalogBattle(database);
            CatalogBattleUnit boss = battle.AddEnemy("frost-boss", 4800, 80, 26, 1.8f, 1.1f,
                .48f, true, 2.2f, 0f, CatalogEnemyRole.FrostWarden, true);
            for (int i = 0; i < 5; i++)
                battle.AddUnit("ally-" + i, CatalogTeam.Ally, "B-KN", null, null, -.4f,
                    (i - 2) * .35f);
            bool warned = false, impacted = false, frozen = false;
            battle.Event += e =>
            {
                if (e.SourceId != boss.Id) return;
                if (e.Kind == CatalogEventKind.BossTelegraph && e.AbilityId == "FROST_NOVA")
                    warned = true;
                if (e.Kind == CatalogEventKind.BossImpact && e.AbilityId == "FROST_NOVA")
                    impacted = true;
                if (e.Kind == CatalogEventKind.Freeze && e.AbilityId == "FROST_NOVA")
                {
                    CatalogBattleUnit target = battle.Get(e.TargetId);
                    Check(target != null && target.FrozenRemaining > 0f,
                        "nova freezes a surviving target");
                    frozen = true;
                }
            };
            for (int i = 0; i < 180 && !impacted && !battle.Ended; i++)
            {
                battle.Step(.05f);
                if (warned && !impacted)
                    Check(boss.SlamWarningRemaining > 0f && boss.SlamRadius > 0f,
                        "nova exposes active ground radius");
            }
            Check(warned && impacted, "frost nova warning precedes impact");
            Check(frozen, "frost nova applies freeze after impact");
        }

        static int Frame(string id, int state, float stateTime, float windupAge,
            float releaseRemaining, float releaseAge, float attackWindupRemaining,
            float slamWarningRemaining, string releaseAbilityId)
        {
            return FrostEnemyArt.FrameIndex(id, state, stateTime, windupAge, releaseRemaining,
                releaseAge, attackWindupRemaining, slamWarningRemaining, releaseAbilityId);
        }

        public static void BuildAndroid()
        {
            GenerateManifest();
            Validate();
            MineEnemyArtValidation.BuildAndroid();
        }
    }
}
