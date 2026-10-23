using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Expedition.Core;

internal static class Program
{
    static int assertions;
    static CatalogDatabase catalog;

    static int Main(string[] args)
    {
        try
        {
            catalog = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
            CatalogContainsThirtyForms();
            GrowthAndCombination();
            AllRecipesAndSupply();
            AllSkillsExecute();
            FirstHitAndDefense();
            ShieldBreakAndBurn();
            FinalBranchPriority();
            PhysicalSpacingAndReach();
            SeedDeterminism();
            Console.WriteLine("PASS: catalog combat regression, " + assertions + " assertions.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error); return 1; }
    }

    static void Check(bool value, string reason)
    {
        assertions++;
        if (!value) throw new Exception(reason);
    }

    static bool BodyOverlap(CatalogBattleUnit a, CatalogBattleUnit b,
        float widthA, float heightA, float widthB, float heightB)
    {
        float overlapX = (widthA + widthB) * .5f - Math.Abs(a.X - b.X) * 83f;
        float footA = -a.Y * 48f, footB = -b.Y * 48f;
        float overlapY = Math.Min(footA, footB) - Math.Max(footA - heightA, footB - heightB);
        return overlapX > .3f * Math.Min(widthA, widthB) + 2f &&
            overlapY > .3f * Math.Min(heightA, heightB) + 2f;
    }

    static bool HasVisibleBodyOverlap(CatalogBattleUnit a, CatalogBattleUnit b,
        float widthA, float heightA, float widthB, float heightB)
    {
        float overlapX = (widthA + widthB) * .5f - Math.Abs(a.X - b.X) * 83f;
        float footA = -a.Y * 48f, footB = -b.Y * 48f;
        float overlapY = Math.Min(footA, footB) - Math.Max(footA - heightA, footB - heightB);
        return overlapX > 4f && overlapY > 4f;
    }

    static void PhysicalSpacingAndReach()
    {
        var crowded = new CatalogBattle(catalog);
        var ally = crowded.AddUnit("ally", CatalogTeam.Ally, "B-KN", null, null, 0, 0);
        var cub1 = crowded.AddEnemy("beast-1", 100, 8, 0, 1.8f, 1, .65f,
            false, 0, 0, CatalogEnemyRole.Beast);
        var cub2 = crowded.AddEnemy("beast-2", 100, 8, 0, 1.8f, 1, .65f,
            false, 0, 0, CatalogEnemyRole.Beast);
        var elite = crowded.AddEnemy("elite-beast", 250, 20, 0, 1.8f, 1, .65f,
            false, 0, 0, CatalogEnemyRole.Beast);
        crowded.Step(1f / 60f);
        Check(!BodyOverlap(ally, cub1, 70, 80, 100, 70) &&
            !BodyOverlap(ally, cub2, 70, 80, 100, 70) &&
            !BodyOverlap(ally, elite, 70, 80, 200, 125) &&
            !BodyOverlap(cub1, cub2, 100, 70, 100, 70) &&
            !BodyOverlap(cub1, elite, 100, 70, 200, 125) &&
            !BodyOverlap(cub2, elite, 100, 70, 200, 125),
            "identical starts keep ordinary enemies and allies substantially visible");
        foreach (CatalogBattleUnit unit in crowded.Units)
            Check(unit.X >= -6.4f && unit.X <= 6.4f && unit.Y >= -2.35f && unit.Y <= 2.35f,
                "crowded separation remains inside the rendered viewport");
        var partial = new CatalogBattle(catalog);
        var small = partial.AddUnit("small-ally", CatalogTeam.Ally, "B-KN", null, null, 0, 0);
        var large = partial.AddEnemy("elite-beast", 340, 38, 12, 1.8f, 1f, .7f,
            false, 0, 0, CatalogEnemyRole.Beast);
        partial.Step(1f / 60f);
        Check(HasVisibleBodyOverlap(small, large, 70, 80, 200, 125),
            "single small and large actors retain modest visible silhouette overlap");
        Check(!BodyOverlap(small, large, 70, 80, 200, 125),
            "large elite leaves at least 70 percent of the smaller actor clear on one axis");
        var packed = new CatalogBattle(catalog);
        var packedUnits = new List<CatalogBattleUnit>();
        for (int i = 0; i < 3; i++)
            packedUnits.Add(packed.AddUnit("packed-ally-" + i, CatalogTeam.Ally,
                "B-KN", null, null, 0, 0));
        for (int i = 0; i < 5; i++)
            packedUnits.Add(packed.AddEnemy("packed-beast-" + i, 100, 8, 0, 1.8f, 1, .65f,
                false, 0, 0, CatalogEnemyRole.Beast));
        packed.Step(1f / 60f);
        for (int i = 0; i < packedUnits.Count; i++)
        for (int j = i + 1; j < packedUnits.Count; j++)
        {
            CatalogBattleUnit a = packedUnits[i], b = packedUnits[j];
            if (a.Team == CatalogTeam.Ally && b.Team == CatalogTeam.Ally) continue;
            Check(!BodyOverlap(a, b, a.Team == CatalogTeam.Ally ? 70 : 100,
                a.Team == CatalogTeam.Ally ? 80 : 70,
                b.Team == CatalogTeam.Ally ? 70 : 100,
                b.Team == CatalogTeam.Ally ? 80 : 70),
                "eight actors at one origin retain readable projected bodies");
        }
        foreach (CatalogBattleUnit unit in packedUnits)
            Check(unit.X >= -6.4f && unit.X <= 6.4f && unit.Y >= -2.35f && unit.Y <= 2.35f,
                "eight-actor crowd remains visible");
        for (int tick = 0; tick < 8 * 60 && !crowded.Ended; tick++)
        {
            crowded.Step(1f / 60f);
            if (cub1.IsAlive && cub2.IsAlive && !cub1.IsCharging && !cub2.IsCharging)
                Check(!BodyOverlap(cub1, cub2, 100, 70, 100, 70),
                    "converging minions remain substantially visible during combat");
            if (ally.IsAlive && elite.IsAlive && !elite.IsCharging)
                Check(!BodyOverlap(ally, elite, 70, 80, 200, 125),
                    "elite never hides most of a living ally between charges");
            foreach (CatalogBattleUnit unit in crowded.Units)
                Check(unit.X >= -6.4f && unit.X <= 6.4f && unit.Y >= -2.35f && unit.Y <= 2.35f,
                    "combat bodies stay inside the rendered viewport");
        }

        var bossBattle = new CatalogBattle(catalog);
        var boss = bossBattle.AddEnemy("beast-boss", 5000, 20, 0, 1.65f, 1.1f, .65f,
            true, 0, 0, CatalogEnemyRole.Beast, true);
        var escort = bossBattle.AddEnemy("beast-escort", 200, 10, 0, 1.8f, 1, .65f,
            false, 0, 0, CatalogEnemyRole.Beast);
        var bossAlly = bossBattle.AddUnit("boss-ally", CatalogTeam.Ally, "B-KN", null, null, 0, 0);
        bossBattle.Step(1f / 60f);
        Check(boss.X == 0 && boss.Y == 0 &&
            !BodyOverlap(boss, escort, 280, 210, 100, 70),
            "boss stays anchored while its living escort clears most of the boss body");
        Check(bossAlly.IsAlive, "boss and ally may share the contact area");

        var corpseBattle = new CatalogBattle(catalog);
        var corpse = corpseBattle.AddEnemy("beast-corpse", 100, 10, 0, 1.8f, 1, .65f,
            false, 0, 0, CatalogEnemyRole.Beast);
        corpse.Hp = 0;
        var living = corpseBattle.AddEnemy("beast-living", 100, 10, 0, 1.8f, 1, .65f,
            false, 0, 0, CatalogEnemyRole.Beast);
        corpseBattle.AddUnit("target", CatalogTeam.Ally, "B-KN", null, null, -4, 0);
        corpseBattle.Step(1f / 60f);
        Check(corpse.X == 0 && corpse.Y == 0 && Math.Abs(living.X) < .1f,
            "dead bodies do not push living units");

        var contact = new CatalogBattle(catalog);
        var knight = contact.AddUnit("knight", CatalogTeam.Ally, "B-KN", null, null, -1.5f, 0);
        contact.AddEnemy("beast", 110, 8, 0, 1.8f, 1, .65f,
            false, 1.5f, 0, CatalogEnemyRole.Beast);
        int hits = 0;
        contact.Event += e => { if (e.Kind == CatalogEventKind.BasicHit && e.SourceId == knight.Id) hits++; };
        for (int tick = 0; tick < 30 * 60 && !contact.Ended; tick++) contact.Step(1f / 60f);
        Check(contact.Ended && contact.Winner == CatalogTeam.Ally && hits > 0,
            "separated melee units can connect and complete combat");

        var distant = new CatalogBattle(catalog);
        distant.AddUnit("distant-knight", CatalogTeam.Ally, "B-KN", null, null, -4, 0);
        distant.AddEnemy("distant-beast", 110, 8, 0, 1.8f, 1, .65f,
            false, 4, 0, CatalogEnemyRole.Beast);
        int premature = 0;
        distant.Event += e => { if (e.Kind == CatalogEventKind.BasicHit) premature++; };
        distant.Step(.4f);
        Check(premature == 0, "body contact does not create remote melee hits");

        var lane = new CatalogBattle(catalog);
        lane.AddUnit("lane-knight", CatalogTeam.Ally, "B-KN", null, null, -1.3f, 0);
        lane.AddUnit("lane-archer", CatalogTeam.Ally, "B-AR", null, null, -3.7f, 1.8f);
        lane.AddUnit("lane-priest", CatalogTeam.Ally, "B-PR", null, null, -3.7f, -1.8f);
        lane.AddEnemy("elite-beast", 340, 38, 12, 1.8f, 1f, .7f,
            false, 3.25f, 0, CatalogEnemyRole.Beast);
        lane.AddEnemy("beast-1", 145, 22, 7, 1.8f, 1f, .65f,
            false, 2.1f, -.575f, CatalogEnemyRole.Beast);
        lane.AddEnemy("beast-2", 145, 22, 7, 1.8f, 1f, .65f,
            false, 2.65f, .575f, CatalogEnemyRole.Beast);
        lane.AddEnemy("spine-raider-1", 100, 25, 4, 2f, 3.6f, .55f,
            false, 3.2f, 1.2f, CatalogEnemyRole.Raider);
        int flankHits = 0, chargeHits = 0;
        lane.Event += e =>
        {
            if (e.Kind == CatalogEventKind.BasicHit && e.SourceId == "spine-raider-1") flankHits++;
            if (e.Kind == CatalogEventKind.ChargeHit && e.SourceId == "elite-beast") chargeHits++;
        };
        for (int tick = 0; tick < 8 * 60 && !lane.Ended; tick++) lane.Step(1f / 60f);
        Check(flankHits > 0 && chargeHits > 0,
            "rear ranged escort reaches a firing lane while an elite lands its first charge");
    }

    static void CatalogContainsThirtyForms()
    {
        Check(catalog.All.Count == 30, "Exactly 30 runtime forms exist.");
        Check(catalog.All.Count(f => f.Id.StartsWith("B-")) == 6 &&
            catalog.All.Count(f => f.Id.StartsWith("E-")) == 12 &&
            catalog.All.Count(f => f.Id.StartsWith("F-")) == 6 &&
            catalog.All.Count(f => f.Id.StartsWith("H-")) == 6, "Six base, twelve branch, six final, six hybrid.");
        Check(catalog.All.Select(f => f.Id).Distinct().Count() == 30, "All form IDs are unique.");
        Check(catalog.Get("B-KN").Hp == 240 && catalog.Get("H-06").Power == 47,
            "Runtime catalog keeps the base baseline and applies the documented hybrid candidate tune.");
    }

    static void GrowthAndCombination()
    {
        var roster = new CatalogRoster(catalog);
        Check(roster.Recruit("kn", "B-KN", -2, 1) && roster.Recruit("fi", "B-FI", -1, 1), "Recruit two owners.");
        for (int i = 0; i < 12; i++)
            Check(roster.Recruit("d" + i, i % 2 == 0 ? "B-PR" : "B-AR", i, 0), "Supply accepts fourteen originals.");
        Check(roster.Recruit("overflow", "B-KN", 0, 0) && roster.SupplyUsed == 15,
            "Gold-funded roster can retain a fifteenth cumulative recruit.");
        Check(roster.Transfer("d0", "kn", true) && roster.Transfer("d1", "kn", false) &&
            roster.Find("kn").Growth == 2 && roster.Find("kn").InheritedTraitId == "T-PR",
            "Two native-trait donors reach first evolution and preserve chosen inherited trait.");
        Check(!roster.Transfer("d2", "kn", false) && roster.Evolve("kn", "E-KN-G"),
            "Basic form cannot bank four later donors before branch evolution.");
        for (int i = 2; i < 6; i++) Check(roster.Transfer("d" + i, "kn", false), "Branch accepts four more donors.");
        Check(roster.Evolve("kn", "F-KN") && roster.Find("kn").BranchId == "E-KN-G" &&
            roster.Find("kn").Growth == 6 && roster.Find("kn").InheritedTraitId == "T-PR",
            "Final form keeps chosen branch, native family and inherited trait after six donors.");
        Check(!roster.Transfer("d6", "kn", false), "Finished form cannot consume donor without a trait change.");
        Check(roster.Transfer("d7", "kn", true) && roster.Find("kn").Growth == 6 &&
            roster.Find("kn").InheritedTraitId == "T-AR",
            "Finished form may replace inherited trait without more growth.");
        Check(roster.Undo() && roster.Find("d7") != null, "Undo restores consumed donor and previous trait.");

        var pair = new CatalogRoster(catalog);
        string[] ids = { "a", "b", "c", "d", "e", "f" };
        string[] forms = { "B-KN", "B-FI", "B-PR", "B-AR", "B-IC", "B-BA" };
        for (int i = 0; i < ids.Length; i++) pair.Recruit(ids[i], forms[i], i, i + 1);
        pair.Transfer("c", "a", true); pair.Transfer("d", "a", false); pair.Evolve("a", "E-KN-C");
        pair.Transfer("e", "b", true); pair.Transfer("f", "b", false); pair.Evolve("b", "E-FI-E");
        Check(pair.Combine("a", "b", "H-01") && pair.Members.Count == 1 &&
            pair.Find("a").FormId == "H-01" && pair.Find("a").X == 0 && pair.Find("a").Y == 1 &&
            pair.Find("a").InheritedTraitId == "T-PR" && pair.Find("a").BranchId == null &&
            pair.Find("a").Growth == 0, "Hybrid consumes two branches and retains anchor placement and one inherited trait.");
        Check(pair.Undo() && pair.Find("a").FormId == "E-KN-C" && pair.Find("b").FormId == "E-FI-E",
            "Combination Undo restores both donors and branch progress.");

        var keepSummon = new CatalogRoster(catalog);
        keepSummon.Recruit("receiver", "B-KN", 0, 0);
        keepSummon.Recruit("donor", "B-FI", 1, 0);
        keepSummon.Transfer("donor", "receiver", true);
        keepSummon.Recruit("later", "B-PR", 2, 0);
        Check(keepSummon.Undo() && keepSummon.Find("later") != null &&
            keepSummon.Find("donor") != null && keepSummon.SupplyUsed == 3,
            "Preparation Undo does not erase or re-roll a later summon.");
    }

    static void AllRecipesAndSupply()
    {
        Check(CatalogRoster.FitsSupply(1, 1, 0, 3) &&
            CatalogRoster.FitsSupply(0, 1, 1, 3) &&
            CatalogRoster.FitsSupply(0, 4, 0, 1) &&
            CatalogRoster.FitsSupply(0, 0, 2, 2),
            "The documented 13/12/13/14-original expedition compositions fit supply and slots.");
        Check(!CatalogRoster.FitsSupply(0, 0, 2, 3, 14) &&
            !CatalogRoster.FitsSupply(1, 0, 1, 3, 14) &&
            !CatalogRoster.FitsSupply(0, 0, 2, 4, 20, 5),
            "Fifteen/sixteen originals or six active slots are rejected.");
        Check(CatalogRoster.FitsSupply(0, 0, 2, 3),
            "Default supply estimate no longer caps a gold-funded expedition at fourteen.");
        string[][] recipes =
        {
            new[] { "H-01", "KN", "FI" }, new[] { "H-02", "KN", "PR" },
            new[] { "H-03", "AR", "IC" }, new[] { "H-04", "FI", "IC" },
            new[] { "H-05", "PR", "BA" }, new[] { "H-06", "AR", "BA" }
        };
        foreach (string[] recipe in recipes)
            for (int variant = 0; variant < 2; variant++)
            {
                var roster = new CatalogRoster(catalog);
                roster.Recruit("a", "B-" + recipe[1], 1, 2);
                roster.Recruit("b", "B-" + recipe[2], 3, 4);
                for (int donor = 0; donor < 4; donor++)
                    roster.Recruit("d" + donor, "B-IC", donor, 0);
                roster.Transfer("d0", "a", false); roster.Transfer("d1", "a", false);
                roster.Transfer("d2", "b", false); roster.Transfer("d3", "b", false);
                string aBranch = Branch(recipe[1], variant), bBranch = Branch(recipe[2], variant);
                Check(roster.Evolve("a", aBranch) && roster.Evolve("b", bBranch),
                    "Both variant branches can be made for " + recipe[0]);
                string anchor = variant == 0 ? "a" : "b";
                string other = variant == 0 ? "b" : "a";
                float originalX = roster.Find(anchor).X, originalY = roster.Find(anchor).Y;
                Check(roster.Combine(anchor, other, recipe[0]) && roster.Find(anchor).FormId == recipe[0] &&
                    roster.Find(anchor).X == originalX && roster.Find(anchor).Y == originalY &&
                    roster.Find(other) == null && roster.Find(anchor).Growth == 0,
                    "Recipe, branch variant and anchor order produce " + recipe[0]);
                Check(roster.Undo() && roster.Find("a") != null && roster.Find("b") != null,
                    "Fusion undo restores both selected branch forms.");
            }
    }

    static string Branch(string family, int variant)
    {
        switch (family)
        {
            case "KN": return variant == 0 ? "E-KN-G" : "E-KN-C";
            case "AR": return variant == 0 ? "E-AR-P" : "E-AR-T";
            case "FI": return variant == 0 ? "E-FI-B" : "E-FI-E";
            case "PR": return variant == 0 ? "E-PR-G" : "E-PR-H";
            case "IC": return variant == 0 ? "E-IC-F" : "E-IC-S";
            case "BA": return variant == 0 ? "E-BA-G" : "E-BA-A";
        }
        throw new ArgumentException("Unknown family.");
    }

    static void AllSkillsExecute()
    {
        foreach (CatalogForm form in catalog.All)
        {
            string branch = form.Id.StartsWith("F-") ? "E-" + form.Id.Substring(2) +
                (form.Id == "F-KN" || form.Id == "F-PR" || form.Id == "F-BA" ? "-G" :
                 form.Id == "F-AR" ? "-P" : form.Id == "F-FI" ? "-B" : "-F") : null;
            var battle = new CatalogBattle(catalog);
            var abilities = new HashSet<string>();
            battle.Event += ev => { if (ev.Kind == CatalogEventKind.SkillUsed) abilities.Add(ev.AbilityId); };
            var actor = battle.AddUnit("actor", CatalogTeam.Ally, form.Id, branch,
                form.Id == "E-KN-C" ? "T-PR" : null, 0, 0);
            actor.Hp = actor.MaxHp * .55f;
            var friend = battle.AddUnit("friend", CatalogTeam.Ally, "B-KN", null, null, -2.2f, 0);
            friend.Hp = friend.MaxHp * .5f;
            float threat = form.Id == "E-KN-C" ? 80f : 2f;
            float interval = form.Id == "E-KN-C" ? 1f : 5f;
            battle.AddEnemy("target", 50000f, threat, 0f, interval, 1f, .5f, false, 1f, 0);
            battle.AddEnemy("second", 50000f, 2f, 0f, 5f, 1f, .5f, false, 1.5f, .1f);
            for (int tick = 0; tick < 900; tick++) battle.Step(1f / 60f);
            if (form.Id == "E-KN-C")
                Check(abilities.Contains("E-KN-C"), "Conditional shield-break skill fires: " + form.Id);
            else Check(abilities.Contains(form.Id), "Unique skill fires: " + form.Id);
            if (branch != null) Check(abilities.Contains(branch), "Final form retains branch skill: " + form.Id);
        }
    }

    static void FirstHitAndDefense()
    {
        var battle = new CatalogBattle(catalog);
        var knight = battle.AddUnit("knight", CatalogTeam.Ally, "B-KN", null, null, 0, 0);
        battle.AddEnemy("striker", 10000, 100, 0, 100, 1, 1, false, 1, 0);
        for (int i = 0; i < 20; i++) battle.Step(1f / 60f);
        float expected = 240f - 100f * 100f / 135f * .8f;
        Check(Math.Abs(knight.Hp - expected) < .01f,
            "Defense 35 and first-hit Resolve 20% reduction apply to the same opening hit.");
    }

    static void ShieldBreakAndBurn()
    {
        var battle = new CatalogBattle(catalog);
        var counter = battle.AddUnit("counter", CatalogTeam.Ally, "E-KN-C", null, "T-PR", 0, 0);
        var enemy = battle.AddEnemy("striker", 10000, 50, 0, 1f, 1f, .5f, false, 1, 0);
        int counters = 0, blocks = 0;
        battle.Event += ev =>
        {
            if (ev.Kind == CatalogEventKind.SkillUsed && ev.AbilityId == "E-KN-C") counters++;
            if (ev.Kind == CatalogEventKind.ShieldBlocked && ev.TargetId == "counter") blocks++;
        };
        for (int i = 0; i < 180; i++) battle.Step(1f / 60f);
        Check(blocks > 0 && counters == 1 && enemy.Hp < 10000,
            "Damage-broken starting shield triggers one cooldown-limited cone counter.");
        var fire = new CatalogBattle(catalog);
        fire.AddUnit("fire", CatalogTeam.Ally, "B-FI", null, null, 0, 0);
        var dummy = fire.AddEnemy("dummy", 10000, 1, 0, 100, 1, .5f, false, 1, 0);
        int ticks = 0;
        fire.Event += ev => { if (ev.Kind == CatalogEventKind.Damage && ev.AbilityId == "BURN") ticks++; };
        for (int i = 0; i < 180; i++) fire.Step(1f / 60f);
        Check(ticks >= 1 && dummy.BurnRemaining > 0,
            "Fire's native basic attack applies a damage-dealing three-second burn.");
    }

    static void FinalBranchPriority()
    {
        var battle = new CatalogBattle(catalog);
        battle.AddUnit("final", CatalogTeam.Ally, "F-AR", "E-AR-P", null, 0, 0);
        battle.AddEnemy("dummy", 100000, 1, 0, 100, 1, .5f, false, 1, 0);
        var used = new List<string>();
        battle.Event += ev => { if (ev.Kind == CatalogEventKind.SkillUsed) used.Add(ev.AbilityId); };
        for (int i = 0; i < 200; i++) battle.Step(1f / 60f);
        Check(used.Count >= 2 && used[0] == "F-AR" && used[1] == "E-AR-P",
            "Final skill wins simultaneous readiness and branch follows on an independent cooldown.");
    }

    static void SeedDeterminism()
    {
        string first = BattleSignature(31), same = BattleSignature(31);
        Check(first == same, "Identical placement and target order produce identical event sequence and outcome.");
    }

    static string BattleSignature(int seed)
    {
        var random = new Random(seed);
        var battle = new CatalogBattle(catalog);
        string signature = "";
        battle.Event += ev => signature += ev.Kind + ":" + ev.SourceId + ":" + ev.TargetId + ":" + ev.AbilityId + ";";
        battle.AddUnit("a", CatalogTeam.Ally, "E-FI-B", null, null, -1, random.Next(3) * .2f);
        battle.AddUnit("b", CatalogTeam.Ally, "E-KN-G", null, null, -.5f, 0);
        battle.AddEnemy("e1", 900, 18, 15, 1.3f, 1, 1.1f, false, 2, random.Next(3) * .2f);
        battle.AddEnemy("e2", 900, 18, 15, 1.3f, 1, 1.1f, false, 2.4f, 0);
        for (int i = 0; i < 2400 && !battle.Ended; i++) battle.Step(1f / 60f);
        return signature + battle.Winner;
    }
}
