using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Expedition.Core;

internal static class ExpeditionResearchTests
{
    static int checks;
    static CatalogDatabase database;
    static readonly MethodInfo damage = typeof(CatalogBattle).GetMethod("Damage",
        BindingFlags.Instance | BindingFlags.NonPublic);

    static void Check(bool condition, string reason)
    {
        checks++;
        if (!condition) throw new Exception(reason);
    }

    static void Close(float actual, float expected, string reason)
    { Check(Math.Abs(actual - expected) < .001f, reason + ": " + actual + " vs " + expected); }

    static CatalogBattle Battle(string[] research, string form, out CatalogBattleUnit ally,
        out CatalogBattleUnit enemy)
    {
        var battle = new CatalogBattle(database, research);
        ally = battle.AddUnit("ally", CatalogTeam.Ally, form, null, null, -1f, 0f);
        enemy = battle.AddEnemy("enemy", 10000f, 10f, 0f, 2f, 1f, .1f,
            false, 1f, 0f, CatalogEnemyRole.MineWorker);
        return battle;
    }

    static float Hit(CatalogBattle battle, CatalogBattleUnit source, CatalogBattleUnit target,
        string ability, bool basic, bool periodic)
    {
        float before = target.Hp;
        damage.Invoke(battle, new object[] { source, target, 100f, ability, basic, periodic });
        return before - target.Hp;
    }

    static void Effects()
    {
        CatalogBattleUnit ally, enemy;
        var plain = Battle(null, "B-KN", out ally, out enemy);
        float ordinary = Hit(plain, ally, enemy, "BASIC", true, false);
        Close(ordinary, 100f, "Unresearched attack is unchanged.");
        var bulwark = Battle(new[] { "bulwark" }, "B-KN", out ally, out enemy);
        Close(ally.MaxHp / database.Get("B-KN").Hp, 1.12f,
            "Bulwark grants a fixed twelve-percent maximum HP to a melee ally.");
        Close(ally.Hp, ally.MaxHp, "Bulwark starts each battle at full adjusted HP.");
        Battle(new[] { "bulwark" }, "B-AR", out ally, out enemy);
        Close(ally.MaxHp, database.Get("B-AR").Hp, "Bulwark does not grow ranged ally HP.");

        var shieldcraft = Battle(new[] { "bulwark", "shieldcraft" }, "B-KN", out ally, out enemy);
        Close(Hit(shieldcraft, ally, enemy, "BASIC", true, false), 100f,
            "Shieldcraft has no effect without an active shield.");
        ally.Shield = 20f;
        Close(Hit(shieldcraft, ally, enemy, "BASIC", true, false), 115f,
            "An active shield buffs ally damage once.");
        ally.Shield = 0f;
        Close(Hit(shieldcraft, ally, enemy, "BASIC", true, false), 100f,
            "Losing the shield removes the bonus immediately.");

        var marksmanship = Battle(new[] { "marksmanship" }, "B-AR", out ally, out enemy);
        Close(Hit(marksmanship, ally, enemy, "BASIC", true, false), 115f,
            "Ranged basic attacks gain the precision bonus.");
        Close(Hit(marksmanship, ally, enemy, "E-AR-P", false, false), 100f,
            "Marksmanship does not buff direct skills.");
        var exploitation = Battle(new[] { "marksmanship", "exploitation" }, "B-AR", out ally, out enemy);
        enemy.MarkUntil = 10f;
        enemy.Slow = .2f; enemy.SlowUntil = 10f;
        Close(Hit(exploitation, ally, enemy, "BASIC", true, false), 115f * 1.18f,
            "Marked and slowed together apply exploitation only once.");
        enemy.MarkUntil = 0f;
        Close(Hit(exploitation, ally, enemy, "BASIC", true, false), 115f * 1.18f,
            "Slow alone qualifies for exploitation.");
        enemy.SlowUntil = 0f;
        Close(Hit(exploitation, ally, enemy, "BASIC", true, false), 115f,
            "Expired control removes exploitation without affecting precision.");

        var spellcraft = Battle(new[] { "spellcraft" }, "B-AR", out ally, out enemy);
        Close(Hit(spellcraft, ally, enemy, "E-AR-P", false, false), 115f,
            "Direct damaging skills gain spellcraft.");
        Close(Hit(spellcraft, ally, enemy, "BASIC", true, false), 100f,
            "Spellcraft excludes basic attacks.");
        Close(Hit(spellcraft, ally, enemy, "BURN", false, true), 100f,
            "Spellcraft excludes periodic burn.");
        var cinders = Battle(new[] { "spellcraft", "cinders" }, "B-AR", out ally, out enemy);
        Close(Hit(cinders, ally, enemy, "BURN", false, true), 130f,
            "Cinders buffs ally burn without inheriting spellcraft's direct-skill bonus.");
        Close(Hit(cinders, enemy, ally, "BURN", false, true),
            100f * 100f / (100f + ally.Defense),
            "Research never buffs enemy-sourced burn.");
    }

