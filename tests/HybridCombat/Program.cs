using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Expedition.Core;

internal static class HybridCombatTests
{
    static int checks;
    static void Check(bool condition, string reason)
    {
        checks++;
        if (!condition)
        {
            Console.Error.WriteLine("FAIL: " + reason);
            Environment.Exit(1);
        }
    }

    static void Cast(CatalogBattle battle, CatalogBattleUnit actor,
        CatalogBattleUnit target, string skill)
    {
        typeof(CatalogBattle).GetMethod("ExecuteSkill", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(battle, new object[] { actor, target, skill });
    }

    static CatalogBattleUnit Ally(CatalogBattle battle, string id, string form, float x)
    { return battle.AddUnit(id, CatalogTeam.Ally, form, null, null, x, 0f); }

    static void WaitForFlameWindup(CatalogBattle battle, CatalogBattleUnit front)
    {
        for (int i = 0; i < 120 && front.PendingSkillId != "H-01"; i++) battle.Step(.05f);
        Check(front.PendingSkillId == "H-01" && front.Shield == 0f,
            "Flame slash should wind before its shield is granted.");
    }

    static int Main(string[] args)
    {
        CatalogDatabase db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
        var flame = db.Get("H-01");
        var song = db.Get("H-05");
        Check(flame.Description != null && flame.Description.Contains("보호막") &&
            song.Description != null && song.Description.Contains("동료"),
            "The updated hybrid roles should be available from the catalog.");

        var battle = new CatalogBattle(db);
        CatalogBattleUnit front = Ally(battle, "front", "H-01", 0f);
        CatalogBattleUnit ally = Ally(battle, "other", "B-AR", -.5f);
        CatalogBattleUnit enemy = battle.AddEnemy("enemy", 10000f, 1f, 0f, 100f, 1f,
            .001f, false, 1f, 0f);
        int shieldEvents = 0;
        battle.Event += e => {
            if (e.Kind == CatalogEventKind.Shield && e.AbilityId == "H-01") shieldEvents++;
        };
        Cast(battle, front, enemy, "H-01");
        float firstShield = front.Shield;
        Check(enemy.Hp < enemy.MaxHp && enemy.BurnRemaining > 0f,
            "Flame slash should still attack and apply burn.");
        Check(firstShield > 0f && firstShield < front.MaxHp * .2f && ally.Shield == 0f,
            "Flame slash should protect its frontliner without shielding the whole party.");
        Cast(battle, front, enemy, "H-01");
        Check(Math.Abs(front.Shield - firstShield) < .0001f && shieldEvents == 1,
            "Repeated casts should not stack an unspent self shield.");

        battle = new CatalogBattle(db);
        CatalogBattleUnit singer = Ally(battle, "singer", "H-05", 0f);
        CatalogBattleUnit nearby = Ally(battle, "near", "B-KN", .5f);
        CatalogBattleUnit injured = Ally(battle, "injured", "B-FI", 3.4f);
        CatalogBattleUnit critical = Ally(battle, "critical", "B-AR", 4.4f);
        CatalogBattleUnit outside = Ally(battle, "outside", "B-IC", 4.6f);
        singer.Hp = singer.MaxHp * .5f;
        nearby.Hp = nearby.MaxHp * .9f;
        injured.Hp = injured.MaxHp * .1f;
        critical.Hp = critical.MaxHp * .2f;
        outside.Hp = outside.MaxHp * .1f;
        float nearBefore = nearby.Hp, injuredBefore = injured.Hp,
            criticalBefore = critical.Hp, outsideBefore = outside.Hp;
        float selfBefore = singer.Hp;
        Cast(battle, singer, null, "H-05");
        Check(singer.Hp > singer.MaxHp * .5f && injured.Hp > injuredBefore &&
            critical.Hp > criticalBefore,
            "Song should heal self and the two most injured reachable allies.");
        Check(nearby.Hp == nearBefore && outside.Hp == outsideBefore,
            "Closer healthy and unreachable critical allies should not displace wounded targets.");
        Check(singer.Buffs.Any() && injured.Buffs.Any() && critical.Buffs.Any() &&
            !nearby.Buffs.Any() && !outside.Buffs.Any(),
            "The support buff should follow the same two healing targets.");
        Check(Math.Abs((singer.Hp - selfBefore) - (injured.Hp - injuredBefore)) < .001f &&
            Math.Abs((singer.Hp - selfBefore) - (critical.Hp - criticalBefore)) < .001f,
            "Self and ally healing should use the same cast-time power before the self buff.");

        battle = new CatalogBattle(db);
        front = Ally(battle, "front", "H-01", 0f);
        enemy = battle.AddEnemy("enemy", 10000f, 1f, 0f, 100f, 1f,
            .001f, false, 1f, 0f);
        WaitForFlameWindup(battle, front);
        enemy.X = 5f;
        battle.Step(.3f);
        Check(front.Shield == 0f,
            "A target leaving range during windup must not grant a free shield.");

        battle = new CatalogBattle(db);
        front = Ally(battle, "front", "H-01", 0f);
        enemy = battle.AddEnemy("enemy", 10000f, 1f, 0f, 100f, 1f,
            .001f, false, 1f, 0f);
        battle.AddEnemy("backup", 10000f, 1f, 0f, 100f, 1f,
            .001f, false, 5f, 0f);
        WaitForFlameWindup(battle, front);
        enemy.Hp = 0f;
        battle.Step(.3f);
        Check(front.Shield == 0f,
            "A target dying during windup with no replacement in range must not grant a shield.");

        battle = new CatalogBattle(db);
        front = Ally(battle, "front", "H-01", 0f);
        enemy = battle.AddEnemy("enemy", 10000f, 1f, 0f, 100f, 1f,
            .001f, false, 1f, 0f);
        WaitForFlameWindup(battle, front);
        battle.Step(.3f);
        Check(front.Shield > 0f && enemy.Hp < enemy.MaxHp,
            "A released flame slash that actually hits should grant the shield.");

        battle = new CatalogBattle(db);
        singer = Ally(battle, "singer", "H-05", 0f);
        Ally(battle, "friend", "B-PR", -.5f);
        battle.AddEnemy("distant", 10000f, 1f, 0f, 100f, 1f,
            .001f, false, 6f, 0f);
        var castTimes = new System.Collections.Generic.List<float>();
        battle.Event += e => {
            if (e.Kind == CatalogEventKind.SkillUsed && e.SourceId == singer.Id &&
                e.AbilityId == "H-05") castTimes.Add(e.Time);
        };
        battle.Step(10f);
        Check(castTimes.Count == 2 && castTimes[1] - castTimes[0] > 7.15f &&
            castTimes[1] - castTimes[0] < 7.35f,
            "Live support songs should be separated by cooldown plus a new windup.");
        Console.WriteLine("PASS: hybrid combat, " + checks + " assertions.");
        return 0;
    }
}
