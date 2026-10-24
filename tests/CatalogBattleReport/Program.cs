using System;
using System.IO;
using System.Reflection;
using Expedition.Core;

internal static class Program
{
    static int assertions;
    static readonly MethodInfo Emit = typeof(CatalogBattle).GetMethod("Emit",
        BindingFlags.Instance | BindingFlags.NonPublic);

    static int Main(string[] args)
    {
        try
        {
            CatalogDatabase database = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
            EventRouting(database);
            ActualOverkill(database);
            Console.WriteLine("PASS: battle report, " + assertions + " assertions.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error); return 1; }
    }

    static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    static void Send(CatalogBattle battle, CatalogEventKind kind,
        CatalogBattleUnit source, CatalogBattleUnit target, float amount)
    {
        Emit.Invoke(battle, new object[] { kind, source, target, "TEST", amount });
    }

    static void EventRouting(CatalogDatabase database)
    {
        var battle = new CatalogBattle(database);
        CatalogBattleUnit guard = battle.AddUnit("guard", CatalogTeam.Ally, "B-KN", null, null, -1, 0);
        CatalogBattleUnit healer = battle.AddUnit("healer", CatalogTeam.Ally, "B-PR", null, null, -2, 0);
        CatalogBattleUnit enemy = battle.AddEnemy("enemy", 100, 8, 0, 1f, 1f, 1f, false, 1, 0);
        var report = new CatalogBattleReport(battle);
        Check(report.Allies.Count == 2 && report.Allies[0].Id == "guard" &&
            report.Allies[1].Id == "healer", "allies retain battle order");

        // Damage already contains shield loss. ShieldBlocked is credited only to the shield owner.
        Send(battle, CatalogEventKind.Damage, guard, enemy, 9);
        Send(battle, CatalogEventKind.ShieldBlocked, healer, guard, 4);
        Send(battle, CatalogEventKind.Heal, healer, guard, 3);
        Send(battle, CatalogEventKind.Damage, enemy, guard, 7);
        Send(battle, CatalogEventKind.Heal, enemy, enemy, 8);
        Send(battle, CatalogEventKind.Damage, guard, healer, 5);
        Send(battle, CatalogEventKind.ShieldBlocked, healer, enemy, 6);
        Send(battle, CatalogEventKind.ShieldBlocked, enemy, guard, 2);
        Send(battle, CatalogEventKind.Shield, healer, guard, 20);
        Send(battle, CatalogEventKind.BasicHit, guard, enemy, 2);
        Check(report.Allies[0].Damage == 9 && report.Allies[0].Healing == 0 &&
            report.Allies[0].Protected == 0, "only actual offensive Damage is credited");
        Check(report.Allies[1].Damage == 0 && report.Allies[1].Healing == 3 &&
            report.Allies[1].Protected == 4, "heal and shield prevention credit their sources once");

        report.Dispose();
        report.Dispose();
        Send(battle, CatalogEventKind.Damage, guard, enemy, 10);
        Check(report.Allies[0].Damage == 9, "disposed report unsubscribes without losing its snapshot");
    }

    static void ActualOverkill(CatalogDatabase database)
    {
        var battle = new CatalogBattle(database);
        battle.AddUnit("attacker", CatalogTeam.Ally, "B-KN", null, null, -1, 0);
        battle.AddEnemy("fragile", 1, 1, 0, 1f, 1f, 1f, false, 1, 0);
        using (var report = new CatalogBattleReport(battle))
        {
            battle.Step(20f);
            Check(battle.Ended && battle.Winner == CatalogTeam.Ally, "control battle ends in victory");
            Check(Math.Abs(report.Allies[0].Damage - 1f) < .0001f,
                "battle event excludes overkill from report");
        }
    }
}