    static void Definitions()
    {
        Check(CatalogResearch.Nodes.Count == 6 && CatalogResearch.MaxNodes == 3,
            "Six stable nodes form three two-tier branches and a three-node run cap.");
        foreach (CatalogResearchNode node in CatalogResearch.Nodes)
            Check(CatalogResearch.Get(node.Id) == node && node.Cost == (node.Tier == 1 ? 40 : 60) &&
                node.UnlockWave == (node.Tier == 1 ? 6 : 11) &&
                !string.IsNullOrEmpty(node.Title) && !string.IsNullOrEmpty(node.Description),
                "Research metadata is complete and unlock timing matches its tier.");
        Check(CatalogResearch.Get("unknown") == null, "Unknown research has no definition.");
        bool invalid = false;
        try { new CatalogBattle(database, new[] { "shieldcraft" }); }
        catch (ArgumentException) { invalid = true; }
        Check(invalid, "Battle rejects an advanced node without its prerequisite.");
        invalid = false;
        try { new CatalogBattle(database, new[] { "bulwark", "bulwark" }); }
        catch (ArgumentException) { invalid = true; }
        Check(invalid, "Battle rejects duplicate research instead of stacking it.");
    }

    static int Main(string[] args)
    {
        try
        {
            database = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
            Definitions(); Effects(); RunEconomyAndSaves();
            Console.WriteLine("PASS: expedition research, " + checks + " assertions.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error);
            return 1;
        }
    }

    static void AdvanceTo(ExpeditionRun run, int wave)
    {
        while (run.Wave < wave)
        {
            Check(run.BeginBattle() != null && run.ResolveBattle(true) && run.Continue(),
                "Legal fixture advances to its next preparation.");
        }
    }

    static void RejectRestore(ExpeditionRunSave save, string reason)
    {
        bool rejected = false;
        try { ExpeditionRun.Restore(database, save); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, reason);
    }

    static void RunEconomyAndSaves()
    {
        var run = ExpeditionRun.NewLegacy(database, 73);
        Check(run.TacticIds.Count == 0 && run.LegacyResearchIds.Count == 0 &&
            run.StartFormationId == "standard" && run.Roster.Find("ally-02").FormId == "B-AR",
            "Ordinary new runs retain the original starting trio and no research.");
        uint initialRandom = run.Capture().RandomState;
        Check(!run.CanPurchaseTactic("bulwark") && !run.PurchaseTactic("bulwark") &&
            run.Gold == 40 && run.Capture().RandomState == initialRandom,
            "Wave-one research cannot spend gold or advance summon RNG.");
        AdvanceTo(run, 6);
        Check(run.CanPurchaseTactic("bulwark") && !run.CanPurchaseTactic("shieldcraft") &&
            !run.CanPurchaseTactic("unknown"),
            "Base node unlocks at six while advanced and unknown nodes remain unavailable.");
        int beforeGold = run.Gold;
        uint beforeRandom = run.Capture().RandomState;
        Check(run.Move("ally-01", -1.6f, 0f) && run.PurchaseTactic("bulwark") &&
            run.Gold == beforeGold - 40 && run.TacticIds.SequenceEqual(new[] { "bulwark" }) &&
            run.Capture().RandomState == beforeRandom,
            "Purchase spends exactly forty gold without rerolling summon RNG.");
        Check(run.Undo() && run.TacticIds.SequenceEqual(new[] { "bulwark" }) &&
            run.Gold == beforeGold - 40,
            "Preparation Undo cannot refund a confirmed tactic purchase.");
        Check(!run.PurchaseTactic("bulwark") && !run.PurchaseTactic("shieldcraft") &&
            run.Gold == beforeGold - 40 && run.Capture().RandomState == beforeRandom,
            "Duplicate and too-early advanced purchases fail atomically.");
        ExpeditionRunSave copied = run.Capture();
        copied.TacticIds[0] = "spellcraft";
        Check(run.TacticIds.Single() == "bulwark", "Captured arrays do not alias active run research.");
        copied = run.Capture();
        var restored = ExpeditionRun.Restore(database, copied);
        Check(restored.TacticIds.Single() == "bulwark" && restored.Gold == run.Gold &&
            restored.Capture().RandomState == run.Capture().RandomState,
            "Tactic selection, budget and RNG survive a v8 roundtrip.");
        CatalogBattle battle = restored.BeginBattle();
        Check(battle.TacticIds.Single() == "bulwark" &&
            battle.Get("ally-01").MaxHp > database.Get("B-KN").Hp &&
            !restored.PurchaseTactic("marksmanship"),
            "Current run tactics enter battle, and battle phase rejects purchases.");
        Check(restored.ResolveBattle(true) && !restored.PurchaseTactic("marksmanship") &&
            restored.Continue(), "Result phase rejects purchases before continuing.");
        AdvanceTo(restored, 11);
        Check(restored.CanPurchaseTactic("shieldcraft") &&
            restored.PurchaseTactic("marksmanship") && restored.PurchaseTactic("shieldcraft") &&
            restored.TacticIds.Count == 3 && !restored.CanPurchaseTactic("spellcraft") &&
            !restored.PurchaseTactic("spellcraft"),
            "Advanced tactic requires its branch base, costs sixty, and three nodes cap the run.");
        int capGold = restored.Gold;
        Check(restored.Gold == capGold && ExpeditionRun.Restore(database, restored.Capture()).Gold == capGold,
            "A failed fourth purchase does not change the saved gold ledger.");
        ExpeditionRunSave forged = restored.Capture();
        forged.Gold += 20;
        RejectRestore(forged, "V8 budget cannot mint gold after tactic purchases.");
        forged = restored.Capture(); forged.TacticIds = new[] { "bulwark", "bulwark" };
        RejectRestore(forged, "Duplicate tactic IDs cannot stack or bypass the budget.");
        forged = restored.Capture(); forged.TacticIds = new[] { "shieldcraft" };
        RejectRestore(forged, "Advanced tactic without its parent is rejected.");
        forged = restored.Capture(); forged.TacticIds = null;
        RejectRestore(forged, "V8 requires an explicit, copy-safe tactic selection.");
        forged = restored.Capture(); forged.Wave = 5;
        RejectRestore(forged, "A tactic cannot appear before its unlock wave.");
        Check(ExpeditionRun.NewLegacy(database, 73).TacticIds.Count == 0,
            "A new expedition never inherits run-only tactics.");

        string[] external = { "tactical-start", "tactical-vigor" };
        var tactical = ExpeditionRun.NewLegacy(database, 91, ExpeditionDifficulty.Normal,
            external, "tactical");
        external[0] = "invalid";
        Check(tactical.LegacyResearchIds.SequenceEqual(new[] { "tactical-start", "tactical-vigor" }) &&
            tactical.StartFormationId == "tactical" &&
            tactical.Roster.Find("ally-02").FormId == "B-BA",
            "Start formation and permanent research are immutable expedition snapshots.");
        CatalogBattle tacticalBattle = tactical.BeginBattle();
        Close(tacticalBattle.Get("ally-01").MaxHp / database.Get("B-KN").Hp, 1.05f,
            "Permanent vigor affects this expedition's ally health.");
        Check(ExpeditionRun.Restore(database, tactical.Capture()).StartFormationId == "tactical" &&
            ExpeditionRun.Restore(database, tactical.Capture()).LegacyResearchIds.Count == 2,
            "V8 reload preserves the original permanent research snapshot.");
        forged = tactical.Capture(); forged.LegacyResearchIds[0] = "invalid";
        RejectRestore(forged, "Unknown permanent research cannot enter a v8 save.");
        forged = tactical.Capture(); forged.LegacyResearchIds = new string[0];
        RejectRestore(forged, "A nonstandard starting formation requires its permanent unlock.");
        bool unavailable = false;
        try { ExpeditionRun.NewLegacy(database, 91, ExpeditionDifficulty.Normal, null, "tactical"); }
        catch (ArgumentException) { unavailable = true; }
        Check(unavailable, "A new run cannot select a locked starting formation.");

        ExpeditionRunSave old = ExpeditionRun.NewLegacy(database, 107).Capture();
        old.Version = 7; old.TacticIds = null; old.LegacyResearchIds = null;
        old.StartFormationId = null;
        ExpeditionRun migrated = ExpeditionRun.Restore(database, old);
        Check(migrated.Capture().Version == 8 && migrated.TacticIds.Count == 0 &&
            migrated.LegacyResearchIds.Count == 0 && migrated.StartFormationId == "standard" &&
            migrated.Gold == 40 && migrated.Capture().RandomState == old.RandomState,
            "Genuine v7 economy upgrades to v8 with no tactic or permanent research grant.");
    }
}
