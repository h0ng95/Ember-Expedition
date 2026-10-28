using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Expedition.Core;

static class DrakeSpacingTests
{
    static CatalogDatabase db;
    static int checks;
    static void Check(bool condition,string why){checks++;if(!condition)throw new Exception(why);}
    static float Distance(CatalogBattleUnit a,CatalogBattleUnit b)
    {float x=a.X-b.X,y=a.Y-b.Y;return (float)Math.Sqrt(x*x+y*y);}
    static CatalogBattle Fixture(int rules,float y,out CatalogBattleUnit boss,out CatalogBattleUnit ally)
    {
        var b=new CatalogBattle(db,null,null,rules);
        ally=b.AddUnit("ally",CatalogTeam.Ally,"B-AR",null,null,-3,y);
        ally.MoveSpeed=0;ally.Power=0;ally.Hp=ally.MaxHp=100000;
        boss=b.AddEnemy("palace-boss",100000,10,20,1.8f,1.1f,2f,true,3,0,CatalogEnemyRole.CrownDrake,true);
        boss.NextSlamAt=1000;
        return b;
    }
    static void Movement()
    {
        foreach(int rules in new[]{0,1,2,3,4,5})
        foreach(float y in new[]{-2f,0f,2f})
        foreach(float step in new[]{1f/60f,.25f})
        {
            CatalogBattleUnit boss,ally;var b=Fixture(rules,y,out boss,out ally);
            int basic=0;b.Event+=e=>{if(e.Kind==CatalogEventKind.BasicHit&&e.SourceId==boss.Id)basic++;};
            for(float elapsed=0;elapsed<12;elapsed+=step)b.Step(step);
            Check(Math.Abs(Distance(boss,ally)-(rules>=4?3.2f:1.1f))<.015f,"approach distance/legacy changed");
            Check(basic>0,"stopping farther away must not remove basic pressure");
            Check(boss.DrakeRangedBasic==(rules>=4),"firebolt enabled for legacy melee");
            float x=boss.X,by=boss.Y; b.Step(2);
            Check(Math.Abs(boss.X-x)<.001f&&Math.Abs(boss.Y-by)<.001f,"oscillating at stop distance");
            ally.X=boss.X-.6f;ally.Y=boss.Y;int before=basic;b.Step(2);
            Check(basic>before,"nearby ally must still receive melee attacks");
            Check(Math.Abs(boss.X-x)<.001f&&Math.Abs(boss.Y-by)<.001f,"boss must not flee a melee ally");
        }
        CatalogBattleUnit drake,front;var retarget=Fixture(4,0,out drake,out front);
        front.X=2.4f;
        var rear=retarget.AddUnit("rear",CatalogTeam.Ally,"B-AR",null,null,-3,1.7f);
        rear.MoveSpeed=0;rear.Power=0;rear.Hp=rear.MaxHp=100000;
        retarget.Step(1);front.Hp=0;retarget.Step(12);
        Check(Math.Abs(Distance(drake,rear)-3.2f)<.015f,"after tank dies boss crowds the backline");
        var ordinary=new CatalogBattle(db,null,null,4);
        var target=ordinary.AddUnit("target",CatalogTeam.Ally,"B-KN",null,null,-3,0);
        target.MoveSpeed=0;target.Power=0;target.Hp=target.MaxHp=100000;
        var enemy=ordinary.AddEnemy("other-boss",100000,10,20,1.8f,1.1f,2,true,3,0,CatalogEnemyRole.Golem,true);
        enemy.NextSlamAt=1000;ordinary.Step(8);
        Check(Math.Abs(Distance(enemy,target)-1.1f)<.015f,"unrelated bosses changed movement");
    }
    static void Patterns()
    {
        foreach(bool rage in new[]{false,true})
        {
            CatalogBattleUnit boss,ally;var b=Fixture(4,0,out boss,out ally);
            boss.NextSlamAt=2;boss.BossEnraged=rage;
            var events=new List<CatalogBattleEvent>();b.Event+=events.Add;
            float x=0,y=0,tx=0,ty=0;bool warned=false;
            for(int i=0;i<1800;i++)
            {
                b.Step(1f/60f);
                if(boss.BreathWarningRemaining>0)
                {
                    if(!warned){x=boss.X;y=boss.Y;tx=boss.BreathTargetX;ty=boss.BreathTargetY;warned=true;}
                    Check(boss.X==x&&boss.Y==y&&boss.BreathTargetX==tx&&boss.BreathTargetY==ty,"breath aim moves after warning");
                }
                else warned=false;
            }
            foreach(string id in new[]{"DRACONIC_BREATH","DRACONIC_BOMB","DRACONIC_LAND"})
                Check(events.Count(e=>e.Kind==CatalogEventKind.BossImpact&&e.AbilityId==id)>=2,"boss stalled while waiting: "+id);
            Check(events.Any(e=>e.Kind==CatalogEventKind.Damage&&e.AbilityId=="DRACONIC_BREATH"),"breath cannot reach retained distance");
            Check(events.Any(e=>e.Kind==CatalogEventKind.Damage&&e.AbilityId=="DRACONIC_BOMB"),"ranged party avoids all aerial pressure");
            Check(boss.Range==3.2f&&boss.BreathLength==5.5f&&boss.BreathHalfWidth==.65f,"authored attack ranges changed");
        }
    }
    static float Hit(int rules,string ability)
    {
        CatalogBattleUnit boss,ally;var battle=Fixture(rules,0,out boss,out ally);
        boss.X=0;boss.Y=0;ally.X=-1;ally.Y=0;ally.Shield=0;
        boss.PendingTargetId=ally.Id;boss.PendingSkillId=ability;
        boss.DrakeBreathOriginX=0;boss.DrakeBreathOriginY=0;boss.DrakeBreathEndX=-5;boss.DrakeBreathEndY=0;
        boss.SlamAimX=ally.X;boss.SlamAimY=ally.Y;boss.SlamRadiusValue=1.5f;
        float damage=0;battle.Event+=e=>{if(e.Kind==CatalogEventKind.Damage&&e.SourceId==boss.Id)damage+=e.Amount;};
        typeof(CatalogBattle).GetMethod(ability=="BASIC"?"ReleaseBasic":"ReleaseDrake",
            System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(battle,new object[]{boss});
        Check(boss.Power==10&&boss.AttackInterval==1.8f,"base skill power/cadence changed");
        return damage;
    }
    static void PressureAndHistory()
    {
        Check(Math.Abs(Hit(5,"BASIC")/Hit(4,"BASIC")-.9f)<.001f,"basic damage not 90 percent");
        foreach(string ability in new[]{"DRACONIC_BREATH","DRACONIC_BOMB","DRACONIC_LAND"})
            Check(Hit(5,ability)==Hit(4,ability)&&Hit(5,ability)>0,"pattern damage changed: "+ability);
        var history=new ExpeditionHistory();var json=new JavaScriptSerializer();
        foreach(int rules in new[]{4,5})
        {
            var save=ExpeditionRun.New(db,(uint)(52000+rules)).Capture();save.RulesVersion=rules;save.Version=rules+8;
            var run=ExpeditionRun.Restore(db,save);
            while(run.Wave<30){run.BeginBattle();run.ResolveBattle(true);run.Continue();}
            var before=run.Capture();var reload=ExpeditionRun.Restore(db,json.Deserialize<ExpeditionRunSave>(json.Serialize(before)));
            var boss=reload.BeginBattle().Get("palace-boss");
            Check(boss.BasicPowerScale==(rules==5?.9f:1f)&&boss.Range==3.2f,"continued run changed balance");
            reload.ResolveBattle(true);history.Observe(reload);
            if(rules==4)
            {
                var old=history.Capture();var record=old.ArchivedBests.Single();
                old.Version=4;old.HasBest=true;old.Best=record;old.ArchivedBests=new ExpeditionRecord[0];
                history=ExpeditionHistory.Restore(db,old);
                Check(history.Best==null&&history.ArchivedBests.Single().BalanceVersion=="normal-050","v050 best not archived");
            }
        }
        var restored=ExpeditionHistory.Restore(db,json.Deserialize<ExpeditionHistorySave>(json.Serialize(history.Capture())));
        Check(restored.Best.BalanceVersion=="normal-052"&&restored.ArchivedBests.Single().BalanceVersion=="normal-050","score revisions mixed");
    }
    static object Fight(ExpeditionRunSave source,int rules,int placement,float strength,bool removeTank)
    {
        var json=new JavaScriptSerializer();var save=json.Deserialize<ExpeditionRunSave>(json.Serialize(source));
        save.RulesVersion=rules;save.Version=rules+8;var run=ExpeditionRun.Restore(db,save);var b=run.BeginBattle();
        var allies=b.Units.Where(u=>u.Team==CatalogTeam.Ally).ToArray();
        float[][] layouts={new[]{-1.3f,0,-3.5f,1.7f,-3.5f,-1.7f,-4.6f,0,-2.4f,.8f},new[]{-1.3f,0,-2.8f,.6f,-2.8f,-.6f,-3.6f,0,-2,.4f},new[]{-3f,0,-4.5f,1.5f,-4.5f,-1.5f,-5.7f,0,-3.8f,.6f}};
        for(int i=0;i<allies.Length;i++){allies[i].X=layouts[placement][i*2];allies[i].Y=layouts[placement][i*2+1];allies[i].Power*=strength;allies[i].Hp=allies[i].MaxHp*=strength;if(removeTank&&allies[i].FormId=="F-KN")allies[i].Hp=0;}
        int basics=0,breaths=0,bombs=0;b.Event+=e=>{if(e.SourceId!="palace-boss")return;if(e.Kind==CatalogEventKind.BasicHit)basics++;if(e.Kind==CatalogEventKind.BossImpact){if(e.AbilityId=="DRACONIC_BREATH")breaths++;if(e.AbilityId=="DRACONIC_BOMB")bombs++;}};
        while(!b.Ended&&b.Time<75)b.Step(1f/60f);
        return new{rules,placement,strength,removeTank,win=b.Ended&&b.Winner==CatalogTeam.Ally,timeout=!b.Ended,seconds=b.Time,health=allies.Average(u=>u.Hp/u.MaxHp),basicHits=basics,breaths,bombs,bossX=b.Get("palace-boss").X};
    }
    static int Main(string[] args)
    {
        try
        {
            db=CatalogDatabase.FromJson(File.ReadAllText(args[0]));Movement();Patterns();PressureAndHistory();
            var json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};
            var account=json.Deserialize<ExpeditionAccountSave>(File.ReadAllText(args[1]));account.Validate(db);
            var outcomes=new List<object>();
            foreach(int placement in new[]{0,1,2})foreach(float strength in new[]{.8f,1f,1.2f})foreach(bool removeTank in new[]{false,true})foreach(int rules in new[]{3,4})outcomes.Add(Fight(account.Run,rules,placement,strength,removeTank));
            var current=ExpeditionRun.New(db,50001);Check(current.RulesVersion==5&&current.Capture().Version==13,"fresh version");
            var old=account.Run;Check(ExpeditionRun.Restore(db,old).RulesVersion==3,"old run silently changed");
            Directory.CreateDirectory(Path.GetDirectoryName(args[2]));File.WriteAllText(args[2],json.Serialize(new{checks,scenarios=outcomes.Count,note="Forced-progress QA roster; paired diagnostics, not player win rates.",outcomes}));
            Console.WriteLine("PASS drake spacing: "+checks+" checks; "+outcomes.Count+" paired encounter simulations.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
