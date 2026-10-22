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
        public int Wave, Lantern, SummonsRemaining, Victories, NextMemberNumber;
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
        public const int TotalWaves = 6;
        const int SupplyLimit = 14;
        static readonly string[] Families = { "KN", "AR", "FI", "PR", "IC", "BA" };
        static readonly string[] Encounters =
        {
            "숲의 짐승", "숲길 약탈자", "전초기지 수비대", "매복한 약탈자",
            "성문 수비대", "성문 골렘"
        };
        static readonly string[] EncounterDescriptions =
        {
            "야수 2 · 돌진 주의", "야수 2 · 약탈자 궁수 1", "야수 1 · 약탈자 궁수 2",
            "야수 2 · 약탈자 궁수 2", "야수 3 · 약탈자 궁수 2",
            "성문 골렘 1 · 호위 야수 1 · 약탈자 궁수 1"
        };

        readonly CatalogDatabase catalog;
        readonly List<string> deployedIds = new List<string>(5);
        readonly Stack<ExpeditionPreparationSave> history = new Stack<ExpeditionPreparationSave>();
        CatalogBattle battle;
        uint randomState;
        string lastSummonedFamily;
        int familyStreak, nextMemberNumber;

        public CatalogRoster Roster { get; private set; }
        public IReadOnlyList<string> DeployedIds { get { return deployedIds.AsReadOnly(); } }
        public int Wave { get; private set; }
        public int Lantern { get; private set; }
        public int SummonsRemaining { get; private set; }
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
            run.SummonsRemaining = 1;
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
            if (Phase != ExpeditionPhase.Preparation || SummonsRemaining <= 0 || Roster.SupplyUsed >= SupplyLimit)
                return null;
            int excluded = familyStreak >= 3 ? Array.IndexOf(Families, lastSummonedFamily) : -1;
            int index = NextIndex(excluded < 0 ? Families.Length : Families.Length - 1);
            if (excluded >= 0 && index >= excluded) index++;
            string family = Families[index];
            string id = NextId();
            if (!Roster.Recruit(id, "B-" + family, -2f, 0f))
                throw new InvalidOperationException("Summon could not join the roster.");
            SummonsRemaining--;
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

        public bool SetDeployed(string id, bool deployed)
        {
            if (Phase != ExpeditionPhase.Preparation || Roster.Find(id) == null) return false;
            bool exists = deployedIds.Contains(id);
            if (exists == deployed || deployed && deployedIds.Count >= 5 || !deployed && deployedIds.Count <= 1)
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
            if (Phase != ExpeditionPhase.Preparation || history.Count == 0) return false;
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
            if (Phase != ExpeditionPhase.Preparation || deployedIds.Count < 1 || deployedIds.Count > 5)
                return null;
            foreach (string id in deployedIds) if (Roster.Find(id) == null) return null;
            SummonsRemaining = 0;
            history.Clear();
            Roster.CommitPreparation();
            Phase = ExpeditionPhase.Battle;
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
            int beasts = Wave == 3 || Wave == 6 ? 1 : Wave == 5 ? 3 : 2;
            int raiders = Wave == 1 ? 0 : Wave == 2 || Wave == 6 ? 1 : 2;
            if (Wave == 6)
                result.AddEnemy("golem-boss", 700f, 21f, 15f, 1.55f, 1.1f, .48f,
                    true, 3.7f, 0f, CatalogEnemyRole.Golem);
            for (int i = 0; i < beasts; i++)
            {
                float y = beasts == 1 ? -.85f : (i - (beasts - 1) * .5f) * 1.15f;
                float x = 2.1f + (i % 2) * .55f;
                result.AddEnemy("beast-" + (i + 1), 74f + 10f * (Wave - 1),
                    8f + .8f * (Wave - 1), 6f, 1.8f, 1.0f, .65f,
                    false, x, y, CatalogEnemyRole.Beast);
            }
            for (int i = 0; i < raiders; i++)
            {
                float y = raiders == 1 ? 1.05f : (i == 0 ? -1.2f : 1.2f);
                result.AddEnemy("raider-" + (i + 1), 65f + 7f * (Wave - 1),
                    7f + .65f * (Wave - 1), 3f, 2f, 3.6f, .55f,
                    false, 3.35f + i * .5f, y, CatalogEnemyRole.Raider);
            }
            return result;
        }

        public bool ResolveBattle(bool victory)
        {
            if (Phase != ExpeditionPhase.Battle) return false;
            LastVictory = victory;
            if (victory) Victories++;
            else if (Wave < TotalWaves) Lantern--;
            battle = null;
            Phase = ExpeditionPhase.Result;
            return true;
        }

        public bool Continue()
        {
            if (Phase != ExpeditionPhase.Result) return false;
            if (Lantern <= 0 || Wave == TotalWaves && !LastVictory)
                Phase = ExpeditionPhase.Defeated;
            else if (Wave == TotalWaves)
                Phase = ExpeditionPhase.Completed;
            else
            {
                Wave++;
                SummonsRemaining = 2;
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
            return new ExpeditionRunSave { Version = 1, Wave = Wave, Lantern = Lantern,
                SummonsRemaining = SummonsRemaining, Victories = Victories, LastVictory = LastVictory,
                Phase = Phase, RandomState = randomState, LastSummonedFamily = lastSummonedFamily,
                FamilyStreak = familyStreak, NextMemberNumber = nextMemberNumber,
                Roster = Roster.Capture(), DeployedIds = deployedIds.ToArray(), UndoHistory = snapshots.ToArray() };
        }

        public static ExpeditionRun Restore(CatalogDatabase catalog, ExpeditionRunSave save)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            string lastFamily = save == null || string.IsNullOrEmpty(save.LastSummonedFamily) ?
                null : save.LastSummonedFamily;
            if (save == null || save.Version != 1 || save.Wave < 1 || save.Wave > TotalWaves ||
                save.Lantern < 0 || save.Lantern > 3 || save.SummonsRemaining < 0 || save.SummonsRemaining > 2 ||
                save.Victories < 0 || save.Victories > save.Wave || save.NextMemberNumber < 3 ||
                save.NextMemberNumber > SupplyLimit || save.RandomState == 0 ||
                save.FamilyStreak < 0 || save.FamilyStreak > SupplyLimit ||
                (save.FamilyStreak == 0) != (lastFamily == null) ||
                lastFamily != null && Array.IndexOf(Families, lastFamily) < 0 ||
                save.DeployedIds == null || save.UndoHistory == null ||
                !Enum.IsDefined(typeof(ExpeditionPhase), save.Phase))
                throw new ArgumentException("Invalid expedition save.");
            var run = new ExpeditionRun(catalog);
            run.Roster = CatalogRoster.Restore(catalog, save.Roster);
            run.Wave = save.Wave; run.Lantern = save.Lantern;
            run.SummonsRemaining = save.SummonsRemaining; run.Victories = save.Victories;
            run.LastVictory = save.LastVictory; run.Phase = save.Phase;
            run.randomState = save.RandomState; run.lastSummonedFamily = lastFamily;
            run.familyStreak = save.FamilyStreak; run.nextMemberNumber = save.NextMemberNumber;
            run.deployedIds.AddRange(save.DeployedIds);
            run.ValidateDeployment();
            if (run.Roster.SupplyUsed != run.nextMemberNumber)
                throw new ArgumentException("Saved supply and member IDs disagree.");
            if (save.Phase != ExpeditionPhase.Preparation && save.UndoHistory.Length != 0 ||
                save.Phase == ExpeditionPhase.Battle && save.SummonsRemaining != 0)
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
