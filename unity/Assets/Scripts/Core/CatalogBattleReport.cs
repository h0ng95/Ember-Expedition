using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    public enum CatalogBattleOutcome { Unknown, Victory, Defeat, Timeout, Interrupted }
    // Event-only accounting. Damage includes HP loss and shield damage, as emitted by CatalogBattle.
    public sealed class CatalogBattleReport : IDisposable
    {
        public sealed class AllyContribution
        {
            public string Id { get; private set; }
            public string FormId { get; private set; }
            public float Damage { get; internal set; }
            public float Healing { get; internal set; }
            public float Protected { get; internal set; }

            internal AllyContribution(CatalogBattleUnit unit)
            {
                Id = unit.Id;
                FormId = unit.FormId;
            }
        }

        readonly CatalogBattle battle;
        readonly Dictionary<string, AllyContribution> byId =
            new Dictionary<string, AllyContribution>(StringComparer.Ordinal);
        readonly IReadOnlyList<AllyContribution> allies;
        bool disposed;

        public IReadOnlyList<AllyContribution> Allies { get { return allies; } }
        public CatalogBattleOutcome Outcome { get; private set; }
        public float Duration { get; private set; }
        public float EnemyHpLeft { get; private set; }
        public int AlliesAlive { get; private set; }
        public int EnemiesAlive { get; private set; }
        public string FirstLostFormId { get; private set; }
        public string FirstLostUnitId { get; private set; }
        public float FirstLossAt { get; private set; }
        public bool FirstLostWasRanged { get; private set; }

        // Capture evidence once, before the view releases this battle. Reading a report
        // never advances combat or infers a timeout from a restored run's victory flag.
        public void Complete(bool victory)
        {
            if (Outcome != CatalogBattleOutcome.Unknown || disposed) return;
            Duration = battle.Time;
            foreach (var unit in battle.Units)
                if (unit.Team == CatalogTeam.Ally) { if (unit.IsAlive) AlliesAlive++; }
                else { if (unit.IsAlive) EnemiesAlive++; EnemyHpLeft += Math.Max(0, unit.Hp); }
            Outcome = victory && battle.Ended && battle.Winner == CatalogTeam.Ally ? CatalogBattleOutcome.Victory :
                !victory && battle.Ended && battle.Winner == CatalogTeam.Enemy ? CatalogBattleOutcome.Defeat :
                !victory && !battle.Ended && Duration >= 74.99f && AlliesAlive > 0 && EnemiesAlive > 0 ?
                    CatalogBattleOutcome.Timeout : CatalogBattleOutcome.Interrupted;
            Dispose();
        }

        public CatalogBattleReport(CatalogBattle battle)
        {
            if (battle == null) throw new ArgumentNullException("battle");
            this.battle = battle;
            FirstLossAt = -1f;
            var rows = new List<AllyContribution>();
            foreach (CatalogBattleUnit unit in battle.Units)
            {
                if (unit.Team != CatalogTeam.Ally) continue;
                var row = new AllyContribution(unit);
                rows.Add(row);
                byId.Add(unit.Id, row);
            }
            allies = rows.AsReadOnly();
            battle.Event += OnEvent;
        }

        void OnEvent(CatalogBattleEvent eventData)
        {
            if (disposed || eventData == null) return;
            if (eventData.Kind == CatalogEventKind.Defeated && FirstLostFormId == null)
            {
                CatalogBattleUnit lost = battle.Get(eventData.TargetId);
                if (lost != null && lost.Team == CatalogTeam.Ally)
                { FirstLostFormId = lost.FormId; FirstLostUnitId = lost.Id; FirstLossAt = battle.Time; FirstLostWasRanged = lost.Range > 2.1f; }
            }
            if (eventData.Amount <= 0f) return;
            AllyContribution source;
            if (eventData.SourceId == null || !byId.TryGetValue(eventData.SourceId, out source)) return;
            CatalogBattleUnit actor = battle.Get(eventData.SourceId);
            CatalogBattleUnit target = battle.Get(eventData.TargetId);
            if (actor == null || actor.Team != CatalogTeam.Ally || target == null) return;
            switch (eventData.Kind)
            {
                case CatalogEventKind.Damage:
                    if (target.Team == CatalogTeam.Enemy) source.Damage += eventData.Amount;
                    break;
                case CatalogEventKind.Heal:
                    if (target.Team == CatalogTeam.Ally) source.Healing += eventData.Amount;
                    break;
                case CatalogEventKind.ShieldBlocked:
                    if (target.Team == CatalogTeam.Ally) source.Protected += eventData.Amount;
                    break;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            battle.Event -= OnEvent;
        }
    }
}
