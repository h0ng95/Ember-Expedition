using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using Expedition.Core;

static class ResearchRegionsTests
{
    static CatalogDatabase db;
    static int checks;
    static readonly JavaScriptSerializer json = new JavaScriptSerializer();
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static void Close(float a, float b, string why) { Check(Math.Abs(a-b) < .02f, why + " " + a + "/" + b); }
    static void Advance(ExpeditionRun run, int wins)
    { while (run.Victories < wins) { run.BeginBattle(); run.ResolveBattle(true); if (!run.HasEnded) run.Continue(); } }
    static ExpeditionRun Previous(uint seed)
    { var save = ExpeditionRun.New(db, seed, startFormationId: "frost").Capture(); save.Version = 9; save.RulesVersion = 1; return ExpeditionRun.Restore(db, save); }
    static ExpeditionHistory Reload(ExpeditionHistory history)
    { return ExpeditionHistory.Restore(db, json.Deserialize<ExpeditionHistorySave>(json.Serialize(history.Capture()))); }
    static void Gates()
    {
        foreach (string formation in new[] { "standard", "tactical", "ember", "frost" })
        for (uint seed = 46001; seed < 46021; seed++)
        {
            var run = ExpeditionRun.New(db, seed, startFormationId: formation);
            Advance(run, 4);
            Check(run.Gold >= 40 && !run.HasResearchRegion(CatalogResearch.Get("bulwark")), "rich but forest not cleared");
            int gold = run.Gold;
            foreach (var node in CatalogResearch.Nodes) Check(!run.PurchaseTactic(node.Id) && run.Gold == gold, "locked spending");
            run.BeginBattle(); run.ResolveBattle(false); run.Continue();
            Check(run.Wave == 5 && !run.CanPurchaseTactic("bulwark"), "boss loss unlocked research");
            Advance(run, 5);
            Check(run.HasResearchRegion(CatalogResearch.Get("bulwark")) && run.PurchaseTactic("bulwark"), "forest clear unlock");
            Check(!run.CanPurchaseTactic("shieldcraft"), "castle not cleared");
            Advance(run, 9); Check(!run.CanPurchaseTactic("shieldcraft"), "castle boss not defeated");
            run.BeginBattle(); run.ResolveBattle(false); run.Continue();
            Check(run.Lantern == 1 && run.TacticIds.Count == 1 && !run.CanPurchaseTactic("shieldcraft"), "retry reset research or unlock");
            Advance(run, 10); Check(run.PurchaseTactic("shieldcraft"), "castle clear advanced unlock");
            var restored = ExpeditionRun.Restore(db, json.Deserialize<ExpeditionRunSave>(json.Serialize(run.Capture())));
            Check(restored.TacticIds.SequenceEqual(run.TacticIds) && restored.HasResearchRegion(CatalogResearch.Get("shieldcraft")), "resume lost gate");
            Check(!ExpeditionRun.New(db, seed).HasResearchRegion(CatalogResearch.Get("bulwark")), "new run inherited clear");
        }
    }
    static void Damage()
    {
        var skill = typeof(CatalogBattle).GetMethod("ExecuteSkill", BindingFlags.Instance | BindingFlags.NonPublic);
        var hit = typeof(CatalogBattle).GetMethod("Damage", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (int rules in new[] {0,1,2})
        foreach (string id in new[] {"B-IC","E-IC-F","E-IC-S","F-IC"})
        {
            var battle = new CatalogBattle(db, null, null, rules);
            var actor = battle.AddUnit("a", CatalogTeam.Ally, id, id=="F-IC"?"E-IC-F":null, null, -1, 0);
            var target = battle.AddEnemy("e", 10000, 10, 0, 2, 1, .1f, false, 1, 0, CatalogEnemyRole.MineWorker);
            float hp = target.Hp; skill.Invoke(battle, new object[]{actor,target,id});
            Close(hp-target.Hp, actor.Power*CatalogBalance.FrostPower(id,rules), "frost actual skill");
            Check(CatalogUnitInfo.Skill(id,rules).Contains((CatalogBalance.FrostPower(id,rules)*100).ToString("0")+"%"), "skill text disagrees");
        }
        foreach (int rules in new[] {1,2})
        {
            var battle = new CatalogBattle(db, new[]{"spellcraft","cinders"}, null, rules);
            var actor = battle.AddUnit("a", CatalogTeam.Ally, "B-FI", null,null,-1,0);
            var target = battle.AddEnemy("e",10000,10,0,2,1,.1f,false,1,0,CatalogEnemyRole.MineWorker);
            foreach (string kind in new[]{"SKILL","BASIC","BURN"})
            {
                float hp=target.Hp;
                hit.Invoke(battle,new object[]{actor,target,100f,kind,kind=="BASIC",kind=="BURN"});
                Close(hp-target.Hp,kind=="SKILL"?(rules==2?130:115):kind=="BURN"?(rules==2?150:130):100,"research actual multiplier");
            }
        }
    }
    static void History()
    {
        var old=Previous(45001); Advance(old,30);
        Check(old.RulesVersion==1 && old.Capture().Version==9,"old run upgraded silently");
        var previousRecord=new ExpeditionRecord {SavedUtc=DateTime.UtcNow.ToString("o"),Score=333000,ScoreVersion=1,
            BalanceVersion="normal-045",FinalRun=old.Capture()};
        var history=ExpeditionHistory.Restore(db,new ExpeditionHistorySave {Version=1,Recent=new[]{previousRecord},Best=previousRecord,HasBest=true});
        Check(history.Best==null && history.ArchivedBest.Score==333000,"old best mixed with new");
        var current=ExpeditionRun.New(db,46001); current.BeginBattle();current.ResolveBattle(false);current.Continue();Advance(current,30);
        history.Observe(current);history=Reload(history);
        Check(history.Best.Score==332000 && history.ArchivedBest.Score==333000,"best must be partitioned by balance");
        Check(!history.Observe(current) && !history.Observe(old),"duplicate result");
        for(uint seed=1;seed<40;seed++) history.Observe(ExpeditionRun.New(db,seed),true);
        history=Reload(history);
        Check(history.Recent.Count==30 && history.Best.Score==332000 && history.ArchivedBest.Score==333000,"best evicted with recent history");
        var broken=history.Capture();broken.ArchivedBest.BalanceVersion="normal-046";
        bool rejected=false;try{ExpeditionHistory.Restore(db,broken);}catch(ArgumentException){rejected=true;}
        Check(rejected,"cross-version score tamper");
    }
    static int Main(string[] args)
    {try{db=CatalogDatabase.FromJson(File.ReadAllText(args[0]));Gates();Damage();History();Console.WriteLine("PASS research regions/balance versions: "+checks+" checks.");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
