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
        public StageDefinition Definition { get; private set; }
        public StagePhase Phase { get; private set; }
        public int BattleNumber { get; private set; }
        public RecruitKind? Recruited { get; private set; }
        public IReadOnlyList<RecruitKind> Candidates { get { return candidates.AsReadOnly(); } }
        public int Score { get; private set; }
        public int Stars { get; private set; }
        public int LastSurvivors { get; private set; }
        public int LastDeployed { get; private set; }
        public int FirstSurvivors { get; private set; }
        public int FirstDeployed { get; private set; }
        public bool RecruitSurvived { get; private set; }
        public int FirstSurvivalBonus { get { return SurvivalPoints(10, FirstSurvivors, FirstDeployed); } }
        public int FinalSurvivalBonus { get { return SurvivalPoints(15, LastSurvivors, LastDeployed); } }
        public int ObjectiveBonus { get { return RecruitSurvived ? 10 : 0; } }
        public bool Cleared { get; private set; }

        public StageFlow(Random random)
        {
            if (random == null) throw new ArgumentNullException("random");
            this.random = random;
            Definition = StageCatalog.ForestEntrance;
            Phase = StagePhase.Map;
        }

        public bool OpenBriefing(StageDefinition selected = null)
        {
            if (Phase != StagePhase.Map) return false;
            selected = selected ?? StageCatalog.ForestEntrance;
            if (!selected.Playable) return false;
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
            Score = Stars = LastSurvivors = LastDeployed = FirstSurvivors = FirstDeployed = 0;
            RecruitSurvived = false;
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

        public bool CompleteBattle(bool victory, int survivors, int deployed, bool recruitedAlive = false)
        {
            if (Phase != StagePhase.Battle || deployed < 1 || survivors < 0 || survivors > deployed)
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
            if (BattleNumber == 1)
            {
                FirstSurvivors = survivors;
                FirstDeployed = deployed;
            }
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
            RecruitSurvived = recruitedAlive;
            Score = CalculateScore(FirstSurvivors, FirstDeployed, survivors, deployed, recruitedAlive);
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
            if (firstDeployed < 1 || firstSurvivors < 0 || firstSurvivors > firstDeployed ||
                finalDeployed < 1 || finalSurvivors < 0 || finalSurvivors > finalDeployed)
                throw new ArgumentOutOfRangeException("firstSurvivors");
            return 65 + SurvivalPoints(10, firstSurvivors, firstDeployed) +
                SurvivalPoints(15, finalSurvivors, finalDeployed) + (recruitedAlive ? 10 : 0);
        }

        private static int SurvivalPoints(int maximum, int survivors, int deployed)
        { return deployed > 0 ? (maximum * survivors + deployed / 2) / deployed : 0; }

        private void DraftCandidates()
        {
            var pool = new List<RecruitKind> { RecruitKind.ShieldKnight, RecruitKind.LanternPriest,
                RecruitKind.FireMage, RecruitKind.ScoutArcher };
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                RecruitKind value = pool[i];
                pool[i] = pool[other];
                pool[other] = value;
            }
            candidates.Clear();
            for (int i = 0; i < 3; i++) candidates.Add(pool[i]);
        }
    }
}
