using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using Expedition.Core;

static class DifficultyRulesTests
{
    static CatalogDatabase db;
    static int checks;
    static readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    static void Check(bool condition, string why) { checks++; if (!condition) throw new Exception(why); }
    static void Close(float a, float b, string why) { Check(Math.Abs(a-b)<.025f, why+": "+a+" / "+b); }
    static void Advance(ExpeditionRun run, int wins)
    { while(run.Victories<wins) {run.BeginBattle();run.ResolveBattle(true);if(!run.HasEnded)run.Continue();} }
    static ExpeditionRun Run(int rules)
    {
        var save = ExpeditionRun.New(db,(uint)(48001+rules)).Capture();
        save.RulesVersion=rules;save.Version=rules+8;
        return ExpeditionRun.Restore(db,save);
    }
    #pragma warning disable 0649 // Populated by the JSON serializer.
    public sealed class Enemy { public string id; public float hp,power,x,y; public bool boss; }
    public sealed class Encounter { public int wave,count; public Enemy[] enemies; }
    public sealed class Reference { public Encounter[] encounters; }
    #pragma warning restore 0649
    static void OldEncounters(string reference)
    {
        var rows=json.Deserialize<Reference>(File.ReadAllText(reference)).encounters;
        foreach(int rules in new[]{1,2})
        {
            var run=Run(rules);
            foreach(var row in rows)
            {
                var battle=run.BeginBattle();var enemies=battle.Units.Where(u=>u.Team==CatalogTeam.Enemy).ToArray();
                Check(enemies.Length==row.count,"old enemy count changed");
                foreach(var e in row.enemies)
                {
                    var unit=enemies.Single(u=>u.Id==e.id);
                    Close(unit.MaxHp,e.hp,"old HP");Close(unit.Power,e.power,"old power");
                    Close(unit.X,e.x,"old X");Close(unit.Y,e.y,"old Y");Check(unit.IsBoss==e.boss,"old role");
                }
                run.ResolveBattle(true);if(!run.HasEnded)run.Continue();
                var saved=run.Capture();
                run=ExpeditionRun.Restore(db,json.Deserialize<ExpeditionRunSave>(json.Serialize(saved)));
                Check(run.RulesVersion==rules && run.Capture().Version==rules+8,"resume silently upgraded rules");
            }
        }
        var current=Run(3);Advance(current,10);
        Check(current.EncounterDescription.Contains("채굴병 3"),"wave11 advice disagrees");
        Check(current.BeginBattle().Units.Count(u=>u.Team==CatalogTeam.Enemy)==4,"wave11 reinforcement");
        current.ResolveBattle(true);current.Continue();
        Check(current.EncounterDescription.Contains("채굴병 3"),"wave12 advice disagrees");
        Check(current.BeginBattle().Units.Count(u=>u.Team==CatalogTeam.Enemy)==5,"wave12 reinforcement");
    }
    static void Until(CatalogBattle b, Func<bool> condition, string why)
    { for(int i=0;i<1200&&!condition();i++)b.Step(1f/60f);Check(condition(),why); }
    static void Frost()
    {
        foreach(int rules in new[]{2,3})
        foreach(bool enraged in new[]{false,true})
        {
            var b=new CatalogBattle(db,null,null,rules);
            var front=b.AddUnit("front",CatalogTeam.Ally,"B-KN",null,null,-1,0);
            var rear=b.AddUnit("rear",CatalogTeam.Ally,"B-AR",null,null,-4,0);
            front.MoveSpeed=rear.MoveSpeed=0;front.Hp=front.MaxHp=rear.Hp=rear.MaxHp=3000;
            var boss=b.AddEnemy("frost-boss",10000,10,25,1.8f,1.1f,.01f,true,3,0,CatalogEnemyRole.FrostWarden,true);
            boss.BossEnraged=enraged;
            var events=new List<CatalogBattleEvent>();b.Event+=events.Add;
            Until(b,()=>boss.SlamWarningRemaining>0,"first frost warning");
            Check(boss.AttackTargetId==rear.Id && !boss.IsFrostFollowup,"first circle must target rear");
            float warning=b.Time;
            Until(b,()=>events.Any(e=>e.Kind==CatalogEventKind.BossImpact),"first frost impact");
            Check(b.Time-warning>=(enraged?.78f:1.08f),"no instant first hit");
            var hit=events.Single(e=>e.Kind==CatalogEventKind.Damage&&e.AbilityId=="FROST_NOVA");
            Close(hit.Amount,10*(rules==3?1.8f:1.5f)*100/(100+rear.Defense),"nova damage");
            Close(rear.FrozenRemaining,rules==3?.9f:1.1f,"freeze duration");
            if(rules==2) {Check(!boss.IsFrostFollowup,"legacy has new followup");continue;}
            Check(boss.IsFrostFollowup && boss.AttackTargetId==front.Id,"second circle must target different front ally");
            float x=boss.SlamTargetX,y=boss.SlamTargetY,time=b.Time;
            front.X=-8;front.Y=2;
            Until(b,()=>events.Count(e=>e.Kind==CatalogEventKind.BossImpact)==2,"second impact");
            Check(b.Time-time>=1.08f,"second impact skipped readable warning");
            Close(boss.SlamTargetX,x,"second warning tracked target X");Close(boss.SlamTargetY,y,"second warning tracked target Y");
            Check(!events.Any(e=>e.Kind==CatalogEventKind.Damage&&e.AbilityId=="FROST_NOVA"&&e.TargetId==front.Id),"out of circle damaged");
            Check(!boss.IsFrostFollowup && events.Count(e=>e.Kind==CatalogEventKind.BossTelegraph)==2,"unbounded frost chain");
            Close(boss.NextSlamAt-b.Time,enraged?5:7,"recovery between two-hit cycles");
            Until(b,()=>boss.IsFrostFollowup,"next cycle followup");
            typeof(CatalogBattle).GetMethod("Damage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(b,new object[]{rear,boss,100000f,"TEST",false,false});
            int impacts=events.Count(e=>e.Kind==CatalogEventKind.BossImpact);b.Step(2);
            Check(!boss.FrostFollowup && events.Count(e=>e.Kind==CatalogEventKind.BossImpact)==impacts,"dead boss followup fired");
        }
        var single=new CatalogBattle(db,null,null,3);
        var only=single.AddUnit("only",CatalogTeam.Ally,"B-KN",null,null,-4,0);only.MoveSpeed=0;only.Hp=only.MaxHp=3000;
        var warden=single.AddEnemy("frost-boss",10000,10,25,1.8f,1.1f,.01f,true,3,0,CatalogEnemyRole.FrostWarden,true);
        int count=0;single.Event+=e=>{if(e.Kind==CatalogEventKind.BossImpact)count++;};
        Until(single,()=>count==1,"solo frost impact");
        Check(!warden.IsFrostFollowup && warden.AttackPhase==CatalogAttackPhase.Ready,"single ally double targeted");
    }
    static ExpeditionRecord Record(int rules,int losses)
    {
        var run=Run(rules);
        for(int i=0;i<losses;i++){run.BeginBattle();run.ResolveBattle(false);run.Continue();}
        Advance(run,30);
        return new ExpeditionRecord{SavedUtc=DateTime.UtcNow.ToString("o"),Score=run.Score,ScoreVersion=1,
            BalanceVersion=CatalogBalance.Version(rules),FinalRun=run.Capture()};
    }
    static void History()
    {
        var a=Record(1,0);var b=Record(2,1);var c=Record(3,2);var d=Record(4,2);
        var old=new ExpeditionHistorySave{Version=2,Recent=new[]{b,a},Best=b,HasBest=true,ArchivedBest=a,HasArchivedBest=true};
        var history=ExpeditionHistory.Restore(db,json.Deserialize<ExpeditionHistorySave>(json.Serialize(old)));
        Check(history.Best==null && history.ArchivedBests.Count==2,"v2 best migration lost a version");
        history.Observe(ExpeditionRun.Restore(db,c.FinalRun));
        var v3=new ExpeditionHistorySave{Version=3,Recent=new[]{c,b,a},Best=c,HasBest=true,ArchivedBests=new[]{b,a}};
        history=ExpeditionHistory.Restore(db,json.Deserialize<ExpeditionHistorySave>(json.Serialize(v3)));
        Check(history.Best==null && history.ArchivedBests.Count==3,"v3 best migration lost a version");
        history.Observe(ExpeditionRun.Restore(db,d.FinalRun));
        for(uint seed=1;seed<40;seed++)history.Observe(ExpeditionRun.New(db,seed),true);
        history=ExpeditionHistory.Restore(db,json.Deserialize<ExpeditionHistorySave>(json.Serialize(history.Capture())));
        Check(history.Best.Score==331000 && history.Recent.Count==30,"current best lost");
        Check(history.ArchivedBests.Single(r=>r.BalanceVersion=="normal-045").Score==333000 &&
            history.ArchivedBests.Single(r=>r.BalanceVersion=="normal-046").Score==332000 &&
            history.ArchivedBests.Single(r=>r.BalanceVersion=="normal-048").Score==331000,"archived bests compared across versions");
        Check(history.Contains(a.FinalRun.RunId)&&history.Contains(b.FinalRun.RunId)&&history.Contains(c.FinalRun.RunId),"archived dedup lost");
        var broken=json.Deserialize<ExpeditionHistorySave>(json.Serialize(history.Capture()));
        broken.ArchivedBests=new[]{broken.ArchivedBests[0],broken.ArchivedBests[0]};
        bool rejected=false;try{ExpeditionHistory.Restore(db,broken);}catch(ArgumentException){rejected=true;}
        Check(rejected,"duplicate archived version accepted");
        var future=Run(3).Capture();future.Version=CatalogBalance.CurrentSaveVersion+1;
        rejected=false;try{ExpeditionRun.Restore(db,future);}catch(ArgumentException){rejected=true;}
        Check(rejected,"future run accepted");
        future=Run(2).Capture();future.RulesVersion=3;
        rejected=false;try{ExpeditionRun.Restore(db,future);}catch(ArgumentException){rejected=true;}
        Check(rejected,"rules/save mismatch accepted");
    }
    static int Main(string[] args)
    {try{db=CatalogDatabase.FromJson(File.ReadAllText(args[0]));OldEncounters(args[1]);Frost();History();Console.WriteLine("PASS difficulty curve rules: "+checks+" checks.");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
