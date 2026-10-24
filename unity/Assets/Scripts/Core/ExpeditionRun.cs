using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public enum ExpeditionPhase { Preparation, Battle, Result, Completed, Defeated }
    public enum ExpeditionDifficulty { Normal = 0, Hard = 1 }

    public sealed class CatalogCombinationOption
    {
        public readonly string RecipientId, DonorId, ResultId;
        public CatalogCombinationOption(string recipientId, string donorId, string resultId)
        { RecipientId = recipientId; DonorId = donorId; ResultId = resultId; }
    }

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
        public ExpeditionDifficulty Difficulty;
        public int ContentWaves;
        public int PreviousClearWaves;
        public string[] TacticIds, LegacyResearchIds;
        public string StartFormationId;
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
        public const int TotalWaves = 30;
        public const int ReleaseWaves = 30;
        public const int SummonCost = 20;
        const int MaxDeployed = 5;
        const int MaxBench = 7;
        static readonly int[] VictoryRewards = { 20, 20, 20, 30, 50, 25, 25, 25, 35, 50,
            30, 30, 35, 40, 50, 35, 35, 40, 45, 50, 40, 40, 45, 50, 50,
            45, 50, 50, 55, 0 };
        static readonly int[] LegacyVictoryRewards = { 20, 20, 20, 30, 50, 25, 25, 25, 35, 0 };
        static readonly string[] Families = { "KN", "AR", "FI", "PR", "IC", "BA" };
        static readonly string[] Encounters =
        {
            "숲길 입구", "가시등 숲길", "야수의 소굴", "돌격 뿔짐승", "잿갈기 우두머리",
            "무너진 성채", "성채 수비선", "성채의 무리", "갑주 석상", "성문 골렘",
            "폐광 입구", "잿가루 통로", "광차 갈림길", "철쇄 감독관", "용광로 수호자",
            "서리 성당 입구", "얼어붙은 회랑", "서리 제단", "찬송의 감시자", "서리의 파수꾼",
            "황혼 정원 입구", "독가시 산책로", "시든 온실", "황혼 정원사", "가시의 군주",
            "잿빛 왕궁 입구", "왕궁의 회랑", "검은 왕좌 길", "왕궁 집행관", "왕관의 수호룡"
        };
        static readonly string[] EncounterDescriptions =
        {
            "새끼 뿔짐승 2", "새끼 뿔짐승 2 · 가시등 짐승 1",
            "새끼 뿔짐승 1 · 가시등 짐승 3 (측면 1)", "돌격 뿔짐승 1 · 일반 뿔짐승 2 · 가시등 짐승 1",
            "잿갈기 우두머리 1 · 일반 졸개 2", "돌편 보병 2 · 석궁 수비병 1",
            "돌편 보병 2 · 석궁 수비병 2", "돌편 보병 3 · 석궁 수비병 2",
            "갑주 석상 1 · 돌편 보병 3 · 석궁 수비병 2", "성문 골렘 1 · 석궁 수비병 2",
            "채굴병 2 · 불씨 투척병 1", "채굴병 2 · 불씨 투척병 2",
            "채굴병 3 · 불씨 투척병 2", "철쇄 감독관 1 · 채굴병 2 · 불씨 투척병 2",
            "용광로 수호자 1 · 철쇄 감독관 1 · 불씨 투척병 2",
            "서리 근위병 2 · 서리 시종 2", "서리 근위병 3 · 서리 시종 2",
            "서리 근위병 3 · 서리 시종 3", "찬송 감시자 1 · 서리 근위병 2 · 서리 시종 2",
            "서리의 파수꾼 1 · 서리 시종 2",
            "가시 근위병 2 · 독침 사냥꾼 2", "가시 근위병 3 · 독침 사냥꾼 2",
            "가시 근위병 3 · 독침 사냥꾼 3", "황혼 정원사 1 · 가시 근위병 2 · 독침 사냥꾼 2",
            "가시의 군주 1 · 독침 사냥꾼 2",
            "왕궁 근위병 2 · 왕궁 석궁병 2", "왕궁 근위병 3 · 왕궁 석궁병 2",
            "왕궁 근위병 3 · 왕궁 석궁병 3", "왕궁 집행관 1 · 왕궁 근위병 2 · 왕궁 석궁병 2",
            "왕관의 수호룡 1 · 왕궁 석궁병 2"
        };

        readonly CatalogDatabase catalog;
        readonly List<string> deployedIds = new List<string>(5);
        readonly List<string> tacticIds = new List<string>(CatalogResearch.MaxNodes);
        readonly List<string> legacyResearchIds = new List<string>();
        readonly Stack<ExpeditionPreparationSave> history = new Stack<ExpeditionPreparationSave>();
        CatalogBattle battle;
        uint randomState;
        string lastSummonedFamily;
        int familyStreak, nextMemberNumber;
        int RewardedWave;

        public CatalogRoster Roster { get; private set; }
        public IReadOnlyList<string> DeployedIds { get { return deployedIds.AsReadOnly(); } }
        public IReadOnlyList<string> TacticIds { get { return tacticIds.AsReadOnly(); } }
        public IReadOnlyList<string> LegacyResearchIds { get { return legacyResearchIds.AsReadOnly(); } }
        public string StartFormationId { get; private set; }
        public int Wave { get; private set; }
        public ExpeditionDifficulty Difficulty { get; private set; }
        public int ContentWaves { get; private set; }
        public int PreviousClearWaves { get; private set; }
        public int Lantern { get; private set; }
        public int Gold { get; private set; }
        public int LastReward { get; private set; }
        public int SummonsRemaining { get { return Gold / SummonCost; } }
        public bool CanSummon { get { return Phase == ExpeditionPhase.Preparation &&
            Gold >= SummonCost && Roster.Members.Count - deployedIds.Count < MaxBench; } }
        public bool CanUndo { get { return Phase == ExpeditionPhase.Preparation && history.Count > 0 &&
            FitsCapacity(history.Peek().Roster.Members.Length, history.Peek().DeployedIds.Length); } }
        public bool IsBossWave { get { return Wave % 5 == 0; } }
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

        public static ExpeditionRun New(CatalogDatabase catalog, uint seed,
            ExpeditionDifficulty difficulty = ExpeditionDifficulty.Normal,
            IEnumerable<string> legacyResearchIds = null, string startFormationId = "standard")
        {
            if (difficulty != ExpeditionDifficulty.Normal)
                throw new ArgumentOutOfRangeException("difficulty", "Difficulty is unavailable.");
            var selectedLegacy = new List<string>(legacyResearchIds ?? new string[0]);
            CatalogLegacyResearch.ValidateSelection(selectedLegacy);
            if (!CatalogLegacyResearch.IsFormationAvailable(startFormationId, selectedLegacy))
                throw new ArgumentException("Starting formation is unavailable.", "startFormationId");
            string[] startingForms = CatalogLegacyResearch.GetFormationUnits(startFormationId);
            var run = new ExpeditionRun(catalog);
            run.Difficulty = difficulty;
            run.ContentWaves = TotalWaves;
            run.legacyResearchIds.AddRange(selectedLegacy);
            run.StartFormationId = startFormationId;
            run.randomState = seed == 0 ? 0x6d2b79f5u : seed;
            run.Roster = new CatalogRoster(catalog);
            run.Wave = 1;
            run.Lantern = 3;
            run.Gold = 40;
            run.Phase = ExpeditionPhase.Preparation;
            run.RecruitStart(startingForms[0], -1.3f, 0f);
            run.RecruitStart(startingForms[1], -3.5f, .8f);
            run.RecruitStart(startingForms[2], -3.5f, -.8f);
            return run;
        }

        public bool CanPurchaseTactic(string id)
        {
            CatalogResearchNode node = CatalogResearch.Get(id);
            return Phase == ExpeditionPhase.Preparation && node != null &&
                Wave >= node.UnlockWave && Gold >= node.Cost &&
                tacticIds.Count < CatalogResearch.MaxNodes && !tacticIds.Contains(id) &&
                (node.PrerequisiteId == null || tacticIds.Contains(node.PrerequisiteId));
        }

        public bool PurchaseTactic(string id)
        {
            if (!CanPurchaseTactic(id)) return false;
            CatalogResearchNode node = CatalogResearch.Get(id);
            tacticIds.Add(id);
            Gold -= node.Cost;
            // A confirmed research purchase is outside preparation Undo, like a summon.
            return true;
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

        static int TotalEarned(int rewardedWave, int[] rewards)
        {
            int total = 40;
            for (int i = 0; i < rewardedWave; i++) total += rewards[i];
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

        public bool SetMaterialLocked(string id, bool locked)
        {
            if (Phase != ExpeditionPhase.Preparation || !Roster.SetMaterialLocked(id, locked)) return false;
            // Protection is a preference, not a growth action. Undo must not silently remove it.
            foreach (var snapshot in history)
                foreach (var member in snapshot.Roster.Members)
                    if (member.Id == id) member.MaterialLocked = locked;
            return true;
        }

        public bool CanTransfer(string donorId, string recipientId)
        {
            return Phase == ExpeditionPhase.Preparation && Roster.CanTransfer(donorId, recipientId);
        }

        public bool CanCombine(string anchorId, string otherId, string resultId)
        {
            return Phase == ExpeditionPhase.Preparation && Roster.CanCombine(anchorId, otherId, resultId);
        }

        public string PreviewCombination(string donorId, string recipientId)
        {
            var donor = Roster.Find(donorId);
            var recipient = Roster.Find(recipientId);
            if (donor == null || recipient == null) return null;
            string result = Roster.HybridResult(donor.FormId, recipient.FormId);
            return CanCombine(recipientId, donorId, result) ? result : null;
        }

        public List<CatalogCombinationOption> GetCombinationOptions()
        {
            var result = new List<CatalogCombinationOption>();
            if (Phase != ExpeditionPhase.Preparation) return result;
            var members = Roster.Members;
            for (int i = 0; i < members.Count; i++)
                for (int j = i + 1; j < members.Count; j++)
                {
                    CatalogMember recipient = members[i], donor = members[j];
                    string form = Roster.HybridResult(recipient.FormId, donor.FormId);
                    if (form == null || recipient.MaterialLocked && donor.MaterialLocked) continue;
                    // Preserve protected members; otherwise retain the field member's position.
                    if (donor.MaterialLocked || !recipient.MaterialLocked &&
                        !deployedIds.Contains(recipient.Id) && deployedIds.Contains(donor.Id))
                    { CatalogMember swap = recipient; recipient = donor; donor = swap; }
                    if (CanCombine(recipient.Id, donor.Id, form))
                        result.Add(new CatalogCombinationOption(recipient.Id, donor.Id, form));
                }
            return result;
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
            var result = new CatalogBattle(catalog, tacticIds, legacyResearchIds);
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
                    AddMelee(result, "beast", 1, 190f, 24f, 7f);
                    AddRanged(result, "spine-raider", 3, 175f, 30f, false, true); break;
                case 4:
                    result.AddEnemy("elite-beast", 550f, 62f, 12f, 1.8f, 1f, .7f,
                        false, 3.25f, 0f, CatalogEnemyRole.Beast);
                    AddMelee(result, "beast", 2, 220f, 40f, 7f);
                    AddRanged(result, "spine-raider", 1, 165f, 43f); break;
                case 5:
                    result.AddEnemy("beast-boss", 2500f, 46f, 20f, 1.65f, 1.1f, .65f,
                        true, 3.7f, 0f, CatalogEnemyRole.Beast, true);
                    AddMelee(result, "beast", 2, 90f, 9f, 7f); break;
                case 6:
                    AddMelee(result, "stone-infantry", 2, 400f, 40f, 14f);
                    AddRanged(result, "crossbow-raider", 1, 260f, 35f); break;
                case 7:
                    AddMelee(result, "stone-infantry", 2, 420f, 45f, 14f);
                    AddRanged(result, "crossbow-raider", 2, 270f, 40f); break;
                case 8:
                    AddMelee(result, "stone-infantry", 3, 440f, 48f, 15f);
                    AddRanged(result, "crossbow-raider", 2, 300f, 45f); break;
                case 9:
                    result.AddEnemy("stone-elite", 770f, 55f, 22f, 1.8f, 1f, .55f,
                        false, 3.45f, 0f, CatalogEnemyRole.Beast);
                    AddMelee(result, "stone-infantry", 3, 370f, 40f, 16f);
                    AddRanged(result, "crossbow-raider", 2, 275f, 38f); break;
                case 10:
                    result.AddEnemy("golem-boss", 4200f, 80f, 25f, 1.8f, 1.1f, .48f,
                        true, 4.1f, 0f, CatalogEnemyRole.Golem, true);
                    AddRanged(result, "crossbow-raider", 2, 150f, 15f, true); break;
                case 11:
                    AddMineWorkers(result, 2, 550f, 50f, 12f);
                    AddMineSlingers(result, 1, 380f, 42f); break;
                case 12:
                    AddMineWorkers(result, 2, 620f, 55f, 15f);
                    AddMineSlingers(result, 2, 410f, 47f); break;
                case 13:
                    AddMineWorkers(result, 3, 700f, 60f, 16f);
                    AddMineSlingers(result, 2, 450f, 51f); break;
                case 14:
                    result.AddEnemy("mine-elite", 1200f, 66f, 24f, 1.8f, 1f, .57f,
                        false, 3.45f, 0f, CatalogEnemyRole.Supervisor);
                    AddMineWorkers(result, 2, 660f, 59f, 18f);
                    AddMineSlingers(result, 2, 450f, 51f); break;
                case 15:
                    result.AddEnemy("furnace-boss", 6000f, 98f, 25f, 1.8f, 1.1f, .48f,
                        true, 4.1f, 0f, CatalogEnemyRole.Furnace, true);
                    result.AddEnemy("mine-elite", 850f, 52f, 20f, 1.8f, 1f, .57f,
                        false, 3f, -1.25f, CatalogEnemyRole.Supervisor);
                    AddMineSlingers(result, 2, 300f, 36f); break;
                case 16:
                    AddFrostGuards(result, 2, 790f, 67f, 17f);
                    AddRanged(result, "frost-acolyte", 2, 500f, 55f); break;
                case 17:
                    AddFrostGuards(result, 3, 930f, 75f, 17f);
                    AddRanged(result, "frost-acolyte", 2, 590f, 61f); break;
                case 18:
                    AddFrostGuards(result, 3, 1040f, 82f, 18f);
                    AddRanged(result, "frost-acolyte", 3, 660f, 67f); break;
                case 19:
                    result.AddEnemy("frost-cantor", 1650f, 90f, 27f, 1.8f, 1f, .57f,
                        false, 3.45f, 0f, CatalogEnemyRole.Supervisor);
                    AddFrostGuards(result, 2, 1100f, 86f, 18f);
                    AddRanged(result, "frost-acolyte", 2, 680f, 71f); break;
                case 20:
                    result.AddEnemy("frost-boss", 7400f, 110f, 28f, 1.8f, 1.1f, .48f,
                        true, 4.1f, 0f, CatalogEnemyRole.FrostWarden, true);
                    AddRanged(result, "frost-acolyte", 2, 380f, 46f, true); break;
                case 21:
                    AddGardenGuards(result, 2, 920f, 83f, 20f);
                    AddGardenSpitters(result, 2, 540f, 60f); break;
                case 22:
                    AddGardenGuards(result, 3, 1050f, 92f, 20f);
                    AddGardenSpitters(result, 2, 620f, 68f); break;
                case 23:
                    AddGardenGuards(result, 3, 1170f, 98f, 21f);
                    AddGardenSpitters(result, 3, 700f, 74f); break;
                case 24:
                    result.AddEnemy("garden-keeper", 1950f, 103f, 30f, 1.8f, 1f, .57f,
                        false, 3.45f, 0f, CatalogEnemyRole.Supervisor);
                    AddGardenGuards(result, 2, 1230f, 101f, 21f);
                    AddGardenSpitters(result, 2, 740f, 78f); break;
                case 25:
                    result.AddEnemy("garden-boss", 8500f, 125f, 32f, 1.8f, 1.1f, .48f,
                        true, 4.1f, 0f, CatalogEnemyRole.GardenLord, true);
                    AddGardenSpitters(result, 2, 450f, 60f, true); break;
                case 26:
                    AddPalaceGuards(result, 2, 1207.5f, 88f, 24f);
                    AddRanged(result, "palace-arbalist", 2, 713f, 70.4f); break;
                case 27:
                    AddPalaceGuards(result, 3, 1322.5f, 93.5f, 24f);
                    AddRanged(result, "palace-arbalist", 2, 782f, 77f); break;
                case 28:
                    AddPalaceGuards(result, 3, 1437.5f, 99f, 26f);
                    AddRanged(result, "palace-arbalist", 3, 851f, 81.4f); break;
                case 29:
                    result.AddEnemy("palace-sentinel", 2185f, 90f, 34f, 1.8f, 1f, .57f,
                        false, 3.45f, 0f, CatalogEnemyRole.Supervisor);
                    AddPalaceGuards(result, 2, 1518f, 103.4f, 26f);
                    AddRanged(result, "palace-arbalist", 2, 885.5f, 85.8f); break;
                case 30:
                    result.AddEnemy("palace-boss", 10800f, 145f, 36f, 1.8f, 1.1f, .48f,
                        true, 4.1f, 0f, CatalogEnemyRole.CrownDrake, true);
                    AddRanged(result, "palace-arbalist", 2, 600f, 78f, true); break;
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

        static void AddRanged(CatalogBattle battle, string prefix, int count, float hp, float power,
            bool bossEscort = false, bool flank = false)
        {
            for (int i = 0; i < count; i++)
            {
                bool isFlanker = flank && i == count - 1;
                float y = isFlanker ? 2.5f : count == 1 ? 1.2f : count == 3 ? (i - 1) * 1.35f :
                    (i == 0 ? -1 : 1) * (bossEscort ? 2.15f : 1.35f);
                battle.AddEnemy(prefix + "-" + (i + 1), hp, power, 4f, 2f, 3.6f, .55f,
                    false, isFlanker ? -.5f : bossEscort ? 2.8f : 3.2f + i * .5f,
                    y, CatalogEnemyRole.Raider);
            }
        }

        static void AddMineWorkers(CatalogBattle battle, int count, float hp, float power, float defense)
        {
            for (int i = 0; i < count; i++)
                battle.AddEnemy("mine-worker-" + (i + 1), hp, power, defense, 1.8f, 1f, .65f,
                    false, 2.1f + (i % 2) * .55f, (i - (count - 1) * .5f) * 1.15f,
                    CatalogEnemyRole.MineWorker);
        }

        static void AddFrostGuards(CatalogBattle battle, int count, float hp, float power, float defense)
        {
            for (int i = 0; i < count; i++)
                battle.AddEnemy("frost-guard-" + (i + 1), hp, power, defense, 1.8f, 1f, .65f,
                    false, 2.1f + (i % 2) * .55f, (i - (count - 1) * .5f) * 1.15f,
                    CatalogEnemyRole.MineWorker);
        }

        static void AddGardenGuards(CatalogBattle battle, int count, float hp, float power, float defense)
        {
            for (int i = 0; i < count; i++)
                battle.AddEnemy("garden-guard-" + (i + 1), hp, power, defense, 1.8f, 1f, .65f,
                    false, 2.1f + (i % 2) * .55f, (i - (count - 1) * .5f) * 1.15f,
                    CatalogEnemyRole.MineWorker);
        }

        static void AddPalaceGuards(CatalogBattle battle, int count, float hp, float power, float defense)
        {
            for (int i = 0; i < count; i++)
                battle.AddEnemy("palace-guard-" + (i + 1), hp, power, defense, 1.8f, 1f, .65f,
                    false, 2.1f + (i % 2) * .55f, (i - (count - 1) * .5f) * 1.15f,
                    CatalogEnemyRole.MineWorker);
        }

        static void AddGardenSpitters(CatalogBattle battle, int count, float hp, float power,
            bool bossEscort = false)
        {
            for (int i = 0; i < count; i++)
            {
                float y = count == 3 ? (i - 1) * 1.35f : (i == 0 ? -1f : 1f) *
                    (bossEscort ? 2.15f : 1.35f);
                battle.AddEnemy("garden-spitter-" + (i + 1), hp, power, 6f, 2f, 3.6f, .55f,
                    false, bossEscort ? 2.8f : 3.2f + i * .5f, y,
                    CatalogEnemyRole.GardenSpitter);
            }
        }

        static void AddMineSlingers(CatalogBattle battle, int count, float hp, float power)
        {
            for (int i = 0; i < count; i++)
                battle.AddEnemy("mine-slinger-" + (i + 1), hp, power, 5f, 2f, 3.6f, .55f,
                    false, 3.2f + i * .5f, count == 1 ? 1.2f : (i == 0 ? -1.35f : 1.35f),
                    CatalogEnemyRole.Raider);
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
            return new ExpeditionRunSave { Version = 8, Difficulty = Difficulty,
                ContentWaves = ContentWaves, PreviousClearWaves = PreviousClearWaves,
                TacticIds = tacticIds.ToArray(), LegacyResearchIds = legacyResearchIds.ToArray(),
                StartFormationId = StartFormationId,
                Wave = Wave, Lantern = Lantern,
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
            bool legacy = save != null && save.Version == 2;
            bool normalSave = save != null && save.Version >= 4;
            bool twenty = save != null && save.Version == 5;
            bool twentyFive = save != null && save.Version == 6;
            bool thirty = save != null && save.Version == 7;
            bool current = save != null && save.Version == 8;
            int saveWaves = legacy ? 10 : current || thirty ? TotalWaves : twentyFive ? 25 : twenty ? 20 : 15;
            // Validate old files against their original economy before granting expansion rewards.
            int[] fifteenRewards = { 20, 20, 20, 30, 50, 25, 25, 25, 35, 50,
                30, 30, 35, 40, 0 };
            int[] twentyRewards = { 20, 20, 20, 30, 50, 25, 25, 25, 35, 50,
                30, 30, 35, 40, 50, 35, 35, 40, 45, 0 };
            int[] twentyFiveRewards = { 20, 20, 20, 30, 50, 25, 25, 25, 35, 50,
                30, 30, 35, 40, 50, 35, 35, 40, 45, 50, 40, 40, 45, 50, 0 };
            int[] rewards = legacy ? LegacyVictoryRewards : current || thirty ? VictoryRewards :
                twentyFive ? twentyFiveRewards : twenty ? twentyRewards : fifteenRewards;
            if (save == null || save.Version < 2 || save.Version > 8 ||
                normalSave && save.Difficulty != ExpeditionDifficulty.Normal ||
                save.Version == 4 && save.ContentWaves != 15 ||
                twenty && (save.ContentWaves != 20 ||
                    save.PreviousClearWaves != 0 && save.PreviousClearWaves != 15 ||
                    save.PreviousClearWaves > save.Victories) ||
                twentyFive && (save.ContentWaves != 25 ||
                    save.PreviousClearWaves != 0 && save.PreviousClearWaves != 15 &&
                    save.PreviousClearWaves != 20 ||
                    save.PreviousClearWaves > save.Victories) ||
                (current || thirty) && (save.ContentWaves != TotalWaves ||
                    save.PreviousClearWaves != 0 && save.PreviousClearWaves != 15 &&
                    save.PreviousClearWaves != 20 && save.PreviousClearWaves != 25 ||
                    save.PreviousClearWaves > save.Victories) ||
                current && (save.TacticIds == null || save.LegacyResearchIds == null ||
                    !CatalogLegacyResearch.IsFormationAvailable(save.StartFormationId, save.LegacyResearchIds)) ||
                save.Wave < 1 || save.Wave > saveWaves ||
                save.Lantern < 0 || save.Lantern > 3 || save.Gold < 0 ||
                save.Gold > TotalEarned(saveWaves, rewards) ||
                save.LastReward < 0 || save.RewardedWave < 0 || save.RewardedWave > saveWaves ||
                save.Victories != save.RewardedWave || save.NextMemberNumber < 3 ||
                save.RandomState == 0 || save.FamilyStreak < 0 ||
                save.FamilyStreak > save.NextMemberNumber - 3 ||
                (save.FamilyStreak == 0) != (lastFamily == null) ||
                lastFamily != null && Array.IndexOf(Families, lastFamily) < 0 ||
                save.DeployedIds == null || save.UndoHistory == null ||
                !Enum.IsDefined(typeof(ExpeditionPhase), save.Phase))
                throw new ArgumentException("Invalid expedition save.");
            string[] selectedTactics = current ? save.TacticIds : new string[0];
            string[] selectedLegacy = current ? save.LegacyResearchIds : new string[0];
            int tacticCost = CatalogResearch.ValidateSelection(selectedTactics, save.Wave);
            CatalogLegacyResearch.ValidateSelection(selectedLegacy);
            var run = new ExpeditionRun(catalog);
            run.Difficulty = normalSave ? save.Difficulty : ExpeditionDifficulty.Normal;
            run.ContentWaves = TotalWaves;
            run.PreviousClearWaves = save.Version >= 5 ? save.PreviousClearWaves : 0;
            run.tacticIds.AddRange(selectedTactics);
            run.legacyResearchIds.AddRange(selectedLegacy);
            run.StartFormationId = current ? save.StartFormationId : "standard";
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
                run.Gold != (long)TotalEarned(run.RewardedWave, rewards) -
                    (long)SummonCost * (run.Roster.SupplyUsed - 3) - tacticCost)
                throw new ArgumentException("Saved supply and member IDs disagree.");
            if (save.Phase != ExpeditionPhase.Preparation && save.UndoHistory.Length != 0 ||
                save.RewardedWave != save.Wave -
                    ((save.Phase == ExpeditionPhase.Result && save.LastVictory) ||
                     save.Phase == ExpeditionPhase.Completed ? 0 : 1) ||
                save.Phase == ExpeditionPhase.Result && save.LastVictory &&
                    save.LastReward != rewards[save.Wave - 1] ||
                save.Phase == ExpeditionPhase.Result && !save.LastVictory && save.LastReward != 0 ||
                save.Phase == ExpeditionPhase.Completed && (save.Wave != saveWaves || !save.LastVictory) ||
                save.Phase == ExpeditionPhase.Completed && save.Lantern == 0 ||
                save.Phase == ExpeditionPhase.Result && save.LastVictory && save.Lantern == 0 ||
                save.Phase == ExpeditionPhase.Defeated && save.Lantern != 0 ||
                (save.Phase == ExpeditionPhase.Battle || save.Phase == ExpeditionPhase.Completed ||
                 save.Phase == ExpeditionPhase.Defeated) && save.LastReward != 0 ||
                save.Phase == ExpeditionPhase.Preparation && save.LastReward != 0 &&
                    (save.RewardedWave == 0 || save.LastReward != rewards[save.RewardedWave - 1]) ||
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
            // Convert only after the complete old save has passed its original rules.
            // Current-content captures include prior expansion grants, preventing duplicates on reload.
            if (saveWaves < TotalWaves && save.Wave == saveWaves && save.LastVictory &&
                (save.Phase == ExpeditionPhase.Result || save.Phase == ExpeditionPhase.Completed))
            {
                run.Gold += VictoryRewards[saveWaves - 1];
                run.LastReward = VictoryRewards[saveWaves - 1];
                if (saveWaves >= 15) run.PreviousClearWaves = saveWaves;
                if (save.Phase == ExpeditionPhase.Completed)
                {
                    run.Wave = saveWaves + 1;
                    run.Phase = ExpeditionPhase.Preparation;
                }
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
