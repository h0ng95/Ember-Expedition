using Expedition.Core;

namespace Expedition.Presentation
{
    public enum CombatEffectStyle { Blade, Blunt, Pierce, Arrow, FrostArrow, Fire, Ice, Holy, Nature, Claw, Stone }

    // Presentation only. Weapon identity comes from the approved sheets, never translated names.
    public static class CombatEffectProfile
    {
        public static CombatEffectStyle Basic(CatalogBattleUnit unit)
        {
            if (unit == null) return CombatEffectStyle.Blunt;
            // Slingers share Raider combat AI with archers, but carry stones in their art.
            if (unit.Id != null && unit.Id.StartsWith("mine-slinger-",System.StringComparison.Ordinal))
                return CombatEffectStyle.Stone;
            switch (unit.EnemyRole)
            {
                case CatalogEnemyRole.Beast: return CombatEffectStyle.Claw;
                case CatalogEnemyRole.Raider: return unit.Range > 2.1f ? CombatEffectStyle.Arrow : CombatEffectStyle.Blade;
                case CatalogEnemyRole.GardenSpitter: case CatalogEnemyRole.GardenLord: return CombatEffectStyle.Nature;
                case CatalogEnemyRole.CrownDrake: return CombatEffectStyle.Fire;
                case CatalogEnemyRole.MineWorker: case CatalogEnemyRole.Supervisor:
                    return unit.Range > 2.1f ? CombatEffectStyle.Stone : CombatEffectStyle.Blunt;
                case CatalogEnemyRole.Golem: case CatalogEnemyRole.Furnace: case CatalogEnemyRole.FrostWarden:
                    return CombatEffectStyle.Blunt;
            }
            string id = unit.FormId ?? "";
            if (id == "E-KN-G" || id == "H-02" || id == "F-BA") return CombatEffectStyle.Blunt;
            if (id == "H-03") return CombatEffectStyle.FrostArrow;
            if (id == "H-05") return CombatEffectStyle.Holy;
            if (id == "H-06" || id.Contains("-AR")) return CombatEffectStyle.Arrow;
            if (id == "H-04" || id.Contains("-FI")) return CombatEffectStyle.Fire;
            if (id.Contains("-IC")) return CombatEffectStyle.Ice;
            if (id.Contains("-PR")) return CombatEffectStyle.Holy;
            if (id.Contains("-BA")) return CombatEffectStyle.Pierce;
            if (id.Contains("-KN") || id == "H-01") return CombatEffectStyle.Blade;
            return unit.Range > 2.1f ? CombatEffectStyle.Stone : CombatEffectStyle.Blunt;
        }

        public static CombatEffectStyle Impact(CatalogBattleUnit source, string ability)
        {
            switch (ability)
            {
                case "FURNACE_BLAST": case "FURNACE_EMBER": case "DRACONIC_BREATH":
                case "DRACONIC_BOMB": return CombatEffectStyle.Fire;
                case "FROST_NOVA": return CombatEffectStyle.Ice;
                case "GARDEN_BRIARS": return CombatEffectStyle.Nature;
                case "GOLEM_SLAM": case "GOLEM_AFTERSHOCK": case "DRACONIC_LAND":
                case "BOSS_CHARGE": return CombatEffectStyle.Blunt;
                default: return Basic(source);
            }
        }
    }
}
