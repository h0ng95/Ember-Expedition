using System;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class MineEnemyArtValidation
    {
        const string Folder = "Assets/Resources/Art/Enemies/v024";
        const string ManifestFile = Folder + "/mine-manifest.json";
        static int assertions;

        static void Check(bool result, string message)
        {
            assertions++;
            if (!result) throw new InvalidOperationException("Mine enemy art: " + message);
        }

        [MenuItem("Expedition/Mine Enemies/Generate Manifest")]
        public static void GenerateManifest()
        {
            ForestEnemyManifest manifest = ForestEnemyArtValidation.BuildManifest(
                Folder, "0.24", MineEnemyArt.Species);
            System.IO.Directory.CreateDirectory(Folder);
            System.IO.File.WriteAllText(ManifestFile, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(ManifestFile, ImportAssetOptions.ForceUpdate);
            Debug.Log("Mine enemy manifest generated: " + ManifestFile);
        }

        [MenuItem("Expedition/Mine Enemies/Validate")]
        public static void Validate()
        {
            assertions = 0;
            ForestEnemyManifest fresh = ForestEnemyArtValidation.BuildManifest(
                Folder, "0.24", MineEnemyArt.Species);
            TextAsset saved = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestFile);
            Check(saved != null, "manifest is missing; run Generate Manifest");
            ForestEnemyManifest stored = JsonUtility.FromJson<ForestEnemyManifest>(saved.text);
            Check(stored != null && stored.version == "0.24" && stored.sheets != null &&
                stored.sheets.Length == MineEnemyArt.Species.Length,
                "manifest must contain exactly four mine and furnace sheets");
            for (int i = 0; i < MineEnemyArt.Species.Length; i++)
            {
                ForestEnemySheet expected = fresh.sheets[i], actual = stored.sheets[i];
                Check(actual != null && actual.id == MineEnemyArt.Species[i] && ForestEnemyArt.Valid(actual),
                    "mapping or bounds for " + MineEnemyArt.Species[i]);
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
                Check(MineEnemyArt.SilhouetteHeight(actual.id) > 0f &&
                    actual.referenceHeight == actual.frames[0].height,
                    "missing fixed species scale");
            }
            ValidateMapping();
            ValidateFrames();
            ValidateTelegraph();
            var art = new ForestEnemyArt();
            try
            {
                Rect boss, worker;
                bool gotBoss = art.TryGetBounds(MineEnemyArt.Species[3], 0, Vector2.zero, out boss);
                bool gotWorker = art.TryGetBounds(MineEnemyArt.Species[0], 0, Vector2.zero, out worker);
                Check(gotBoss && gotWorker && boss.width > worker.width &&
                    Mathf.Abs(boss.height - 280f) < .1f,
                    "furnace boss keeps the large fixed silhouette scale");
            }
            finally { art.Release(); }
            Debug.Log("Mine enemy art validated: " + assertions + " assertions, four sheets.");
        }

        static void ValidateMapping()
        {
            CatalogDatabase database = CatalogDatabase.FromJson(
                Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var battle = new CatalogBattle(database);
            CatalogBattleUnit worker = battle.AddEnemy("mine-worker-1", 100, 10, 0, 1, 1, 1,
                false, 0, 0, CatalogEnemyRole.MineWorker);
            CatalogBattleUnit slinger = battle.AddEnemy("mine-slinger-1", 100, 10, 0, 1, 3, 1,
                false, 0, 0, CatalogEnemyRole.Raider);
            CatalogBattleUnit elite = battle.AddEnemy("mine-elite", 100, 10, 0, 1, 1, 1,
                false, 0, 0, CatalogEnemyRole.Supervisor);
            CatalogBattleUnit boss = battle.AddEnemy("furnace-boss", 100, 10, 0, 1, 1, 1,
                true, 0, 0, CatalogEnemyRole.Furnace, true);
            CatalogBattleUnit forest = battle.AddEnemy("beast-1", 100, 10, 0, 1, 1, 1,
                false, 0, 0, CatalogEnemyRole.Beast);
            Check(MineEnemyArt.SpeciesFor(worker) == MineEnemyArt.Species[0], "worker mapping");
            Check(MineEnemyArt.SpeciesFor(slinger) == MineEnemyArt.Species[1], "slinger mapping");
            Check(MineEnemyArt.SpeciesFor(elite) == MineEnemyArt.Species[2], "supervisor mapping");
            Check(MineEnemyArt.SpeciesFor(boss) == MineEnemyArt.Species[3], "furnace mapping");
            Check(MineEnemyArt.SpeciesFor(forest) == null && ForestEnemyArt.SpeciesFor(worker) == null &&
                ForestEnemyArt.SpeciesFor(slinger) == null && ForestEnemyArt.SpeciesFor(elite) == null &&
                ForestEnemyArt.SpeciesFor(boss) == null,
                "mine and forest art routes remain separate");
        }

        static void ValidateFrames()
        {
            string worker = MineEnemyArt.Species[0], elite = MineEnemyArt.Species[2];
            string boss = MineEnemyArt.Species[3];
            Check(Frame(worker, 0, 0, 0, 0, 0, 0, 0, false, null) == 0, "idle frame");
            Check(Frame(worker, 1, .2f, 0, 0, 0, 0, 0, false, null) >= 6, "walk frame");
            Check(Frame(worker, 2, 0, .2f, 0, 0, .2f, 0, false, null) == 13,
                "attack windup frame");
            Check(Frame(worker, 2, 0, 0, .18f, 0, 0, 0, false, "BASIC") == 14,
                "attack release follows damage event");
            Check(Frame(worker, 3, 0, 0, 0, 0, 0, 0, false, null) == 4,
                "ordinary hit frame");
            Check(Frame(worker, 4, 1f, 0, .18f, 0, 0, 0, false, null) == 21,
                "death owns terminal frame");
            Check(Frame(elite, 3, 0, 0, .18f, 0, 0, 0, false, "MINE_GUARD") == 22 &&
                Frame(elite, 3, 0, 0, .08f, .11f, 0, 0, false, "MINE_GUARD") == 23,
                "supervisor guard uses both dedicated ward poses despite hit feedback");
            Check(Frame(boss, 3, 0, .25f, .18f, 0, .7f, 1f, false, "BASIC") == 22,
                "furnace warning survives hit and basic release");
            Check(Frame(boss, 2, 0, .25f, 0, 0, .2f, .3f, false, null) == 23,
                "late blast warning uses second pose");
            Check(Frame(boss, 2, 0, .25f, .18f, 0, .6f, .6f, true, "FURNACE_BLAST") == 14,
                "first blast impact releases before ember warning");
            Check(Frame(boss, 2, 0, .25f, 0, 0, .3f, .4f, true, null) == 23,
                "ember warning has distinct pose");
            Check(Frame(boss, 3, 0, 0, .18f, 0, 0, 0, false, "FURNACE_EMBER") == 14,
                "ember impact releases on event");
            Check(Frame(boss, 3, 0, 0, 0, 0, 0, 0, false, null) == 0,
                "boss does not play received-hit pose");
            Check(Frame(boss, 4, 0, 0, .18f, 0, 0, .8f, true, "FURNACE_BLAST") == 18,
                "death overrides warning and release");
        }

        static void ValidateTelegraph()
        {
            CatalogDatabase database = CatalogDatabase.FromJson(
                Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var battle = new CatalogBattle(database);
            CatalogBattleUnit boss = battle.AddEnemy("furnace-boss", 4600, 78, 25, 1.8f, 1.1f,
                .48f, true, 2.2f, 0f, CatalogEnemyRole.Furnace, true);
            for (int i = 0; i < 5; i++)
                battle.AddUnit("ally-" + i, CatalogTeam.Ally, "B-KN", null, null, -.4f,
                    (i - 2) * .35f);
            bool warned = false, impacted = false;
            battle.Event += e =>
            {
                if (e.SourceId != boss.Id) return;
                if (e.Kind == CatalogEventKind.BossTelegraph && e.AbilityId == "FURNACE_BLAST")
                    warned = true;
                if (e.Kind == CatalogEventKind.BossImpact && e.AbilityId == "FURNACE_BLAST")
                    impacted = true;
            };
            for (int i = 0; i < 180 && !impacted && !battle.Ended; i++)
            {
                battle.Step(.05f);
                if (warned && !impacted)
                    Check(boss.SlamWarningRemaining > 0f && boss.SlamRadius > 0f,
                        "blast telegraph exposes active ground radius");
            }
            Check(warned && impacted, "furnace blast warning precedes impact");
        }

        static int Frame(string id, int state, float stateTime, float windupAge,
            float releaseRemaining, float releaseAge, float attackWindupRemaining,
            float slamWarningRemaining, bool slamAftershock, string releaseAbilityId)
        {
            return MineEnemyArt.FrameIndex(id, state, stateTime, windupAge, releaseRemaining,
                releaseAge, attackWindupRemaining, slamWarningRemaining,
                slamAftershock, releaseAbilityId);
        }

        public static void BuildAndroid()
        {
            GenerateManifest();
            Validate();
            CastleEnemyArtValidation.BuildAndroid();
        }
    }
}
