using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public enum CatalogTeam { Ally, Enemy }
    public enum CatalogEnemyRole { None, Beast, Raider, Golem, MineWorker, Supervisor, Furnace,
        FrostWarden, GardenSpitter, GardenLord, CrownDrake }
    public enum CrownDrakePhase { Grounded, BreathWarning, GroundWindow, Rising, AirWarning, LandingWarning }
    public enum CatalogAttackPhase { Ready, WindingBasic, WindingSkill }
    public enum CatalogEventKind { SkillUsed, BasicHit, Damage, Shield, ShieldBlocked, Heal, Burn, Slow, Mark,
        Buff, TraitTriggered, Defeated, Ended, ChargeTelegraph, ChargeHit,
        BossTelegraph, BossImpact, BossEnraged, Freeze, Poison }

    public sealed class CatalogBattleEvent
    {
        public CatalogEventKind Kind { get; internal set; }
        public string SourceId { get; internal set; }
        public string TargetId { get; internal set; }
        public string AbilityId { get; internal set; }
        public float Amount { get; internal set; }
        public float Time { get; internal set; }
    }

    internal enum CatalogBuffKind { Power, Haste, DamageReduction }
    internal sealed class CatalogBuff
    {
        public CatalogBuffKind Kind;
        public float Amount, Until;
    }

    public sealed class CatalogBattleUnit
    {
        public string Id { get; internal set; }
        public CatalogTeam Team { get; internal set; }
        public string FormId { get; internal set; }
        public string BranchId { get; internal set; }
        public string InheritedTraitId { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Hp { get; internal set; }
        public float MaxHp { get; internal set; }
        public float Shield { get; internal set; }
        public float Power { get; internal set; }
        public float Defense { get; internal set; }
        public float AttackInterval { get; internal set; }
        // Basic attacks can be tuned independently of boss skill power.
        internal float BasicPowerScale = 1f;
        public float Range { get; internal set; }
        public float MoveSpeed { get; internal set; }
        public bool DrakeRangedBasic { get; internal set; }
        public bool IsBoss { get; internal set; }
        public bool BossPatternsEnabled { get; internal set; }
        public bool BossEnraged { get; internal set; }
        public CrownDrakePhase DrakePhase { get; internal set; }
        public float FlightLift { get { return !IsAlive || Clock.Ended ? 0f :
            DrakePhase == CrownDrakePhase.Rising ? Math.Min(1f, Math.Max(0f, (Clock.Time - DrakePhaseStartAt) / .7f)) :
            DrakePhase == CrownDrakePhase.AirWarning ? 1f :
            DrakePhase == CrownDrakePhase.LandingWarning ?
                Math.Min(1f, Math.Max(0f, (ReleaseAt - Clock.Time) / (ReleaseAt - DrakePhaseStartAt))) : 0f; } }
        public bool IsAirborne { get { return FlightLift > 0f; } }
        public CatalogEnemyRole EnemyRole { get; internal set; }
        public bool IsAlive { get { return Hp > 0f; } }
        public CatalogAttackPhase AttackPhase { get; internal set; }
        public string AttackTargetId { get { return PendingTargetId; } }
        public float AttackWindupRemaining { get { return AttackPhase == CatalogAttackPhase.Ready ? 0f : Math.Max(0f, ReleaseAt - Clock.Time); } }
        public float ChargeWarningRemaining { get { return PendingSkillId == "BEAST_CHARGE" &&
            AttackPhase == CatalogAttackPhase.WindingSkill && !ChargeMoving ? AttackWindupRemaining : 0f; } }
        public bool IsCharging { get { return IsAlive && ChargeMoving; } }
        public float ChargeOriginX { get { return ChargeStartX; } }
        public float ChargeOriginY { get { return ChargeStartY; } }
        public float ChargeTargetX { get { return ChargeAimX; } }
        public float ChargeTargetY { get { return ChargeAimY; } }
        public float ChargeImpactX { get { return ChargeWarningRemaining > 0f ? PredictedChargeEnd(true) : ChargeEndX; } }
        public float ChargeImpactY { get { return ChargeWarningRemaining > 0f ? PredictedChargeEnd(false) : ChargeEndY; } }
        public float ChargeWidth { get { return BossPatternsEnabled && EnemyRole == CatalogEnemyRole.Beast ? .7f : .95f; } }
        public float BreathWarningRemaining { get { return IsAlive && PendingSkillId == "DRACONIC_BREATH" &&
            AttackPhase == CatalogAttackPhase.WindingSkill ? AttackWindupRemaining : 0f; } }
        public float BreathOriginX { get { return DrakeBreathOriginX; } }
        public float BreathOriginY { get { return DrakeBreathOriginY; } }
        public float BreathTargetX { get { return DrakeBreathEndX; } }
        public float BreathTargetY { get { return DrakeBreathEndY; } }
        public float BreathLength { get { return 5.5f; } }
        public float BreathHalfWidth { get { return .65f; } }
        public float SlamWarningRemaining { get { return IsAlive &&
            (PendingSkillId == "GOLEM_SLAM" || PendingSkillId == "GOLEM_AFTERSHOCK" ||
             PendingSkillId == "FURNACE_BLAST" || PendingSkillId == "FURNACE_EMBER" ||
             PendingSkillId == "FROST_NOVA" || PendingSkillId == "GARDEN_BRIARS" ||
             PendingSkillId == "DRACONIC_BOMB" || PendingSkillId == "DRACONIC_LAND") &&
            AttackPhase == CatalogAttackPhase.WindingSkill ? AttackWindupRemaining : 0f; } }
        public float SlamTargetX { get { return SlamAimX; } }
        public float SlamTargetY { get { return SlamAimY; } }
        public float SlamRadius { get { return SlamRadiusValue; } }
        public bool SlamIsAftershock { get { return PendingSkillId == "GOLEM_AFTERSHOCK" ||
            PendingSkillId == "FURNACE_EMBER"; } }
        public float BurnRemaining { get { return BurnTicksLeft > 0 ? Math.Max(0f, BurnUntil - Clock.Time) : 0f; } }
        public float PoisonRemaining { get { return IsAlive && !Clock.Ended ?
            Math.Max(0f, PoisonUntil - Clock.Time) : 0f; } }
        public float FrozenRemaining { get { return IsAlive && !Clock.Ended ?
            Math.Max(0f, FrozenUntil - Clock.Time) : 0f; } }
        public float SlowPercent { get { return SlowUntil > Clock.Time ? Slow : 0f; } }
        public float MarkRemaining { get { return Math.Max(0f, MarkUntil - Clock.Time); } }

        internal CatalogBattle Clock;
        internal string InnateTraitId, PendingSkillId, PendingTargetId, VolleyTargetId, BurnSourceId,
            PoisonSourceId, ShieldSourceId;
        internal float ShieldUntil, BurnUntil, BurnNextAt, BurnDamage, PoisonUntil, PoisonNextAt,
            PoisonDamage, Slow, SlowUntil, MarkUntil, FrozenUntil;
        internal int BurnTicksLeft, PoisonTicksLeft, VolleyShotsLeft, BasicHitsLanded;
        internal float NextBasicAt, ReleaseAt, NextPrimaryAt = 2f, NextFinalAt = 2f, NextCounterAt,
            NextHarmonyAt, NextSpoilsAt, NextVolleyAt, ExtraBasicUntil, ExtraBasicMultiplier;
        internal float NextChargeAt, ChargeAimX, ChargeAimY, ChargeStartX, ChargeStartY,
            ChargeEndX, ChargeEndY, ChargeStartAt;
        internal bool ChargeMoving;
        internal float NextSlamAt, SlamAimX, SlamAimY, SlamRadiusValue;
        internal bool FrostFollowup;
        public bool IsFrostFollowup { get { return FrostFollowup && SlamWarningRemaining > 0; } }
        internal float GardenNextWarningAt;
        internal int GardenStrikeCount, GardenStrikeIndex;
        internal float[] GardenAimXs, GardenAimYs;
        internal string[] GardenTargetIds;
        internal float NextGuardAt;
        internal float DrakePhaseStartAt, DrakeBreathOriginX, DrakeBreathOriginY,
            DrakeBreathEndX, DrakeBreathEndY, DrakeBombX, DrakeBombY;
        internal string DrakeBombTargetId;
        internal bool FirstHitUsed, CrisisUsed;
        internal readonly List<CatalogBuff> Buffs = new List<CatalogBuff>();
        internal float ZoneX, ZoneY, ZoneNextAt;
        internal int ZoneTicksLeft;

        internal float PredictedChargeEnd(bool xAxis)
        {
            float dx = ChargeAimX - ChargeStartX, dy = ChargeAimY - ChargeStartY;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distance < .0001f) return xAxis ? ChargeStartX : ChargeStartY;
            float maximum = BossPatternsEnabled && EnemyRole == CatalogEnemyRole.Beast ? 3.6f : 2.65f;
            float travel = Math.Min(Math.Max(0f, distance - .4f), maximum * (1f - SlowPercent));
            return (xAxis ? ChargeStartX + dx / distance * travel : ChargeStartY + dy / distance * travel);
        }
    }

    public sealed partial class CatalogBattle
    {
        const float Tick = 1f / 60f;
        readonly CatalogDatabase database;
        readonly HashSet<string> tactics = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> legacyResearch = new HashSet<string>(StringComparer.Ordinal);
        readonly IReadOnlyList<string> publicTactics, publicLegacyResearch;
        readonly List<CatalogBattleUnit> units = new List<CatalogBattleUnit>();
        readonly Dictionary<string, CatalogBattleUnit> byId = new Dictionary<string, CatalogBattleUnit>(StringComparer.Ordinal);
        float accumulator;
        readonly int balanceRules;
        public event Action<CatalogBattleEvent> Event;
        public IReadOnlyList<CatalogBattleUnit> Units { get { return units.AsReadOnly(); } }
        public IReadOnlyList<string> TacticIds { get { return publicTactics; } }
        public IReadOnlyList<string> LegacyResearchIds { get { return publicLegacyResearch; } }
        public float Time { get; private set; }
        public bool Ended { get; private set; }
        public CatalogTeam? Winner { get; private set; }

        public CatalogBattle(CatalogDatabase database) : this(database, null) { }

        public CatalogBattle(CatalogDatabase database, IEnumerable<string> tacticIds,
            IEnumerable<string> legacyResearchIds = null, int balanceRules = 1)
        {
            if (database == null) throw new ArgumentNullException("database");
            if (balanceRules < 0 || balanceRules > CatalogBalance.CurrentRules)
                throw new ArgumentOutOfRangeException("balanceRules");
            this.balanceRules = balanceRules;
            this.database = database;
            var selected = new List<string>(tacticIds ?? new string[0]);
            var permanent = new List<string>(legacyResearchIds ?? new string[0]);
            CatalogResearch.ValidateSelection(selected, int.MaxValue);
            CatalogLegacyResearch.ValidateSelection(permanent);
            foreach (string id in selected) tactics.Add(id);
            foreach (string id in permanent) legacyResearch.Add(id);
            publicTactics = Array.AsReadOnly(selected.ToArray());
            publicLegacyResearch = Array.AsReadOnly(permanent.ToArray());
        }

        public CatalogBattleUnit AddUnit(string id, CatalogTeam team, string formId, string branchId,
            string inheritedTraitId, float x, float y)
        {
            CatalogForm form = database.Get(formId);
            if (formId.StartsWith("F-", StringComparison.Ordinal))
            {
                if (branchId == null || !branchId.StartsWith("E-" + CatalogRoster.FamilyCode(formId) + "-", StringComparison.Ordinal))
                    throw new ArgumentException("Final form must retain its chosen branch skill.");
                database.Get(branchId);
            }
            else if (branchId != null && branchId != formId)
                throw new ArgumentException("Only final forms accept a separate branch ID.");
            float hp = form.Hp;
            if (team == CatalogTeam.Ally)
            {
                if (form.Range <= 1.5f && tactics.Contains("bulwark")) hp *= 1.12f;
                if (legacyResearch.Contains("tactical-vigor")) hp *= 1.05f;
            }
            return Add(id, team, formId, branchId, inheritedTraitId, x, y,
                hp, form.Power, form.Defense, form.AttackInterval, form.Range, form.MoveSpeed,
                form.InnateTrait, false);
        }

        public CatalogBattleUnit AddEnemy(string id, float hp, float power, float defense,
            float attackInterval, float range, float moveSpeed, bool boss, float x, float y)
        {
            return AddEnemy(id, hp, power, defense, attackInterval, range, moveSpeed, boss, x, y,
                boss ? CatalogEnemyRole.Golem : CatalogEnemyRole.None);
        }

        public CatalogBattleUnit AddEnemy(string id, float hp, float power, float defense,
            float attackInterval, float range, float moveSpeed, bool boss, float x, float y,
            CatalogEnemyRole role, bool bossPatterns = false)
        {
            if (hp <= 0 || power <= 0 || defense < 0 || attackInterval <= .25f || range <= 0 || moveSpeed <= 0)
                throw new ArgumentException("Invalid enemy stats.");
            if (!Enum.IsDefined(typeof(CatalogEnemyRole), role) ||
                (role == CatalogEnemyRole.Golem || role == CatalogEnemyRole.Furnace ||
                 role == CatalogEnemyRole.FrostWarden || role == CatalogEnemyRole.GardenLord ||
                 role == CatalogEnemyRole.CrownDrake) && !boss ||
                boss && role != CatalogEnemyRole.Golem && role != CatalogEnemyRole.Beast &&
                    role != CatalogEnemyRole.Furnace && role != CatalogEnemyRole.FrostWarden &&
                    role != CatalogEnemyRole.GardenLord && role != CatalogEnemyRole.CrownDrake ||
                bossPatterns && !boss)
                throw new ArgumentException("Invalid enemy role.");
            CatalogBattleUnit enemy = Add(id, CatalogTeam.Enemy, boss ? "EN-BOSS" : "EN-MOB", null, null, x, y,
                hp, power, defense, attackInterval, range, moveSpeed, null, boss);
            enemy.EnemyRole = role;
            enemy.BossPatternsEnabled = bossPatterns;
            // A large dragon fights from outside the party instead of entering
            // melee range. Only the basic firebolt is scaled; skill power and
            // cadence stay intact. Older expeditions retain their saved rules.
            if (balanceRules >= 4 && bossPatterns && role == CatalogEnemyRole.CrownDrake)
            {
                enemy.Range = Math.Max(range, 3.2f); enemy.DrakeRangedBasic = true;
                enemy.BasicPowerScale = CatalogBalance.DrakeBasicPower(balanceRules);
            }
            enemy.NextChargeAt = role == CatalogEnemyRole.Beast ?
                bossPatterns ? 7f : 1.35f : float.PositiveInfinity;
            enemy.NextSlamAt = bossPatterns && role == CatalogEnemyRole.Golem ? 2f : float.PositiveInfinity;
            if (bossPatterns && role == CatalogEnemyRole.Furnace) enemy.NextSlamAt = 2f;
            if (bossPatterns && role == CatalogEnemyRole.FrostWarden) enemy.NextSlamAt = 2f;
            if (bossPatterns && role == CatalogEnemyRole.GardenLord) enemy.NextSlamAt = 2f;
            if (bossPatterns && role == CatalogEnemyRole.CrownDrake) enemy.NextSlamAt = 2f;
            if (role == CatalogEnemyRole.GardenLord)
            {
                enemy.GardenAimXs = new float[3]; enemy.GardenAimYs = new float[3];
                enemy.GardenTargetIds = new string[3];
            }
            enemy.NextGuardAt = role == CatalogEnemyRole.Supervisor ? 1.5f : float.PositiveInfinity;
            return enemy;
        }

        CatalogBattleUnit Add(string id, CatalogTeam team, string formId, string branchId, string inheritedTraitId,
            float x, float y, float hp, float power, float defense, float interval, float range, float moveSpeed,
            string innateTraitId, bool boss)
        {
            if (Ended || string.IsNullOrEmpty(id) || byId.ContainsKey(id)) throw new ArgumentException("Duplicate or invalid actor ID.");
            var unit = new CatalogBattleUnit
            {
                Id = id, Team = team, FormId = formId, BranchId = branchId,
                InheritedTraitId = inheritedTraitId, InnateTraitId = innateTraitId,
                X = x, Y = y, Hp = hp, MaxHp = hp, Power = power, Defense = defense,
                AttackInterval = interval, Range = range, MoveSpeed = moveSpeed,
                IsBoss = boss, Clock = this, NextBasicAt = .25f
            };
            units.Add(unit); byId.Add(id, unit); spacingOrderDirty = true;
            if (HasTrait(unit, "T-PR")) GiveShield(unit, unit, unit.MaxHp * .1f, 4f, "T-PR");
            return unit;
        }

        public CatalogBattleUnit Get(string id)
        {
            CatalogBattleUnit unit;
            return id != null && byId.TryGetValue(id, out unit) ? unit : null;
        }

        public void Step(float dt)
        {
            if (dt < 0 || float.IsNaN(dt) || float.IsInfinity(dt)) throw new ArgumentOutOfRangeException("dt");
            if (Ended) return;
            accumulator += dt;
            int steps = 0;
            while (accumulator + .000001f >= Tick && steps++ < 36000 && !Ended)
            {
                accumulator -= Tick;
                Time += Tick;
                TickOnce();
            }
        }

        void TickOnce()
        {
            foreach (CatalogBattleUnit unit in units)
            {
                if (!unit.IsAlive) continue;
                if (unit.Shield > 0 && unit.ShieldUntil <= Time) unit.Shield = 0;
                if (unit.SlowUntil <= Time) unit.Slow = 0;
                for (int b = unit.Buffs.Count - 1; b >= 0; b--)
                    if (unit.Buffs[b].Until <= Time) unit.Buffs.RemoveAt(b);
                if (unit.BurnTicksLeft > 0 && unit.BurnNextAt <= Time)
                {
                    CatalogBattleUnit source = Get(unit.BurnSourceId);
                    Damage(source, unit, unit.BurnDamage, "BURN", false, true);
                    if (unit.IsAlive)
                    {
                        unit.BurnTicksLeft--;
                        unit.BurnNextAt += 1f;
                        if (unit.BurnTicksLeft == 0) unit.BurnUntil = Time;
                    }
                }
                if (unit.PoisonTicksLeft > 0 && unit.PoisonNextAt <= Time)
                {
                    CatalogBattleUnit source = Get(unit.PoisonSourceId);
                    Damage(source, unit, unit.PoisonDamage, "POISON", false, true);
                    if (unit.IsAlive)
                    {
                        unit.PoisonTicksLeft--;
                        unit.PoisonNextAt += 1f;
                    }
                }
            }
            ResolveBodySpacing();
            foreach (CatalogBattleUnit unit in units)
            {
                if (!unit.IsAlive) continue;
                if (unit.FrozenUntil > Time) continue;
                TickZone(unit);
                TickVolley(unit);
                TryGuard(unit);
                if (unit.ChargeMoving)
                {
                    AdvanceCharge(unit);
                    if (Time >= unit.ReleaseAt) FinishCharge(unit);
                    continue;
                }
                if (unit.AttackPhase != CatalogAttackPhase.Ready && Time >= unit.ReleaseAt)
                {
                    if (unit.AttackPhase == CatalogAttackPhase.WindingBasic) ReleaseBasic(unit);
                    else if (unit.PendingSkillId == "BEAST_CHARGE")
                    {
                        StartChargeMovement(unit);
                        continue;
                    }
                    else if (unit.PendingSkillId == "GOLEM_SLAM" || unit.PendingSkillId == "GOLEM_AFTERSHOCK" ||
                        unit.PendingSkillId == "FURNACE_BLAST" || unit.PendingSkillId == "FURNACE_EMBER" ||
                        unit.PendingSkillId == "FROST_NOVA" || unit.PendingSkillId == "GARDEN_BRIARS")
                    {
                        ReleaseSlam(unit);
                        continue;
                    }
                    else if (unit.PendingSkillId == "DRACONIC_BREATH" ||
                        unit.PendingSkillId == "DRACONIC_BOMB" || unit.PendingSkillId == "DRACONIC_LAND")
                    {
                        ReleaseDrake(unit);
                        continue;
                    }
                    else ReleaseSkill(unit);
                    unit.AttackPhase = CatalogAttackPhase.Ready;
                }
                if (unit.VolleyShotsLeft > 0) continue; // One tracking cast finishes before basic/final actions resume.
                if (!unit.IsAlive || unit.AttackPhase != CatalogAttackPhase.Ready) continue;
                if (unit.GardenStrikeCount > 0 && Time < unit.GardenNextWarningAt) continue;
                if (TryAdvanceDrake(unit)) continue;
                if (TryStartSlam(unit)) continue;
                if (TryStartCharge(unit)) continue;
                if (TryStartSkill(unit)) continue;
                CatalogBattleUnit target = ChooseEnemy(unit, false, false);
                if (unit.EnemyRole == CatalogEnemyRole.Raider ||
                    unit.EnemyRole == CatalogEnemyRole.GardenSpitter)
                {
                    CatalogBattleUnit vulnerable = ChooseEnemy(unit, true, false);
                    if (vulnerable != null) target = vulnerable;
                }
                if (target == null) continue;
                if (!InBasicContact(unit, target))
                {
                    MoveToward(unit, target);
                    continue;
                }
                if (Time + .25f + .0001f >= unit.NextBasicAt)
                {
                    unit.PendingTargetId = target.Id;
                    unit.AttackPhase = CatalogAttackPhase.WindingBasic;
                    unit.ReleaseAt = Math.Max(Time + .25f, unit.NextBasicAt);
                }
            }
            ResolveBodySpacing();
            CheckEnded();
        }

        bool TryStartCharge(CatalogBattleUnit actor)
        {
            if (actor.EnemyRole != CatalogEnemyRole.Beast || Time < actor.NextChargeAt) return false;
            CatalogBattleUnit target = ChooseEnemy(actor, false, false);
            if (target == null || Distance(actor, target) > 4.6f) return false;
            actor.PendingTargetId = target.Id;
            actor.PendingSkillId = "BEAST_CHARGE";
            actor.ChargeStartX = actor.X;
            actor.ChargeStartY = actor.Y;
            actor.ChargeAimX = target.X;
            actor.ChargeAimY = target.Y;
            actor.AttackPhase = CatalogAttackPhase.WindingSkill;
            float warning = actor.BossPatternsEnabled ? .9f : .6f;
            actor.ReleaseAt = Time + warning;
            Emit(CatalogEventKind.ChargeTelegraph, actor, target,
                actor.BossPatternsEnabled ? "BOSS_CHARGE" : "BEAST_CHARGE", warning);
            return true;
        }

        void StartChargeMovement(CatalogBattleUnit actor)
        {
            actor.ChargeStartX = actor.X;
            actor.ChargeStartY = actor.Y;
            actor.ChargeEndX = actor.PredictedChargeEnd(true);
            actor.ChargeEndY = actor.PredictedChargeEnd(false);
            actor.ChargeMoving = true;
            actor.ChargeStartAt = Time;
            actor.ReleaseAt = Time + .2f;
        }

        void AdvanceCharge(CatalogBattleUnit actor)
        {
            float fraction = Math.Min(1f, (Time - actor.ChargeStartAt) / .2f);
            actor.X = actor.ChargeStartX + (actor.ChargeEndX - actor.ChargeStartX) * fraction;
            actor.Y = actor.ChargeStartY + (actor.ChargeEndY - actor.ChargeStartY) * fraction;
        }

        void FinishCharge(CatalogBattleUnit actor)
        {
            CatalogBattleUnit target = Get(actor.PendingTargetId);
            actor.ChargeMoving = false;
            actor.AttackPhase = CatalogAttackPhase.Ready;
            actor.NextChargeAt = Time + (actor.BossPatternsEnabled ?
                actor.BossEnraged ? 5.5f : 7f : 6f);
            actor.NextBasicAt = Math.Max(actor.NextBasicAt, Time + .55f);
            actor.PendingSkillId = null;
            if (actor.BossPatternsEnabled)
            {
                Emit(CatalogEventKind.BossImpact, actor, null, "BOSS_CHARGE", actor.ChargeWidth);
                foreach (CatalogBattleUnit candidate in units)
                {
                    if (!actor.IsAlive || !candidate.IsAlive || candidate.Team == actor.Team ||
                        DistanceToSegment(candidate.X, candidate.Y, actor.ChargeStartX, actor.ChargeStartY,
                            actor.ChargeEndX, actor.ChargeEndY) > actor.ChargeWidth) continue;
                    Damage(actor, candidate, EffectivePower(actor) * 1.6f, "BOSS_CHARGE", false, false);
                }
            }
            else if (target != null && target.IsAlive && Distance(actor, target) <= .95f)
            {
                Emit(CatalogEventKind.ChargeHit, actor, target, "BEAST_CHARGE", 1f);
                Damage(actor, target, EffectivePower(actor) * 1.35f, "BEAST_CHARGE", false, false);
            }
        }

        static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float lengthSquared = dx * dx + dy * dy;
            float fraction = lengthSquared < .000001f ? 0f :
                Math.Max(0f, Math.Min(1f, ((px - ax) * dx + (py - ay) * dy) / lengthSquared));
            float sideX = px - (ax + dx * fraction), sideY = py - (ay + dy * fraction);
            return (float)Math.Sqrt(sideX * sideX + sideY * sideY);
        }

        void TryGuard(CatalogBattleUnit actor)
        {
            if (actor.EnemyRole != CatalogEnemyRole.Supervisor || Time < actor.NextGuardAt) return;
            actor.NextGuardAt = Time + 7f;
            CatalogBattleUnit first = null, second = null;
            foreach (CatalogBattleUnit candidate in units)
            {
                if (candidate == actor || !candidate.IsAlive || candidate.Team != actor.Team ||
                    Distance(actor, candidate) > 2.4f) continue;
                float value = Math.Min(180f, candidate.MaxHp * .12f);
                if (candidate.Shield >= value - .00001f) continue;
                if (first == null || GuardPriority(candidate, first) < 0)
                { second = first; first = candidate; }
                else if (second == null || GuardPriority(candidate, second) < 0) second = candidate;
            }
            if (first != null) GiveShield(actor, first,
                Math.Min(180f, first.MaxHp * .12f), 4f, "MINE_GUARD");
            if (second != null) GiveShield(actor, second,
                Math.Min(180f, second.MaxHp * .12f), 4f, "MINE_GUARD");
            // A lone supervisor still displays the mechanic without making its shield cumulative.
            if (first == null) GiveShield(actor, actor,
                Math.Min(180f, actor.MaxHp * .12f), 4f, "MINE_GUARD");
        }

        static int GuardPriority(CatalogBattleUnit left, CatalogBattleUnit right)
        {
            if (left.IsBoss != right.IsBoss) return left.IsBoss ? -1 : 1;
            int health = (left.Hp / left.MaxHp).CompareTo(right.Hp / right.MaxHp);
            return health != 0 ? health : string.CompareOrdinal(left.Id, right.Id);
        }

        bool TryAdvanceDrake(CatalogBattleUnit actor)
        {
            if (!actor.BossPatternsEnabled || actor.EnemyRole != CatalogEnemyRole.CrownDrake)
                return false;
            if (actor.DrakePhase == CrownDrakePhase.Rising)
            {
                if (Time < actor.NextSlamAt) return true;
                actor.DrakePhase = CrownDrakePhase.AirWarning;
                actor.PendingTargetId = actor.DrakeBombTargetId;
                actor.PendingSkillId = "DRACONIC_BOMB";
                actor.SlamAimX = actor.DrakeBombX; actor.SlamAimY = actor.DrakeBombY;
                actor.SlamRadiusValue = 1.2f;
                actor.AttackPhase = CatalogAttackPhase.WindingSkill;
                actor.ReleaseAt = Time + (actor.BossEnraged ? .9f : 1.1f);
                Emit(CatalogEventKind.BossTelegraph, actor, Get(actor.PendingTargetId),
                    "DRACONIC_BOMB", actor.SlamRadiusValue);
                return true;
            }
            if (Time < actor.NextSlamAt) return false;
            CatalogBattleUnit target = null;
            float chosenDistance = actor.DrakePhase == CrownDrakePhase.GroundWindow ? -1f : float.PositiveInfinity;
            foreach (CatalogBattleUnit candidate in units)
            {
                if (!candidate.IsAlive || candidate.Team == actor.Team) continue;
                float distance = Distance(actor, candidate);
                bool better = actor.DrakePhase == CrownDrakePhase.GroundWindow ?
                    distance > chosenDistance + .00001f : distance < chosenDistance - .00001f;
                if (better || Math.Abs(distance - chosenDistance) <= .00001f && target != null &&
                    string.CompareOrdinal(candidate.Id, target.Id) < 0)
                { target = candidate; chosenDistance = distance; }
            }
            if (target == null) return false;
            if (actor.DrakePhase == CrownDrakePhase.GroundWindow)
            {
                // Core position stays grounded and targetable; FlightLift is presentation only.
                actor.DrakePhase = CrownDrakePhase.Rising;
                actor.DrakePhaseStartAt = Time;
                actor.NextSlamAt = Time + .7f;
                actor.DrakeBombX = target.X; actor.DrakeBombY = target.Y;
                actor.DrakeBombTargetId = target.Id;
                return true;
            }
            actor.DrakePhase = CrownDrakePhase.BreathWarning;
            actor.PendingTargetId = target.Id;
            actor.PendingSkillId = "DRACONIC_BREATH";
            actor.DrakeBreathOriginX = actor.X; actor.DrakeBreathOriginY = actor.Y;
            float dx = target.X - actor.X, dy = target.Y - actor.Y;
            float distanceToTarget = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distanceToTarget < .0001f) { dx = -1f; dy = 0f; distanceToTarget = 1f; }
            actor.DrakeBreathEndX = actor.X + dx / distanceToTarget * actor.BreathLength;
            actor.DrakeBreathEndY = actor.Y + dy / distanceToTarget * actor.BreathLength;
            actor.AttackPhase = CatalogAttackPhase.WindingSkill;
            actor.ReleaseAt = Time + (actor.BossEnraged ? .9f : 1.1f);
            Emit(CatalogEventKind.BossTelegraph, actor, target, "DRACONIC_BREATH", actor.BreathHalfWidth);
            return true;
        }

        void ReleaseDrake(CatalogBattleUnit actor)
        {
            string ability = actor.PendingSkillId;
            Emit(CatalogEventKind.BossImpact, actor, null, ability,
                ability == "DRACONIC_BREATH" ? actor.BreathHalfWidth : actor.SlamRadiusValue);
            foreach (CatalogBattleUnit candidate in units)
            {
                if (!actor.IsAlive || !candidate.IsAlive || candidate.Team == actor.Team) continue;
                bool hit = ability == "DRACONIC_BREATH" ?
                    DistanceToSegment(candidate.X, candidate.Y, actor.DrakeBreathOriginX,
                        actor.DrakeBreathOriginY, actor.DrakeBreathEndX, actor.DrakeBreathEndY) <= actor.BreathHalfWidth :
                    DistanceToPoint(candidate.X, candidate.Y, actor.SlamAimX, actor.SlamAimY) <= actor.SlamRadiusValue;
                if (!hit) continue;
                float coefficient = ability == "DRACONIC_BREATH" ? 1.15f :
                    ability == "DRACONIC_BOMB" ? 1.25f : 1.15f;
                Damage(actor, candidate, EffectivePower(actor) * coefficient, ability, false, false);
                if (candidate.IsAlive && ability == "DRACONIC_BREATH") Burn(actor, candidate, 8f);
            }
            actor.NextBasicAt = Math.Max(actor.NextBasicAt, Time + .45f);
            if (!actor.IsAlive) return;
            if (ability == "DRACONIC_BREATH")
            {
                actor.DrakePhase = CrownDrakePhase.GroundWindow;
                actor.NextSlamAt = Time + 4f;
            }
            else if (ability == "DRACONIC_BOMB")
            {
                actor.DrakePhase = CrownDrakePhase.LandingWarning;
                actor.DrakePhaseStartAt = Time;
                actor.PendingSkillId = "DRACONIC_LAND";
                actor.SlamAimX = actor.X; actor.SlamAimY = actor.Y;
                actor.SlamRadiusValue = 1.55f;
                actor.ReleaseAt = Time + (actor.BossEnraged ? .7f : .9f);
                Emit(CatalogEventKind.BossTelegraph, actor, null, "DRACONIC_LAND", actor.SlamRadiusValue);
                return;
            }
            else
            {
                actor.DrakePhase = CrownDrakePhase.Grounded;
                actor.NextSlamAt = Time + (actor.BossEnraged ? 4f : 6f);
            }
            actor.PendingSkillId = null;
            actor.AttackPhase = CatalogAttackPhase.Ready;
        }

        static float DistanceToPoint(float x, float y, float targetX, float targetY)
        {
            float dx = x - targetX, dy = y - targetY;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        bool TryStartSlam(CatalogBattleUnit actor)
        {
            if (actor.BossPatternsEnabled && actor.EnemyRole == CatalogEnemyRole.GardenLord)
            {
                if (actor.GardenStrikeCount == 0)
                {
                    if (Time < actor.NextSlamAt) return false;
                    for (int i = 0; i < actor.GardenTargetIds.Length; i++)
                    {
                        CatalogBattleUnit farthest = null;
                        float farthestDistance = -1f;
                        foreach (CatalogBattleUnit candidate in units)
                        {
                            if (!candidate.IsAlive || candidate.Team == actor.Team) continue;
                            bool alreadySelected = false;
                            for (int prior = 0; prior < i; prior++)
                                if (actor.GardenTargetIds[prior] == candidate.Id) alreadySelected = true;
                            if (alreadySelected) continue;
                            float distance = Distance(actor, candidate);
                            if (distance > farthestDistance + .00001f ||
                                Math.Abs(distance - farthestDistance) <= .00001f && farthest != null &&
                                string.CompareOrdinal(candidate.Id, farthest.Id) < 0)
                            { farthest = candidate; farthestDistance = distance; }
                        }
                        if (farthest == null) break;
                        actor.GardenTargetIds[i] = farthest.Id;
                        actor.GardenAimXs[i] = farthest.X;
                        actor.GardenAimYs[i] = farthest.Y;
                        actor.GardenStrikeCount++;
                    }
                    if (actor.GardenStrikeCount == 0) return false;
                    actor.GardenStrikeIndex = 0;
                }
                else if (Time < actor.GardenNextWarningAt) return true;
                int strike = actor.GardenStrikeIndex;
                actor.PendingTargetId = actor.GardenTargetIds[strike];
                actor.PendingSkillId = "GARDEN_BRIARS";
                actor.SlamAimX = actor.GardenAimXs[strike];
                actor.SlamAimY = actor.GardenAimYs[strike];
                actor.SlamRadiusValue = 1.25f;
                actor.AttackPhase = CatalogAttackPhase.WindingSkill;
                actor.GardenNextWarningAt = 0f;
                actor.ReleaseAt = Time + (actor.BossEnraged ? .8f : 1.05f);
                Emit(CatalogEventKind.BossTelegraph, actor, Get(actor.PendingTargetId),
                    "GARDEN_BRIARS", 1.25f);
                return true;
            }
            if (actor.BossPatternsEnabled && actor.EnemyRole == CatalogEnemyRole.FrostWarden &&
                Time >= actor.NextSlamAt)
            {
                CatalogBattleUnit farthest = null;
                float farthestDistance = -1f;
                foreach (CatalogBattleUnit candidate in units)
                {
                    if (!candidate.IsAlive || candidate.Team == actor.Team) continue;
                    float distance = Distance(actor, candidate);
                    if (distance > farthestDistance)
                    { farthest = candidate; farthestDistance = distance; }
                }
                if (farthest == null) return false;
                actor.PendingTargetId = farthest.Id;
                actor.PendingSkillId = "FROST_NOVA";
                actor.SlamAimX = farthest.X;
                actor.SlamAimY = farthest.Y;
                actor.SlamRadiusValue = 1.1f;
                actor.AttackPhase = CatalogAttackPhase.WindingSkill;
                actor.ReleaseAt = Time + (actor.BossEnraged ? .8f : 1.1f);
                Emit(CatalogEventKind.BossTelegraph, actor, farthest, "FROST_NOVA", 1.1f);
                return true;
            }
            if (!actor.BossPatternsEnabled ||
                actor.EnemyRole != CatalogEnemyRole.Golem && actor.EnemyRole != CatalogEnemyRole.Furnace ||
                Time < actor.NextSlamAt) return false;
            CatalogBattleUnit target = ChooseEnemy(actor, false, false);
            if (target == null || Distance(actor, target) > 4.3f) return false;
            bool furnace = actor.EnemyRole == CatalogEnemyRole.Furnace;
            actor.PendingTargetId = target.Id;
            actor.PendingSkillId = furnace ? "FURNACE_BLAST" : "GOLEM_SLAM";
            actor.SlamAimX = target.X;
            actor.SlamAimY = target.Y;
            actor.SlamRadiusValue = furnace ? 1.25f : 1.35f;
            actor.AttackPhase = CatalogAttackPhase.WindingSkill;
            actor.ReleaseAt = Time + (furnace ? 1f : 1.1f);
            Emit(CatalogEventKind.BossTelegraph, actor, target, actor.PendingSkillId, actor.SlamRadiusValue);
            return true;
        }

        void ReleaseSlam(CatalogBattleUnit actor)
        {
            if (actor.PendingSkillId == "GARDEN_BRIARS")
            {
                Emit(CatalogEventKind.BossImpact, actor, null, "GARDEN_BRIARS", actor.SlamRadiusValue);
                foreach (CatalogBattleUnit candidate in units)
                {
                    if (!actor.IsAlive || !candidate.IsAlive || candidate.Team == actor.Team) continue;
                    float dx = candidate.X - actor.SlamAimX, dy = candidate.Y - actor.SlamAimY;
                    if (dx * dx + dy * dy > actor.SlamRadiusValue * actor.SlamRadiusValue) continue;
                    Damage(actor, candidate, EffectivePower(actor) * 1.1f,
                        "GARDEN_BRIARS", false, false);
                    if (candidate.IsAlive) Poison(actor, candidate, 14f);
                }
                actor.GardenStrikeIndex++;
                actor.PendingSkillId = null;
                actor.AttackPhase = CatalogAttackPhase.Ready;
                if (actor.GardenStrikeIndex < actor.GardenStrikeCount)
                {
                    actor.GardenNextWarningAt = Time + .35f;
                    return;
                }
                actor.GardenStrikeCount = actor.GardenStrikeIndex = 0;
                actor.GardenNextWarningAt = 0f;
                actor.NextBasicAt = Math.Max(actor.NextBasicAt, Time + .45f);
                actor.NextSlamAt = Time + (actor.BossEnraged ? 6f : 8f);
                return;
            }
            if (actor.PendingSkillId == "FROST_NOVA")
            {
                Emit(CatalogEventKind.BossImpact, actor, null, "FROST_NOVA", actor.SlamRadiusValue);
                foreach (CatalogBattleUnit candidate in units)
                {
                    if (!actor.IsAlive || !candidate.IsAlive || candidate.Team == actor.Team) continue;
                    float dx = candidate.X - actor.SlamAimX, dy = candidate.Y - actor.SlamAimY;
                    if (dx * dx + dy * dy <= actor.SlamRadiusValue * actor.SlamRadiusValue)
                    {
                        Damage(actor, candidate, EffectivePower(actor) * (balanceRules >= 3 ? 1.8f : 1.5f),
                            "FROST_NOVA", false, false);
                        if (candidate.IsAlive) Freeze(actor, candidate, balanceRules >= 3 ? .9f : 1.1f);
                    }
                }
                // The second, separately telegraphed circle pressures the front line.
                // Do not retarget the same unit or track its movement after warning.
                if (balanceRules >= 3 && !actor.FrostFollowup && actor.IsAlive)
                {
                    CatalogBattleUnit nearest = null;
                    float distance = float.PositiveInfinity;
                    foreach (CatalogBattleUnit candidate in units)
                    {
                        if (!candidate.IsAlive || candidate.Team == actor.Team || candidate.Id == actor.PendingTargetId) continue;
                        float value = Distance(actor, candidate);
                        if (value < distance) { nearest = candidate; distance = value; }
                    }
                    if (nearest != null)
                    {
                        actor.FrostFollowup = true;
                        actor.PendingTargetId = nearest.Id;
                        actor.SlamAimX = nearest.X; actor.SlamAimY = nearest.Y;
                        actor.ReleaseAt = Time + 1.1f;
                        Emit(CatalogEventKind.BossTelegraph, actor, nearest, "FROST_NOVA", actor.SlamRadiusValue);
                        return;
                    }
                }
                actor.FrostFollowup = false;
                actor.NextBasicAt = Math.Max(actor.NextBasicAt, Time + .45f);
                actor.NextSlamAt = Time + (balanceRules >= 3 ? (actor.BossEnraged ? 5f : 7f) : (actor.BossEnraged ? 6f : 8f));
                actor.PendingSkillId = null;
                actor.AttackPhase = CatalogAttackPhase.Ready;
                return;
            }
            bool furnace = actor.EnemyRole == CatalogEnemyRole.Furnace;
            bool aftershock = actor.PendingSkillId == "GOLEM_AFTERSHOCK" ||
                actor.PendingSkillId == "FURNACE_EMBER";
            string ability = actor.PendingSkillId;
            Emit(CatalogEventKind.BossImpact, actor, null, ability, actor.SlamRadiusValue);
            foreach (CatalogBattleUnit candidate in units)
            {
                if (!actor.IsAlive || !candidate.IsAlive || candidate.Team == actor.Team) continue;
                float dx = candidate.X - actor.SlamAimX, dy = candidate.Y - actor.SlamAimY;
                if (dx * dx + dy * dy <= actor.SlamRadiusValue * actor.SlamRadiusValue)
                {
                    Damage(actor, candidate, EffectivePower(actor) * (aftershock ?
                        furnace ? 1f : 1.1f : furnace ? 1.55f : 1.65f),
                        ability, false, false);
                    if (candidate.IsAlive && ability == "FURNACE_BLAST") Burn(actor, candidate, 10f);
                }
            }
            actor.NextBasicAt = Math.Max(actor.NextBasicAt, Time + .45f);
            if (!aftershock)
            {
                actor.NextSlamAt = Time + 8f;
                if (actor.IsAlive && actor.BossEnraged)
                {
                    actor.PendingSkillId = furnace ? "FURNACE_EMBER" : "GOLEM_AFTERSHOCK";
                    actor.SlamRadiusValue = furnace ? 1f : .75f;
                    actor.ReleaseAt = Time + .6f;
                    Emit(CatalogEventKind.BossTelegraph, actor, Get(actor.PendingTargetId),
                        actor.PendingSkillId, actor.SlamRadiusValue);
                    return;
                }
            }
            actor.PendingSkillId = null;
            actor.AttackPhase = CatalogAttackPhase.Ready;
        }

        void ReleaseBasic(CatalogBattleUnit actor)
        {
            CatalogBattleUnit target = Get(actor.PendingTargetId);
            if (target == null || !target.IsAlive || target.Team == actor.Team) target = ChooseEnemy(actor, false, false);
            actor.NextBasicAt = Time + actor.AttackInterval * (1f - Math.Min(.4f, BuffTotal(actor, CatalogBuffKind.Haste)));
            if (target == null || !InBasicContact(actor, target)) return;
            float coefficient = actor.BasicPowerScale;
            if (actor.ExtraBasicUntil > Time)
            {
                coefficient += actor.ExtraBasicMultiplier;
                actor.ExtraBasicUntil = actor.ExtraBasicMultiplier = 0;
            }
            float bonus = 0f;
            if (HasTrait(actor, "T-AR") && target.MarkRemaining > 0) bonus += .15f;
            if (HasTrait(actor, "T-H01") && target.BurnRemaining > 0) bonus += .2f;
            coefficient *= 1f + Math.Min(1f, bonus);
            Emit(CatalogEventKind.BasicHit, actor, target, "BASIC", coefficient);
            bool alreadySlowed = target.SlowPercent > 0;
            Damage(actor, target, EffectivePower(actor) * coefficient, "BASIC", true, false);
            if (actor.EnemyRole == CatalogEnemyRole.GardenSpitter)
            {
                actor.BasicHitsLanded++;
                if (actor.BasicHitsLanded % 3 == 0 && target.IsAlive)
                    Poison(actor, target, 6f);
            }
            if (target.IsAlive)
            {
                if (HasTrait(actor, "T-FI")) Burn(actor, target);
                if (HasTrait(actor, "T-IC")) Slow(actor, target, .2f, 2f);
                if (HasTrait(actor, "T-H03") && alreadySlowed) Mark(actor, target, 3f);
            }
        }

        void MoveToward(CatalogBattleUnit actor, CatalogBattleUnit target)
        {
            float dx = target.X - actor.X, dy = target.Y - actor.Y;
            bool bodyContact = actor.Range <= 2.1f && !actor.IsBoss && !target.IsBoss &&
                NeedsBodySpacing(actor, target);
            if (bodyContact)
            {
                float reachX, reachY;
                BodyLimits(actor, target, out reachX, out reachY);
                dx = Math.Sign(dx) * Math.Max(0f, Math.Abs(dx) - reachX);
                dy = Math.Sign(dy) * Math.Max(0f, Math.Abs(dy) - reachY);
            }
            bool flanking = SteerAroundFriendlyBlocker(actor, target, ref dx, ref dy);
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distance < .0001f) return;
            float travel = Math.Min(bodyContact || flanking ? distance : distance - actor.Range,
                actor.MoveSpeed * (1f - actor.SlowPercent) * Tick);
            actor.X += dx / distance * travel;
            actor.Y += dy / distance * travel;
        }

        CatalogBattleUnit ChooseEnemy(CatalogBattleUnit actor, bool lowestHp, bool farthest)
        {
            CatalogBattleUnit best = null;
            float bestValue = 0f, bestDistance = 0f;
            bool markedInRange = false;
            if (actor.Range > 2.1f)
                foreach (CatalogBattleUnit candidate in units)
                    if (candidate.IsAlive && candidate.Team != actor.Team && candidate.MarkRemaining > 0 &&
                        Distance(actor, candidate) <= actor.Range) markedInRange = true;
            foreach (CatalogBattleUnit candidate in units)
            {
                if (!candidate.IsAlive || candidate.Team == actor.Team) continue;
                float distance = Distance(actor, candidate);
                if ((lowestHp || farthest) && distance > actor.Range) continue;
                if (!lowestHp && !farthest && markedInRange &&
                    (candidate.MarkRemaining <= 0 || distance > actor.Range)) continue;
                float value = lowestHp ? candidate.Hp / candidate.MaxHp : farthest ? -distance : distance;
                if (best == null || value < bestValue - .00001f ||
                    (lowestHp && Math.Abs(value - bestValue) <= .00001f && distance < bestDistance - .00001f))
                { best = candidate; bestValue = value; bestDistance = distance; }
            }
            return best;
        }

        float EffectivePower(CatalogBattleUnit actor)
        { return actor.Power * (1f + Math.Min(1f, BuffTotal(actor, CatalogBuffKind.Power))); }

        float BuffTotal(CatalogBattleUnit actor, CatalogBuffKind kind)
        {
            float total = 0;
            foreach (CatalogBuff buff in actor.Buffs)
                if (buff.Kind == kind && buff.Until > Time) total += buff.Amount;
            return total;
        }

        void GiveBuff(CatalogBattleUnit source, CatalogBattleUnit target, CatalogBuffKind kind,
            float value, float duration, string ability)
        {
            target.Buffs.Add(new CatalogBuff { Kind = kind, Amount = value, Until = Time + duration });
            Emit(CatalogEventKind.Buff, source, target, ability, value);
            TryHarmony(source, target, ability);
        }

        void Damage(CatalogBattleUnit source, CatalogBattleUnit target, float raw, string ability,
            bool basic, bool periodic)
        {
            if (!target.IsAlive || raw <= 0) return;
            if (source != null && source.Team == CatalogTeam.Ally && target.Team == CatalogTeam.Enemy)
            {
                if (tactics.Contains("shieldcraft") && source.Shield > 0f) raw *= 1.15f;
                if (tactics.Contains("marksmanship") && basic && source.Range > 2.1f) raw *= 1.15f;
                if (tactics.Contains("exploitation") &&
                    (target.MarkRemaining > 0f || target.SlowPercent > 0f)) raw *= 1.18f;
                if (tactics.Contains("spellcraft") && !basic && !periodic) raw *= CatalogBalance.Spellcraft(balanceRules);
                if (tactics.Contains("cinders") && ability == "BURN") raw *= CatalogBalance.Cinders(balanceRules);
                if (legacyResearch.Contains("ember-insight") && !basic && !periodic) raw *= 1.05f;
                if (legacyResearch.Contains("frost-focus") && basic) raw *= 1.05f;
            }
            if (!target.FirstHitUsed && HasTrait(target, "T-KN"))
            {
                target.FirstHitUsed = true;
                GiveBuff(target, target, CatalogBuffKind.DamageReduction, .2f, 3f, "T-KN");
                Emit(CatalogEventKind.TraitTriggered, target, target, "T-KN", .2f);
            }
            float remaining = 1f;
            foreach (CatalogBuff buff in target.Buffs)
                if (buff.Kind == CatalogBuffKind.DamageReduction && buff.Until > Time)
                    remaining *= 1f - buff.Amount;
            float reduction = Math.Min(.5f, 1f - remaining);
            float damage = raw * 100f / (100f + target.Defense) * (1f - reduction);
            float blocked = Math.Min(target.Shield, damage);
            bool broke = blocked > 0 && blocked >= target.Shield - .00001f;
            target.Shield -= blocked;
            if (blocked > 0) Emit(CatalogEventKind.ShieldBlocked, Get(target.ShieldSourceId), target, ability, blocked);
            float hpLoss = Math.Min(target.Hp, damage - blocked);
            target.Hp -= hpLoss;
            Emit(CatalogEventKind.Damage, source, target, ability, hpLoss + blocked);
            if (target.IsAlive && target.BossPatternsEnabled && !target.BossEnraged &&
                (target.EnemyRole == CatalogEnemyRole.Beast && target.Hp <= target.MaxHp * .5f ||
                 target.EnemyRole == CatalogEnemyRole.Golem && target.Hp <= target.MaxHp * .4f ||
                 target.EnemyRole == CatalogEnemyRole.Furnace && target.Hp <= target.MaxHp * .45f ||
                 target.EnemyRole == CatalogEnemyRole.FrostWarden && target.Hp <= target.MaxHp * .4f ||
                 target.EnemyRole == CatalogEnemyRole.GardenLord && target.Hp <= target.MaxHp * .4f ||
                 target.EnemyRole == CatalogEnemyRole.CrownDrake && target.Hp <= target.MaxHp * .4f))
            {
                target.BossEnraged = true;
                Emit(CatalogEventKind.BossEnraged, target, null, "BOSS_ENRAGED", 1f);
            }
            if (!target.IsAlive)
            {
                target.Hp = 0;
                target.ChargeMoving = false;
                target.PendingSkillId = null;
                target.AttackPhase = CatalogAttackPhase.Ready;
                target.FrozenUntil = 0f;
                target.ZoneTicksLeft = 0;
                target.GardenStrikeCount = target.GardenStrikeIndex = 0;
                target.GardenNextWarningAt = 0f;
                target.FrostFollowup = false;
                target.DrakePhase = CrownDrakePhase.Grounded;
                target.BurnTicksLeft = target.PoisonTicksLeft = 0;
                target.BurnUntil = target.PoisonUntil = 0f;
                Emit(CatalogEventKind.Defeated, source, target, ability, 0);
                if (source != null && source.IsAlive && HasTrait(source, "T-H06") && Time >= source.NextSpoilsAt)
                {
                    CatalogBattleUnit ally = LowestAlly(source, 3f, false, true);
                    if (ally != null && Heal(source, ally, EffectivePower(source), "T-H06"))
                    { source.NextSpoilsAt = Time + 4f; Emit(CatalogEventKind.TraitTriggered, source, ally, "T-H06", 1); }
                }
                return;
            }
            if (broke && ability != "FROST_NOVA" && target.FrozenUntil <= Time &&
                HasCounterSkill(target) && Time >= target.NextCounterAt)
            {
                target.NextCounterAt = Time + 2.5f;
                Counter(target, source);
            }
            if (!target.CrisisUsed && target.Hp <= target.MaxHp * .5f && HasTrait(target, "T-BA"))
            {
                target.CrisisUsed = true;
                GiveShield(target, target, target.MaxHp * .12f, 3f, "T-BA");
                Emit(CatalogEventKind.TraitTriggered, target, target, "T-BA", 1);
            }
        }

        bool GiveShield(CatalogBattleUnit source, CatalogBattleUnit target, float value, float duration, string ability)
        {
            if (!target.IsAlive || value <= target.Shield + .00001f) return false;
            target.Shield = value; target.ShieldUntil = Time + duration; target.ShieldSourceId = source.Id;
            Emit(CatalogEventKind.Shield, source, target, ability, value);
            TryHarmony(source, target, ability);
            return true;
        }

        bool Heal(CatalogBattleUnit source, CatalogBattleUnit target, float amount, string ability)
        {
            if (target == null || !target.IsAlive) return false;
            if (HasTrait(target, "T-H02")) amount *= 1.15f;
            if (target.PoisonRemaining > 0f) amount *= .65f;
            float actual = Math.Min(target.MaxHp - target.Hp, amount);
            if (actual <= .00001f) return false;
            target.Hp += actual;
            Emit(CatalogEventKind.Heal, source, target, ability, actual);
            TryHarmony(source, target, ability);
            return true;
        }

        void Burn(CatalogBattleUnit source, CatalogBattleUnit target)
        { Burn(source, target, EffectivePower(source) * .15f); }

        void Burn(CatalogBattleUnit source, CatalogBattleUnit target, float strength)
        {
            if (!target.IsAlive) return;
            if (target.BurnTicksLeft > 0 && strength + .00001f < target.BurnDamage) return;
            if (target.BurnTicksLeft == 0) target.BurnNextAt = Time + 1f;
            target.BurnDamage = strength; target.BurnTicksLeft = 3; target.BurnUntil = Time + 3f;
            target.BurnSourceId = source.Id;
            Emit(CatalogEventKind.Burn, source, target, "BURN", strength);
        }

        void Poison(CatalogBattleUnit source, CatalogBattleUnit target, float strength)
        {
            if (!target.IsAlive) return;
            bool active = target.PoisonUntil > Time;
            if (active && strength + .00001f < target.PoisonDamage) return;
            // Refresh the four-second status without delaying its existing tick cadence.
            if (!active) target.PoisonNextAt = Time + 1f;
            target.PoisonDamage = strength; target.PoisonTicksLeft = 4; target.PoisonUntil = Time + 4f;
            target.PoisonSourceId = source.Id;
            Emit(CatalogEventKind.Poison, source, target, "POISON", strength);
        }

        void Freeze(CatalogBattleUnit source, CatalogBattleUnit target, float duration)
        {
            if (!target.IsAlive) return;
            float extension = Time + duration - Math.Max(Time, target.FrozenUntil);
            if (extension <= 0f) return;
            target.FrozenUntil = Time + duration;
            // Keep a winding attack's remaining lead time, and pause already active
            // volleys/zones so they do not fire several delayed ticks on thaw.
            if (target.AttackPhase != CatalogAttackPhase.Ready) target.ReleaseAt += extension;
            if (target.VolleyShotsLeft > 0) target.NextVolleyAt += extension;
            if (target.ZoneTicksLeft > 0) target.ZoneNextAt += extension;
            Emit(CatalogEventKind.Freeze, source, target, "FROST_NOVA", duration);
        }

        void Slow(CatalogBattleUnit source, CatalogBattleUnit target, float value, float duration)
        {
            if (!target.IsAlive) return;
            value = Math.Min(.4f, value) * (target.IsBoss ? .5f : 1f);
            if (target.SlowUntil > Time && target.Slow > value + .00001f) return;
            target.Slow = value; target.SlowUntil = Time + duration;
            Emit(CatalogEventKind.Slow, source, target, "SLOW", value);
        }

        void Mark(CatalogBattleUnit source, CatalogBattleUnit target, float duration)
        {
            if (!target.IsAlive) return;
            target.MarkUntil = Math.Max(target.MarkUntil, Time + duration);
            Emit(CatalogEventKind.Mark, source, target, "MARK", duration);
        }

        bool HasTrait(CatalogBattleUnit unit, string trait)
        { return unit != null && (unit.InnateTraitId == trait || unit.InheritedTraitId == trait); }

        bool HasCounterSkill(CatalogBattleUnit unit)
        { return unit.FormId == "E-KN-C" || (unit.FormId == "F-KN" && unit.BranchId == "E-KN-C"); }

        CatalogBattleUnit LowestAlly(CatalogBattleUnit source, float range, bool otherOnly, bool needsHealing)
        {
            CatalogBattleUnit best = null;
            float ratio = float.PositiveInfinity, bestDistance = float.PositiveInfinity;
            foreach (CatalogBattleUnit candidate in units)
            {
                if (!candidate.IsAlive || candidate.Team != source.Team || (otherOnly && candidate == source) ||
                    Distance(source, candidate) > range || (needsHealing && candidate.Hp >= candidate.MaxHp)) continue;
                float current = candidate.Hp / candidate.MaxHp;
                float distance = Distance(source, candidate);
                if (current < ratio - .00001f ||
                    (Math.Abs(current - ratio) <= .00001f && distance < bestDistance - .00001f))
                { best = candidate; ratio = current; bestDistance = distance; }
            }
            return best;
        }

        static float Distance(CatalogBattleUnit a, CatalogBattleUnit b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        void CheckEnded()
        {
            bool allies = false, enemies = false;
            foreach (CatalogBattleUnit unit in units)
            {
                if (!unit.IsAlive) continue;
                if (unit.Team == CatalogTeam.Ally) allies = true; else enemies = true;
            }
            if (units.Count < 2 || allies && enemies) return;
            Ended = true;
            Winner = allies ? CatalogTeam.Ally : enemies ? CatalogTeam.Enemy : (CatalogTeam?)null;
            Emit(CatalogEventKind.Ended, null, null, "END", 0);
        }

        void Emit(CatalogEventKind kind, CatalogBattleUnit source, CatalogBattleUnit target, string ability, float amount)
        {
            Action<CatalogBattleEvent> handler = Event;
            if (handler != null) handler(new CatalogBattleEvent
            {
                Kind = kind, SourceId = source == null ? null : source.Id,
                TargetId = target == null ? null : target.Id,
                AbilityId = ability, Amount = amount, Time = Time
            });
        }
    }
}
