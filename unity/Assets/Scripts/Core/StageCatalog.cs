using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public enum StageObjective { RecruitSurvives, BacklineSurvives, DefeatBoss }
    public enum WaveEnemyKind { Beast, Archer, EliteArcher, Golem }

    public sealed class WaveDefinition
    {
        public readonly string Name;
        public readonly float EnemyHp, EnemyDamage, EnemyCooldown, EnemySpeed;
        public readonly bool RecruitAfter;
        public readonly WaveEnemyKind EnemyKind;

        public WaveDefinition(string name, float hp, float damage, float cooldown, float speed, bool recruitAfter,
            WaveEnemyKind enemyKind = WaveEnemyKind.Beast)
        {
            Name = name;
            EnemyHp = hp;
            EnemyDamage = damage;
            EnemyCooldown = cooldown;
            EnemySpeed = speed;
            RecruitAfter = recruitAfter;
            EnemyKind = enemyKind;
        }
    }

    public sealed class StageDefinition
    {
        public readonly string Id, Name, PrerequisiteId;
        public readonly StageObjective Objective;
        public readonly bool Playable;
        public readonly WaveDefinition[] Waves;
        public readonly int[] SurvivalWeights;
        public readonly int MaxDeployed = 4;

        public StageDefinition(string id, string name, string prerequisiteId, StageObjective objective,
            bool playable, int[] survivalWeights, params WaveDefinition[] waves)
        {
            if (waves == null || survivalWeights == null || waves.Length == 0 ||
                waves.Length != survivalWeights.Length) throw new ArgumentException("Each wave needs a survival weight.");
            int total = 0;
            foreach (int weight in survivalWeights) total += weight;
            if (total != 25) throw new ArgumentException("Stage survival weights must total 25.");
            Id = id;
            Name = name;
            PrerequisiteId = prerequisiteId;
            Objective = objective;
            Playable = playable;
            Waves = waves;
            SurvivalWeights = survivalWeights;
        }
    }

    public static class StageCatalog
    {
        public static readonly StageDefinition ForestEntrance = new StageDefinition("1-1", "FOREST ENTRANCE", null,
            StageObjective.RecruitSurvives, true, new[] { 10, 15 },
            new WaveDefinition("FOREST BEAST", 380f, 16f, 1.55f, .58f, true),
            new WaveDefinition("FOREST ELITE", 1100f, 45f, 1.15f, .62f, false));
        public static readonly StageDefinition RuinedOutpost = new StageDefinition("1-2", "RUINED OUTPOST", "1-1",
            StageObjective.BacklineSurvives, true, new[] { 5, 5, 15 },
            new WaveDefinition("OUTPOST SCOUT", 370f, 17f, 1.8f, .62f, true, WaveEnemyKind.Archer),
            new WaveDefinition("RAMPART ARCHER", 620f, 23f, 1.55f, .65f, true, WaveEnemyKind.Archer),
            new WaveDefinition("ELITE ARCHER", 1040f, 32f, 1.3f, .68f, false, WaveEnemyKind.EliteArcher));
        public static readonly StageDefinition CastleGate = new StageDefinition("1-3", "CASTLE GATE", "1-2",
            StageObjective.DefeatBoss, false, new[] { 25 },
            new WaveDefinition("GATE GOLEM", 0f, 0f, 0f, 0f, false, WaveEnemyKind.Golem));
        public static readonly StageDefinition[] All = { ForestEntrance, RuinedOutpost, CastleGate };
    }

    public sealed class StageRecord
    {
        public int BestScore { get; private set; }
        public int BestStars { get; private set; }

        public void RecordClear(int score, int stars)
        {
            if (score < 0 || score > 100 || stars < 1 || stars > 3)
                throw new ArgumentOutOfRangeException("score");
            BestScore = Math.Max(BestScore, score);
            BestStars = Math.Max(BestStars, stars);
        }
    }

    public sealed class StageProgress
    {
        private readonly Dictionary<string, StageRecord> records = new Dictionary<string, StageRecord>();

        public StageRecord RecordFor(StageDefinition stage)
        {
            StageRecord record;
            if (!records.TryGetValue(stage.Id, out record))
            {
                record = new StageRecord();
                records.Add(stage.Id, record);
            }
            return record;
        }

        public bool IsUnlocked(StageDefinition stage)
        {
            if (stage.PrerequisiteId == null) return true;
            StageDefinition prerequisite = Array.Find(StageCatalog.All, item => item.Id == stage.PrerequisiteId);
            return prerequisite != null && RecordFor(prerequisite).BestStars > 0;
        }
    }
}
