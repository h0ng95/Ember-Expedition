using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public enum ExpeditionPhase { Preparation, Battle, Result, Completed, Defeated }

    [Serializable]
    public sealed class ExpeditionPreparationSave
    {
        public CatalogRosterSave Roster;
        public string[] DeployedIds;
    }

    // Public fields are intentionally compatible with Unity JsonUtility.
    [Serializable]
    public sealed class ExpeditionRunSave
    {
        public int Version;
        public int Wave, Lantern, Victories, NextMemberNumber, Gold, LastReward, RewardedWave;
        public bool LastVictory;
        public ExpeditionPhase Phase;
        public uint RandomState;
        public string LastSummonedFamily;
        public int FamilyStreak;
        public CatalogRosterSave Roster;
        public string[] DeployedIds;
        // Most recent preparation action first. Summons are written into every older entry.
        public ExpeditionPreparationSave[] UndoHistory;
    }

    public sealed class ExpeditionRun
    {
        public const int TotalWaves = 10;
        public const int SummonCost = 20;
        const int MaxDeployed = 5;
        const int MaxBench = 7;
        static readonly int[] VictoryRewards = { 20, 20, 20, 30, 50, 25, 25, 25, 35, 0 };
        static readonly string[] Families = { "KN", "AR", "FI", "PR", "IC", "BA" };
        static readonly string[] Encounters =
        {
            "숲길 입구", "가시등 숲길", "야수의 소굴", "돌격 뿔짐승", "잿갈기 우두머리",
            "무너진 성채", "성채 수비선", "성채의 무리", "갑주 석상", "성문 골렘"
        };
        static readonly string[] EncounterDescriptions =
        {
            "새끼 뿔짐승 2", "새끼 뿔짐승 2 · 가시등 짐승 1",
            "새끼 뿔짐승 2 · 가시등 짐승 2", "돌격 뿔짐승 1 · 일반 뿔짐승 2 · 가시등 짐승 1",
            "잿갈기 우두머리 1 · 일반 졸개 2", "돌편 보병 2 · 석궁 수비병 1",
            "돌편 보병 2 · 석궁 수비병 2", "돌편 보병 3 · 석궁 수비병 2",
            "갑주 석상 1 · 돌편 보병 3 · 석궁 수비병 2", "성문 골렘 1 · 석궁 수비병 2"
        };

        readonly CatalogDatabase catalog;
        readonly List<string> deployedIds = new List<string>(5);
        readonly Stack<ExpeditionPreparationSave> history = new Stack<ExpeditionPreparationSave>();
        CatalogBattle battle;
        uint randomState;
        string lastSummonedFamily;
        int familyStreak, nextMemberNumber;
        int RewardedWave;

        public CatalogRoster Roster { get; private set; }
        public IReadOnlyList<string> DeployedIds { get { return deployedIds.AsReadOnly(); } }
        public int Wave { get; private set; }
        public int Lantern { get; private set; }
        public int Gold { get; private set; }
        public int LastReward { get; private set; }
        public int SummonsRemaining { get { return Gold / SummonCost; } }
        public bool CanSummon { get { return Phase == ExpeditionPhase.Preparation &&
            Gold >= SummonCost && Roster.Members.Count - deployedIds.Count < MaxBench; } }
        public bool CanUndo { get { return Phase == ExpeditionPhase.Preparation && history.Count > 0 &&
            FitsCapacity(history.Peek().Roster.Members.Length, history.Peek().DeployedIds.Length); } }
        public bool IsBossWave { get { return Wave == 5 || Wave == TotalWaves; } }
        public int Victories { get; private set; }
        public bool LastVictory { get; private set; }
        public ExpeditionPhase Phase { get; private set; }
        public string EncounterName { get { return Encounters[Wave - 1]; } }
        public string EncounterDescription { get { return EncounterDescriptions[Wave - 1]; } }

        ExpeditionRun(CatalogDatabase catalog)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            this.catalog = catalog;
        }

        public static ExpeditionRun New(CatalogDatabase catalog, uint seed)
        {
            var run = new ExpeditionRun(catalog);
            run.randomState = seed == 0 ? 0x6d2b79f5u : seed;
            run.Roster = new CatalogRoster(catalog);
            run.Wave = 1;
            run.Lantern = 3;
            run.Gold = 40;
            run.Phase = ExpeditionPhase.Preparation;
            run.RecruitStart("B-KN", -1.3f, 0f);
            run.RecruitStart("B-AR", -3.5f, .8f);
            run.RecruitStart("B-PR", -3.5f, -.8f);
            return run;
        }

        void RecruitStart(string formId, float x, float y)
        {
            string id = NextId();
            if (!Roster.Recruit(id, formId, x, y)) throw new InvalidOperationException("Starting roster failed.");
            deployedIds.Add(id);
        }

        string NextId() { return "ally-" + (++nextMemberNumber).ToString("00"); }

        public string Summon()
        {
            if (!CanSummon) return null;
            int excluded = familyStreak >= 3 ? Array.IndexOf(Families, lastSummonedFamily) : -1;
            int index = NextIndex(excluded < 0 ? Families.Length : Families.Length - 1);
            if (excluded >= 0 && index >= excluded) index++;
            string family = Families[index];
            string id = "ally-" + (nextMemberNumber + 1).ToString("00");
            if (!Roster.Recruit(id, "B-" + family, -2f, 0f))
                throw new InvalidOperationException("Summon could not join the roster.");
            nextMemberNumber++;
            Gold -= SummonCost;
            familyStreak = family == lastSummonedFamily ? familyStreak + 1 : 1;
            lastSummonedFamily = family;
            // Preparation Undo must retain this confirmed summon even if an older action is undone.
            foreach (ExpeditionPreparationSave snapshot in history)
            {
                var members = new List<CatalogMemberSave>(snapshot.Roster.Members);
                members.Add(new CatalogMemberSave { Id = id, FormId = "B-" + family, X = -2f, Y = 0f });
                snapshot.Roster.Members = members.ToArray();
                snapshot.Roster.SupplyUsed++;
            }
            return id;
        }

        uint NextRandom()
        {
            uint x = randomState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            randomState = x;
            return x;
        }

        int NextIndex(int count)
        {
            // xorshift32 has a 2^32-1 state cycle; discard its short tail for equal weights.
            uint limit = (uint)(uint.MaxValue / (uint)count * (uint)count);
            uint value;
            do { value = NextRandom(); } while (value > limit);
            return (int)((value - 1u) % (uint)count);
        }

        static bool FitsCapacity(int members, int deployed)
        {
            return deployed >= 1 && deployed <= MaxDeployed && members >= deployed &&
                members - deployed <= MaxBench;
        }

        static int TotalEarned(int rewardedWave)
        {
            int total = 40;
            for (int i = 0; i < rewardedWave; i++) total += VictoryRewards[i];
            return total;
        }

        public bool SetDeployed(string id, bool deployed)
        {
            if (Phase != ExpeditionPhase.Preparation || Roster.Find(id) == null) return false;
            bool exists = deployedIds.Contains(id);
            if (exists == deployed || deployed && deployedIds.Count >= MaxDeployed ||
                !deployed && (deployedIds.Count <= 1 ||
                Roster.Members.Count - deployedIds.Count >= MaxBench))
                return false;
            SavePreparation();
            if (deployed) deployedIds.Add(id); else deployedIds.Remove(id);
            return true;
        }

        public bool SwapDeployment(string activeId, string benchId)
        {
            if (Phase != ExpeditionPhase.Preparation || Roster.Find(benchId) == null ||
                deployedIds.Contains(benchId)) return false;
            int slot = deployedIds.IndexOf(activeId);
            if (slot < 0) return false;
            SavePreparation();
            deployedIds[slot] = benchId;
            return true;
        }

        public bool Move(string id, float x, float y)
        {
            CatalogMember member = Roster.Find(id);
            if (Phase != ExpeditionPhase.Preparation || member == null ||
                float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) ||
                member.X == x && member.Y == y) return false;
            SavePreparation();
            member.X = x; member.Y = y;
            return true;
        }

        public bool Transfer(string donorId, string recipientId, bool replaceTrait)
        {
            if (Phase != ExpeditionPhase.Preparation) return false;
            ExpeditionPreparationSave before = CapturePreparation();
            if (!Roster.Transfer(donorId, recipientId, replaceTrait)) return false;
            history.Push(before);
            ReplaceConsumedDeployment(donorId, recipientId);
            Roster.CommitPreparation();
            return true;
        }

        public bool Evolve(string memberId, string nextFormId)
        {
            if (Phase != ExpeditionPhase.Preparation) return false;
            ExpeditionPreparationSave before = CapturePreparation();
            if (!Roster.Evolve(memberId, nextFormId)) return false;
            history.Push(before);
            Roster.CommitPreparation();
            return true;
        }

        public bool Combine(string anchorId, string otherId, string resultId)
        {
            if (Phase != ExpeditionPhase.Preparation) return false;
            ExpeditionPreparationSave before = CapturePreparation();
            if (!Roster.Combine(anchorId, otherId, resultId)) return false;
            history.Push(before);
            ReplaceConsumedDeployment(otherId, anchorId);
            Roster.CommitPreparation();
            return true;
        }

        void ReplaceConsumedDeployment(string consumedId, string survivingId)
        {
            int slot = deployedIds.IndexOf(consumedId);
            if (slot < 0) return;
            if (deployedIds.Contains(survivingId)) deployedIds.RemoveAt(slot);
            else deployedIds[slot] = survivingId;
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            ExpeditionPreparationSave previous = history.Pop();
            Roster = CatalogRoster.Restore(catalog, previous.Roster);
            Roster.CommitPreparation();
            deployedIds.Clear();
            deployedIds.AddRange(previous.DeployedIds);
            return true;
        }

        void SavePreparation() { history.Push(CapturePreparation()); }

        ExpeditionPreparationSave CapturePreparation()
        {
            CatalogRosterSave roster = Roster.Capture();
            roster.History = new CatalogRosterHistorySave[0];
            return new ExpeditionPreparationSave { Roster = roster, DeployedIds = deployedIds.ToArray() };
        }

        public CatalogBattle BeginBattle()
        {
            if (Phase != ExpeditionPhase.Preparation || deployedIds.Count < 1 || deployedIds.Count > MaxDeployed)
                return null;
            foreach (string id in deployedIds) if (Roster.Find(id) == null) return null;
            history.Clear();
            Roster.CommitPreparation();
            Phase = ExpeditionPhase.Battle;
            LastReward = 0;
            battle = CreateBattle();
            return battle;
        }

        // Continue from the hub replays the saved battle from its deployment and full health.
        public CatalogBattle ResumeBattle()
        {
            if (Phase != ExpeditionPhase.Battle) return null;
            battle = CreateBattle();
            return battle;
        }

        CatalogBattle CreateBattle()
        {
            var result = new CatalogBattle(catalog);
            foreach (string id in deployedIds)
            {
                CatalogMember member = Roster.Find(id);
                result.AddUnit(member.Id, CatalogTeam.Ally, member.FormId, member.BranchId,
                    member.InheritedTraitId, member.X, member.Y);
            }
            switch (Wave)
            {
                case 1: AddMelee(result, "beast", 2, 74f, 8f, 6f); break;
                case 2:
                    AddMelee(result, "beast", 2, 84f, 9f, 6f);
                    AddRanged(result, "spine-raider", 1, 65f, 7f); break;
                case 3:
                    AddMelee(result, "beast", 2, 150f, 14f, 7f);
                    AddRanged(result, "spine-raider", 2, 110f, 11f); break;
                case 4:
                    result.AddEnemy("elite-beast", 340f, 25f, 12f, 1.8f, 1f, .7f,
                        false, 3.25f, 0f, CatalogEnemyRole.Beast);
                    AddMelee(result, "beast", 2, 145f, 13f, 7f);
                    AddRanged(result, "spine-raider", 1, 100f, 11f); break;
                case 5:
                    result.AddEnemy("beast-boss", 3200f, 53f, 20f, 1.65f, 1.1f, .65f,
                        true, 3.7f, 0f, CatalogEnemyRole.Beast, true);
                    AddMelee(result, "beast", 2, 90f, 9f, 7f); break;
                case 6:
                    AddMelee(result, "stone-infantry", 2, 240f, 20f, 14f);
                    AddRanged(result, "crossbow-raider", 1, 150f, 16f); break;
                case 7:
                    AddMelee(result, "stone-infantry", 2, 260f, 21f, 14f);
                    AddRanged(result, "crossbow-raider", 2, 160f, 17f); break;
                case 8:
                    AddMelee(result, "stone-infantry", 3, 260f, 22f, 15f);
                    AddRanged(result, "crossbow-raider", 2, 180f, 18f); break;
                case 9:
                    result.AddEnemy("stone-elite", 650f, 35f, 22f, 1.8f, 1f, .55f,
                        false, 3.45f, 0f, CatalogEnemyRole.Beast);
                    AddMelee(result, "stone-infantry", 3, 270f, 23f, 16f);
                    AddRanged(result, "crossbow-raider", 2, 190f, 20f); break;
                case 10:
                    result.AddEnemy("golem-boss", 5000f, 80f, 25f, 1.8f, 1.1f, .48f,
                        true, 4.1f, 0f, CatalogEnemyRole.Golem, true);
                    AddRanged(result, "crossbow-raider", 2, 150f, 15f, true); break;
            }
            return result;
        }

        static void AddMelee(CatalogBattle battle, string prefix, int count, float hp, float power, float defense)
        {
            for (int i = 0; i < count; i++)
            {
                float y = (i - (count - 1) * .5f) * 1.15f;
                battle.AddEnemy(prefix + "-" + (i + 1), hp, power, defense, 1.8f, 1f, .65f,
                    false, 2.1f + (i % 2) * .55f, y, CatalogEnemyRole.Beast);
            }
        }

        static void AddRanged(CatalogBattle battle, string prefix, int count, float hp, float power, bool bossEscort = false)
        {
            for (int i = 0; i < count; i++)
            {
                float y = count == 1 ? 1.2f : (i == 0 ? -1 : 1) * (bossEscort ? 2.15f : 1.35f);
                battle.AddEnemy(prefix + "-" + (i + 1), hp, power, 4f, 2f, 3.6f, .55f,
                    false, bossEscort ? 2.8f : 3.2f + i * .5f, y, CatalogEnemyRole.Raider);
            }
        }

        public bool ResolveBattle(bool victory)
        {
            if (Phase != ExpeditionPhase.Battle) return false;
            LastVictory = victory;
            if (victory)
            {
                if (RewardedWave != Wave - 1) throw new InvalidOperationException("Victory reward sequence is invalid.");
                Victories++;
                RewardedWave = Wave;
                LastReward = VictoryRewards[Wave - 1];
                Gold += LastReward;
            }
            else { Lantern--; LastReward = 0; }
            battle = null;
            Phase = ExpeditionPhase.Result;
            return true;
        }

        public bool Continue()
        {
            if (Phase != ExpeditionPhase.Result) return false;
            if (Lantern <= 0)
                Phase = ExpeditionPhase.Defeated;
            else if (Wave == TotalWaves && LastVictory)
                Phase = ExpeditionPhase.Completed;
            else
            {
                if (LastVictory) Wave++;
                Phase = ExpeditionPhase.Preparation;
            }
            return true;
        }

        public ExpeditionRunSave Capture()
        {
            var snapshots = new List<ExpeditionPreparationSave>();
            foreach (ExpeditionPreparationSave item in history)
                snapshots.Add(new ExpeditionPreparationSave {
                    Roster = CatalogRoster.Restore(catalog, item.Roster).Capture(),
                    DeployedIds = (string[])item.DeployedIds.Clone() });
            return new ExpeditionRunSave { Version = 2, Wave = Wave, Lantern = Lantern,
                Gold = Gold, LastReward = LastReward, RewardedWave = RewardedWave,
                Victories = Victories, LastVictory = LastVictory,
                Phase = Phase, RandomState = randomState, LastSummonedFamily = lastSummonedFamily,
                FamilyStreak = familyStreak, NextMemberNumber = nextMemberNumber,
                Roster = Roster.Capture(), DeployedIds = deployedIds.ToArray(), UndoHistory = snapshots.ToArray() };
        }

        public static ExpeditionRun Restore(CatalogDatabase catalog, ExpeditionRunSave save)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            string lastFamily = save == null || string.IsNullOrEmpty(save.LastSummonedFamily) ?
                null : save.LastSummonedFamily;
            if (save == null || save.Version != 2 || save.Wave < 1 || save.Wave > TotalWaves ||
                save.Lantern < 0 || save.Lantern > 3 || save.Gold < 0 ||
                save.Gold > TotalEarned(TotalWaves) ||
                save.LastReward < 0 || save.RewardedWave < 0 || save.RewardedWave > TotalWaves ||
                save.Victories != save.RewardedWave || save.NextMemberNumber < 3 ||
                save.RandomState == 0 || save.FamilyStreak < 0 ||
                save.FamilyStreak > save.NextMemberNumber - 3 ||
                (save.FamilyStreak == 0) != (lastFamily == null) ||
                lastFamily != null && Array.IndexOf(Families, lastFamily) < 0 ||
                save.DeployedIds == null || save.UndoHistory == null ||
                !Enum.IsDefined(typeof(ExpeditionPhase), save.Phase))
                throw new ArgumentException("Invalid expedition save.");
            var run = new ExpeditionRun(catalog);
            run.Roster = CatalogRoster.Restore(catalog, save.Roster);
            run.Wave = save.Wave; run.Lantern = save.Lantern;
            run.Gold = save.Gold; run.LastReward = save.LastReward;
            run.RewardedWave = save.RewardedWave; run.Victories = save.Victories;
            run.LastVictory = save.LastVictory; run.Phase = save.Phase;
            run.randomState = save.RandomState; run.lastSummonedFamily = lastFamily;
            run.familyStreak = save.FamilyStreak; run.nextMemberNumber = save.NextMemberNumber;
            run.deployedIds.AddRange(save.DeployedIds);
            run.ValidateDeployment();
            if (run.Roster.SupplyUsed != run.nextMemberNumber ||
                run.Gold != (long)TotalEarned(run.RewardedWave) -
                    (long)SummonCost * (run.Roster.SupplyUsed - 3))
                throw new ArgumentException("Saved supply and member IDs disagree.");
            if (save.Phase != ExpeditionPhase.Preparation && save.UndoHistory.Length != 0 ||
                save.RewardedWave != save.Wave -
                    ((save.Phase == ExpeditionPhase.Result && save.LastVictory) ||
                     save.Phase == ExpeditionPhase.Completed ? 0 : 1) ||
                save.Phase == ExpeditionPhase.Result && save.LastVictory &&
                    save.LastReward != VictoryRewards[save.Wave - 1] ||
                save.Phase == ExpeditionPhase.Result && !save.LastVictory && save.LastReward != 0 ||
                save.Phase == ExpeditionPhase.Completed && (save.Wave != TotalWaves || !save.LastVictory) ||
                save.Phase == ExpeditionPhase.Completed && save.Lantern == 0 ||
                save.Phase == ExpeditionPhase.Result && save.LastVictory && save.Lantern == 0 ||
                save.Phase == ExpeditionPhase.Defeated && save.Lantern != 0 ||
                (save.Phase == ExpeditionPhase.Battle || save.Phase == ExpeditionPhase.Completed ||
                 save.Phase == ExpeditionPhase.Defeated) && save.LastReward != 0 ||
                save.Phase == ExpeditionPhase.Preparation && save.LastReward != 0 &&
                    (save.RewardedWave == 0 || save.LastReward != VictoryRewards[save.RewardedWave - 1]) ||
                (save.Phase == ExpeditionPhase.Preparation || save.Phase == ExpeditionPhase.Battle) &&
                    save.Lantern == 0 ||
                !FitsCapacity(run.Roster.Members.Count, run.deployedIds.Count))
                throw new ArgumentException("Invalid saved preparation state.");
            for (int i = save.UndoHistory.Length - 1; i >= 0; i--)
            {
                ExpeditionPreparationSave item = save.UndoHistory[i];
                if (item == null || item.DeployedIds == null) throw new ArgumentException("Invalid undo history.");
                CatalogRoster oldRoster = CatalogRoster.Restore(catalog, item.Roster);
                ValidateDeployment(item.DeployedIds, oldRoster);
                if (oldRoster.SupplyUsed != run.Roster.SupplyUsed)
                    throw new ArgumentException("Undo history has inconsistent supply.");
                run.history.Push(new ExpeditionPreparationSave { Roster = oldRoster.Capture(),
                    DeployedIds = (string[])item.DeployedIds.Clone() });
            }
            return run;
        }

        void ValidateDeployment()
        {
            ValidateDeployment(deployedIds, Roster);
        }

        static void ValidateDeployment(IList<string> ids, CatalogRoster roster)
        {
            if (ids.Count < 1 || ids.Count > 5)
                throw new ArgumentException("Invalid deployed count.");
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
                if (string.IsNullOrEmpty(id) || !distinct.Add(id) || roster.Find(id) == null)
                    throw new ArgumentException("Invalid deployed member.");
        }
    }
}
