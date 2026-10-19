using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public enum StageObjective { RecruitSurvives, HoldOut, DefeatBoss }

    public sealed class WaveDefinition
    {
        public readonly string Name;
        public readonly float EnemyHp, EnemyDamage, EnemyCooldown, EnemySpeed;
        public readonly bool RecruitAfter;

        public WaveDefinition(string name, float hp, float damage, float cooldown, float speed, bool recruitAfter)
        {
            Name = name;
            EnemyHp = hp;
            EnemyDamage = damage;
            EnemyCooldown = cooldown;
            EnemySpeed = speed;
            RecruitAfter = recruitAfter;
        }
    }

    public sealed class StageDefinition
    {
        public readonly string Id, Name, PrerequisiteId;
        public readonly StageObjective Objective;
        public readonly bool Playable;
        public readonly WaveDefinition[] Waves;

        public StageDefinition(string id, string name, string prerequisiteId, StageObjective objective,
            bool playable, params WaveDefinition[] waves)
        {
            Id = id;
            Name = name;
            PrerequisiteId = prerequisiteId;
            Objective = objective;
            Playable = playable;
            Waves = waves;
        }
    }

    public static class StageCatalog
    {
        public static readonly StageDefinition ForestEntrance = new StageDefinition("1-1", "FOREST ENTRANCE", null,
            StageObjective.RecruitSurvives, true,
            new WaveDefinition("FOREST BEAST", 380f, 16f, 1.55f, .58f, true),
            new WaveDefinition("FOREST ELITE", 1100f, 45f, 1.15f, .62f, false));
        public static readonly StageDefinition RuinedOutpost = new StageDefinition("1-2", "RUINED OUTPOST", "1-1",
            StageObjective.HoldOut, false,
            new WaveDefinition("OUTPOST GUARD", 0f, 0f, 0f, 0f, false));
        public static readonly StageDefinition CastleGate = new StageDefinition("1-3", "CASTLE GATE", "1-2",
            StageObjective.DefeatBoss, false,
            new WaveDefinition("GATE GOLEM", 0f, 0f, 0f, 0f, false));
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
