using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public enum StagePhase { Map, Briefing, Preparation, Battle, Recruitment, Result }
    public enum RecruitKind { ShieldKnight, LanternPriest, FireMage, ScoutArcher }

    // A stage run uses catalog wave data; persistence is owned by the presentation.
    public sealed class StageFlow
    {
        private readonly Random random;
        private readonly List<RecruitKind> candidates = new List<RecruitKind>(3);
        private readonly List<RecruitKind> recruits = new List<RecruitKind>(2);
        private readonly List<int> waveSurvivors = new List<int>();
        private readonly List<int> waveDeployed = new List<int>();
        public StageDefinition Definition { get; private set; }
        public StagePhase Phase { get; private set; }
        public int BattleNumber { get; private set; }
        public RecruitKind? Recruited { get; private set; }
        public IReadOnlyList<RecruitKind> Recruits { get { return recruits.AsReadOnly(); } }
        public int OwnedCount { get { return 3 + recruits.Count; } }
        public IReadOnlyList<RecruitKind> Candidates { get { return candidates.AsReadOnly(); } }
        public int Score { get; private set; }
        public int Stars { get; private set; }
        public int LastSurvivors { get; private set; }
        public int LastDeployed { get; private set; }
        public int BacklineSurvivors { get; private set; }
        public int BacklineDeployed { get; private set; }
        public int FirstSurvivors { get { return waveSurvivors.Count > 0 ? waveSurvivors[0] : 0; } }
        public int FirstDeployed { get { return waveDeployed.Count > 0 ? waveDeployed[0] : 0; } }
        public bool ObjectiveMet { get; private set; }
        public int ObjectiveBonus { get { return ObjectiveMet ? 10 : 0; } }
        public int TotalSurvivalBonus
        {
            get
            {
                int total = 0;
                for (int i = 0; i < waveSurvivors.Count; i++) total += WaveSurvivalBonus(i);
                return total;
            }
        }
        public int WaveSurvivalBonus(int index)
        {
            if (index < 0 || index >= waveSurvivors.Count) return 0;
            return SurvivalPoints(Definition.SurvivalWeights[index], waveSurvivors[index], waveDeployed[index]);
        }
        public int WaveSurvivorCount(int index) { return index < waveSurvivors.Count ? waveSurvivors[index] : 0; }
        public int WaveDeployedCount(int index) { return index < waveDeployed.Count ? waveDeployed[index] : 0; }
        public bool Cleared { get; private set; }

        public StageFlow(Random random)
        {
            if (random == null) throw new ArgumentNullException("random");
            this.random = random;
            Definition = StageCatalog.ForestEntrance;
            Phase = StagePhase.Map;
        }

        public bool OpenBriefing(StageDefinition selected = null, StageProgress progress = null)
        {
            if (Phase != StagePhase.Map) return false;
            selected = selected ?? StageCatalog.ForestEntrance;
            if (!selected.Playable) return false;
            if (selected.PrerequisiteId != null && (progress == null || !progress.IsUnlocked(selected))) return false;
            Definition = selected;
            Phase = StagePhase.Briefing;
            return true;
        }

        public bool Begin()
        {
            if (Phase != StagePhase.Briefing) return false;
            BattleNumber = 1;
            Recruited = null;
            candidates.Clear();
            recruits.Clear();
            waveSurvivors.Clear();
            waveDeployed.Clear();
            Score = Stars = LastSurvivors = LastDeployed = BacklineSurvivors = BacklineDeployed = 0;
            ObjectiveMet = false;
            Cleared = false;
            Phase = StagePhase.Preparation;
            return true;
        }

        public bool StartBattle()
        {
            if (Phase != StagePhase.Preparation) return false;
            Phase = StagePhase.Battle;
            return true;
        }

        public bool CompleteBattle(bool victory, int survivors, int deployed, bool recruitedAlive = false,
            int backlineSurvivors = 0, int backlineDeployed = 0)
        {
            if (Phase != StagePhase.Battle || deployed < 1 || deployed > Definition.MaxDeployed ||
                survivors < 0 || survivors > deployed)
                return false;
            if (!victory)
            {
                Score = Stars = 0;
                Cleared = false;
                LastSurvivors = survivors;
                LastDeployed = deployed;
                Phase = StagePhase.Result;
                return true;
            }
            waveSurvivors.Add(survivors);
            waveDeployed.Add(deployed);
            if (Definition.Waves[BattleNumber - 1].RecruitAfter)
            {
                DraftCandidates();
                Phase = StagePhase.Recruitment;
                return true;
            }
            if (BattleNumber < Definition.Waves.Length)
            {
                BattleNumber++;
                Phase = StagePhase.Preparation;
                return true;
            }
            LastSurvivors = survivors;
            LastDeployed = deployed;
            BacklineSurvivors = backlineSurvivors;
            BacklineDeployed = backlineDeployed;
            ObjectiveMet = MeetsObjective(Definition, recruitedAlive, backlineSurvivors, backlineDeployed);
            Score = CalculateScore(Definition, waveSurvivors, waveDeployed, ObjectiveMet);
            Stars = Score >= 98 ? 3 : Score >= 80 ? 2 : 1;
            Cleared = true;
            Phase = StagePhase.Result;
            return true;
        }

        public bool ChooseRecruit(int index)
        {
            if (Phase != StagePhase.Recruitment || index < 0 || index >= candidates.Count ||
                BattleNumber >= Definition.Waves.Length) return false;
            Recruited = candidates[index];
            recruits.Add(Recruited.Value);
            BattleNumber++;
            Phase = StagePhase.Preparation;
            return true;
        }

        public bool ReturnMap()
        {
            if (Phase != StagePhase.Briefing && Phase != StagePhase.Preparation &&
                Phase != StagePhase.Result) return false;
            Phase = StagePhase.Map;
            return true;
        }

        public static int CalculateScore(int firstSurvivors, int firstDeployed,
            int finalSurvivors, int finalDeployed, bool recruitedAlive)
        {
            return CalculateScore(StageCatalog.ForestEntrance,
                new[] { firstSurvivors, finalSurvivors }, new[] { firstDeployed, finalDeployed }, recruitedAlive);
        }

        public static int CalculateScore(StageDefinition definition, IReadOnlyList<int> survivors,
            IReadOnlyList<int> deployed, bool objectiveMet)
        {
            if (definition == null || survivors == null || deployed == null ||
                survivors.Count != definition.Waves.Length || deployed.Count != definition.Waves.Length)
                throw new ArgumentException("Score needs one result per wave.");
            int score = 65 + (objectiveMet ? 10 : 0);
            for (int i = 0; i < survivors.Count; i++)
            {
                if (deployed[i] < 1 || survivors[i] < 0 || survivors[i] > deployed[i])
                    throw new ArgumentOutOfRangeException("survivors");
                score += SurvivalPoints(definition.SurvivalWeights[i], survivors[i], deployed[i]);
            }
            return score;
        }

        public static bool MeetsObjective(StageDefinition definition, bool recruitedAlive,
            int backlineSurvivors, int backlineDeployed)
        {
            if (definition.Objective == StageObjective.RecruitSurvives) return recruitedAlive;
            if (definition.Objective == StageObjective.BacklineSurvives)
                return backlineDeployed > 0 && backlineSurvivors == backlineDeployed;
            return false;
        }

        private static int SurvivalPoints(int maximum, int survivors, int deployed)
        { return deployed > 0 ? (maximum * survivors + deployed / 2) / deployed : 0; }

        private void DraftCandidates()
        {
            var pool = new List<RecruitKind> { RecruitKind.ShieldKnight, RecruitKind.LanternPriest,
                RecruitKind.FireMage, RecruitKind.ScoutArcher };
            foreach (RecruitKind recruited in recruits) pool.Remove(recruited);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                RecruitKind value = pool[i];
                pool[i] = pool[other];
                pool[other] = value;
            }
            candidates.Clear();
            for (int i = 0; i < Math.Min(3, pool.Count); i++) candidates.Add(pool[i]);
        }
    }
}
