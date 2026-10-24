using System;
using System.Collections.Generic;

namespace Expedition.Core
{
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

        public CatalogBattleReport(CatalogBattle battle)
        {
            if (battle == null) throw new ArgumentNullException("battle");
            this.battle = battle;
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
            if (disposed || eventData == null || eventData.Amount <= 0f) return;
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
