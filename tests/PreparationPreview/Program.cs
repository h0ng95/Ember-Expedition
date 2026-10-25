using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Expedition.Core;

internal static class PreparationPreviewTests
{
    static int checks;
    static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool condition, string reason)
    { checks++; if (!condition) throw new InvalidOperationException(reason); }
    static void Cast(CatalogBattle battle, CatalogBattleUnit actor, CatalogBattleUnit target, string skill)
    { typeof(CatalogBattle).GetMethod("ExecuteSkill", Hidden).Invoke(battle, new object[] { actor, target, skill }); }
    static bool Start(CatalogBattle battle, CatalogBattleUnit actor, string skill)
    { return (bool)typeof(CatalogBattle).GetMethod("StartSkill", Hidden).Invoke(battle, new object[] { actor, skill }); }
    static CatalogBattleUnit Ally(CatalogBattle battle, string id, string form, float x, float y = 0, string branch = null)
    { return battle.AddUnit(id, CatalogTeam.Ally, form, branch, null, x, y); }
    static CatalogBattleUnit Enemy(CatalogBattle battle, float x)
    { return battle.AddEnemy("enemy", 10000, 1, 0, 10, 1, .1f, false, x, 0); }

    static void Main(string[] args)
    {
        CatalogDatabase db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
        string[] finite = { "B-PR", "B-BA", "E-KN-G", "E-PR-G", "E-PR-H", "E-BA-G", "E-BA-A", "F-KN", "H-02", "H-05", "H-06" };
        Check(finite.All(id => {
            var p = CatalogPreparationPreview.ForSkill(id);
            return p.Reaches(-2, -1, -2 + p.Radius, -1) && !p.Reaches(-2, -1, -2 + p.Radius + .001f, -1);
        }), "All finite support ranges include their boundary and exclude a just-outside point.");
        var song = CatalogPreparationPreview.ForSkill("H-05");
        Check(song.Reaches(0, 0, 2.6f, 3.6f) && !song.Reaches(0, 0, 3.3f, 3.3f),
            "Candidate distance is two-dimensional, not an X-only or rectangular check.");
        Check(CatalogPreparationPreview.ForSkill("H-01") == null && CatalogPreparationPreview.ForSkill("E-KN-C") == null,
            "Self shields and offensive counters do not imply a nearby-ally support range.");

        var battle = new CatalogBattle(db);
        var singer = Ally(battle, "singer", "H-05", 0);
        var edge = Ally(battle, "edge", "B-AR", 4.5f);
        var outside = Ally(battle, "outside", "B-AR", 4.501f);
        edge.Hp = outside.Hp = 1;
        Cast(battle, singer, null, "H-05");
        Check(edge.Hp > 1 && edge.Buffs.Count > 0 && outside.Hp == 1 && outside.Buffs.Count == 0,
            "The real song heals and buffs the preview boundary, never the point immediately outside.");

        battle = new CatalogBattle(db);
        var healer = Ally(battle, "healer", "E-PR-H", 0);
        edge = Ally(battle, "edge", "B-AR", 4.8f);
        Check(CatalogPreparationPreview.ForSkill("E-PR-H").Reaches(0, 0, edge.X, edge.Y) && !Start(battle, healer, "E-PR-H"),
            "A full-health ally is a positional candidate, but the real heal waits for injury.");
        edge.Hp = 1;
        Cast(battle, healer, null, "E-PR-H");
        Check(edge.Hp > 1, "The real recovery skill reaches the 4.8-unit preview boundary.");

        battle = new CatalogBattle(db);
        var priest = Ally(battle, "priest", "B-PR", 0);
        var covered = Ally(battle, "covered", "B-KN", 1);
        edge = Ally(battle, "exposed", "B-AR", 4.5f);
        covered.Hp = 1; covered.Shield = covered.MaxHp * .25f;
        edge.Hp = edge.MaxHp * .4f;
        float protectedBefore = covered.Shield;
        Cast(battle, priest, null, "B-PR");
        Check(edge.Shield > 0 && covered.Shield == protectedBefore,
            "Shield recipient priority depends on existing coverage, not only the candidate's HP or distance.");

        battle = new CatalogBattle(db);
        var paladin = Ally(battle, "paladin", "H-02", 0);
        var second = Ally(battle, "second", "B-AR", 2.5f);
        var third = Ally(battle, "third", "B-AR", 1);
        paladin.Hp = 1; second.Hp = second.MaxHp * .2f; third.Hp = third.MaxHp * .4f;
        float thirdBefore = third.Hp;
        Cast(battle, paladin, Enemy(battle, 1), "H-02");
        Check(paladin.Hp > 1 && second.Hp > second.MaxHp * .2f && third.Hp == thirdBefore,
            "Paladin support is two total recipients including self, not self plus two.");

        battle = new CatalogBattle(db);
        var captain = Ally(battle, "captain", "H-06", 0);
        edge = Ally(battle, "edge", "B-AR", 3);
        var enemy = Enemy(battle, 100);
        bool waited = !Start(battle, captain, "H-06");
        enemy.X = 1;
        bool ready = Start(battle, captain, "H-06");
        Cast(battle, captain, enemy, "H-06");
        Check(waited && ready && edge.ExtraBasicMultiplier > 0,
            "Captain support candidates still require an enemy in attack contact before casting.");

        battle = new CatalogBattle(db);
        var finalPriest = Ally(battle, "final", "F-PR", 0, 0, "E-PR-H");
        var distant = Ally(battle, "distant", "B-AR", 100);
        distant.Hp = 1;
        Cast(battle, finalPriest, distant, "F-PR");
        Check(!CatalogPreparationPreview.ForSkill("E-PR-H").Reaches(0, 0, 100, 0) &&
            CatalogPreparationPreview.ForSkill("F-PR").Global && distant.Hp > 1 && distant.Shield > 0,
            "The final priest's global cast is independent of the retained branch's finite range.");

        battle = new CatalogBattle(db);
        var finalKnight = Ally(battle, "final", "F-KN", 0, 0, "E-KN-G");
        edge = Ally(battle, "edge", "B-AR", 2.25f);
        Cast(battle, finalKnight, null, "E-KN-G");
        bool branchExcluded = edge.Shield == 0;
        Cast(battle, finalKnight, null, "F-KN");
        Check(branchExcluded && edge.Shield > 0 &&
            !CatalogPreparationPreview.ForSkill("E-KN-G").Reaches(0, 0, 2.25f, 0) &&
            CatalogPreparationPreview.ForSkill("F-KN").Reaches(0, 0, 2.25f, 0),
            "The final knight's 2.5 support cast and 2.0 retained branch stay distinct in combat and preview.");
        Console.WriteLine("PASS: preparation preview, " + checks + " assertions.");
    }
}
