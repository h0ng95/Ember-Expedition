using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Expedition.Core;

static class LateGamePressureProbe
{
    public sealed class Outcome { public bool win, timeout; public float seconds, health, enemyHp; public int alive; }
    public sealed class Anchor { public int seed,wave,supply,gold; public string starter,policy,research,growth,party; public Outcome current,previous,next,compact,spread; }
    public sealed class MatrixRow { public int wave,supply,gold; public string build,backline,research,layout; public Outcome result; }
    static CatalogDatabase db;
    static readonly MethodInfo waveSetter=typeof(ExpeditionRun).GetProperty("Wave").GetSetMethod(true);
    static readonly MethodInfo place=typeof(HybridBalanceProbe).GetMethod("Place",BindingFlags.Static|BindingFlags.NonPublic);
    static readonly string[][] research={
        new[]{"bulwark","shieldcraft","marksmanship"},new[]{"bulwark","shieldcraft","spellcraft"},
        new[]{"marksmanship","exploitation","bulwark"},new[]{"marksmanship","exploitation","spellcraft"},
        new[]{"spellcraft","cinders","bulwark"},new[]{"spellcraft","cinders","marksmanship"}};
    static readonly string[] researchNames={"defense-precision","defense-arcana","precision-defense","precision-arcana","arcana-defense","arcana-precision"};
    static readonly string[][] families={new[]{"KN","FI"},new[]{"KN","PR"},new[]{"AR","IC"},new[]{"FI","IC"},new[]{"BA","PR"},new[]{"AR","BA"}};
    static int actual, isolated, matrix, serial;
    static void Check(bool x,string why){if(!x)throw new Exception(why);}
    static Outcome Fight(CatalogBattle b)
    {
        while(!b.Ended&&b.Time<75f)b.Step(b.Time<74.75f?.25f:1f/60f);
        var allies=b.Units.Where(u=>u.Team==CatalogTeam.Ally).ToArray();
        return new Outcome {win=b.Ended&&b.Winner==CatalogTeam.Ally,timeout=!b.Ended,seconds=b.Time,
            health=allies.Average(u=>Math.Max(0,u.Hp)/u.MaxHp),alive=allies.Count(u=>u.IsAlive),enemyHp=b.Units.Where(u=>u.Team==CatalogTeam.Enemy).Sum(u=>Math.Max(0,u.Hp))};
    }
    static Outcome Replay(ExpeditionRun run,int wave,string layout=null)
    {
        var copy=ExpeditionRun.Restore(db,run.Capture());
        // A neighbouring-wave fixture is deliberately not legal progression and yields no rewards.
        waveSetter.Invoke(copy,new object[]{wave});
        if(layout!=null)place.Invoke(null,new object[]{copy,layout});
        isolated++;return Fight(copy.BeginBattle());
    }
    static string Branch(string family)
    {return family=="KN"?"E-KN-G":family=="AR"?"E-AR-P":family=="PR"?"E-PR-H":family=="FI"?"E-FI-B":family=="IC"?"E-IC-F":"E-BA-G";}
    static void Grow(CatalogRoster roster,string id,string family,int copies)
    {
        Check(roster.Recruit(id,"B-"+family,-3,0),"recruit");
        for(int i=1;i<copies;i++)
        {
            string donor="donor-"+(++serial);Check(roster.Recruit(donor,"B-KN",-5,0),"donor");
            Check(roster.Transfer(donor,id,false),"growth");
            if(i==2)Check(roster.Evolve(id,Branch(family)),"branch");
            if(i==6)Check(roster.Evolve(id,"F-"+family),"final");
        }
    }
    static CatalogRoster Team(string build,int backline,int supply,string layout)
    {
        var r=new CatalogRoster(db);
        if(build.StartsWith("H-"))
        {
            var recipe=families[int.Parse(build.Substring(2))-1];
            Grow(r,"anchor",recipe[0],3);Grow(r,"material",recipe[1],3);
            Check(r.Combine("anchor","material",build),"valid hybrid recipe");
        }
        else Grow(r,"anchor",build.Substring(2),7);
        Grow(r,"front","KN",7);
        Grow(r,"support",backline==2?"AR":"PR",supply>=27?7:3);
        Grow(r,"back-a",backline==0?"FI":backline==1?"AR":"PR",supply==35?7:3);
        Grow(r,"back-b",backline==0?"IC":"BA",supply==35?7:3);
        if(build.StartsWith("H-"))
        {
            // Spend the one-copy difference on a real trait transfer, not a growth
            // point below the next evolution threshold that has no immediate effect.
            Check(r.Recruit("trait-donor","B-BA",-5,0)&&r.Transfer("trait-donor","front",true),"one-copy hybrid saving buys a legal inherited trait");
        }
        Check(r.SupplyUsed==supply && r.Members.Count==5,"equal material and field investment");
        var ordered=r.Members.OrderBy(m=>db.Get(m.FormId).Range).ThenBy(m=>m.Id).ToArray();
        for(int i=0;i<ordered.Length;i++)
        {
            bool melee=db.Get(ordered[i].FormId).Range<=1.5f;
            float x=layout=="compact"?(melee?-1.65f:-2.65f):(melee?-1.25f:-4.5f);
            float y=layout=="compact"?(i-2)*.35f:(i-2)*1.0f;
            ordered[i].X=x;ordered[i].Y=y;
        }
        r.CommitPreparation();return r;
    }
    static void Main(string[] args)
    {
        db=CatalogDatabase.FromJson(File.ReadAllText(args[0]));string output=args[1];
        int first=int.Parse(args[2]),count=int.Parse(args[3]);Check(count>0&&count<=8,"seed count");
        bool controlledOnly=args.Length>4&&args[4]=="controlled";
        var json=new JavaScriptSerializer {MaxJsonLength=int.MaxValue};var anchors=new List<Anchor>();var routes=new List<object>();
        foreach(string growth in controlledOnly?new string[0]:new[]{"Balanced","OffenseBalanced"})
        foreach(string starter in new[]{"standard","tactical","ember","frost"})
        foreach(var policy in HybridBalanceProbe.Policies)
        foreach(int branch in new[]{0,2,4})
        for(int seed=first;seed<first+count;seed++)
        {
            HybridBalanceProbe.ConfigureGrowthOrder(growth);
            var run=ExpeditionRun.New(db,(uint)seed,startFormationId:starter);var progress=new HybridBalanceProbe.Progress();var seen=new HashSet<int>();
            while(run.Phase==ExpeditionPhase.Preparation)
            {
                if(run.Wave>=11)foreach(string id in research[branch])if(run.CanPurchaseTactic(id)){run.PurchaseTactic(id);break;}
                HybridBalanceProbe.Prepare(run,policy,progress,run.Lantern==2?"compact":"spread",new List<string>());
                Anchor row=null;var snapshot=run.Capture();
                if(run.Wave>=20&&seen.Add(run.Wave))
                {
                    row=new Anchor {seed=seed,wave=run.Wave,supply=run.Roster.SupplyUsed,gold=run.Gold,starter=starter,policy=policy.Id,
                        research=researchNames[branch],growth=growth,party=HybridBalanceProbe.Party(run)};
                    row.previous=Replay(run,run.Wave-1);if(run.Wave<30)row.next=Replay(run,run.Wave+1);
                    row.compact=Replay(run,run.Wave,"compact");row.spread=Replay(run,run.Wave,"spread");
                }
                var outcome=Fight(run.BeginBattle());actual++;
                if(row!=null)
                {
                    row.current=outcome;anchors.Add(row);
                    string name="legal-"+run.Wave+"-"+(outcome.timeout?"timeout":outcome.win?"win":"defeat")+".json";
                    if(!File.Exists(Path.Combine(output,name)))File.WriteAllText(Path.Combine(output,name),json.Serialize(snapshot));
                }
                run.ResolveBattle(outcome.win);run.Continue();
            }
            routes.Add(new {seed,starter,policy=policy.Id,research=researchNames[branch],growth,complete=run.Victories==30,victories=run.Victories,acquired=progress.AcquiredWave});
            if(routes.Count%84==0)Console.WriteLine("Legal routes "+routes.Count+", anchors "+anchors.Count);
        }
        if(!controlledOnly)File.WriteAllText(Path.Combine(output,"legal.json"),json.Serialize(new {rules=CatalogBalance.Version(6),actual,isolated,routes,anchors}));
        var rows=new List<MatrixRow>();
        foreach(string build in new[]{"F-AR","F-FI","F-IC","H-01","H-02","H-03","H-04","H-05","H-06"})
        {
            foreach(int back in new[]{0,1,2})foreach(int supply in new[]{23,27,35})foreach(int researchIndex in Enumerable.Range(0,6))
            foreach(string layout in new[]{"compact","spread"})
            {
                var roster=Team(build,back,supply,layout);
                Check(research[researchIndex].Sum(id=>CatalogResearch.Get(id).Cost)==140,"equal research spend");
                for(int wave=20;wave<=30;wave++)
                {
                    var encounter=ExpeditionRun.New(db,68001);waveSetter.Invoke(encounter,new object[]{wave});
                    var enemies=encounter.BeginBattle().Units.Where(u=>u.Team==CatalogTeam.Enemy).ToArray();
                    var battle=new CatalogBattle(db,research[researchIndex],null,6);
                    foreach(var m in roster.Members)battle.AddUnit(m.Id,CatalogTeam.Ally,m.FormId,m.BranchId,m.InheritedTraitId,m.X,m.Y);
                    foreach(var e in enemies)battle.AddEnemy(e.Id,e.MaxHp,e.Power,e.Defense,e.AttackInterval,e.Range,e.MoveSpeed,e.IsBoss,e.X,e.Y,e.EnemyRole,e.BossPatternsEnabled);
                    rows.Add(new MatrixRow{wave=wave,supply=supply,gold=(supply-3)*20+140,build=build,backline=back.ToString(),research=researchNames[researchIndex],layout=layout,result=Fight(battle)});matrix++;
                }
            }
            Console.WriteLine("Controlled equal-investment build "+build+", fights "+matrix);
        }
        File.WriteAllText(Path.Combine(output,"controlled.json"),json.Serialize(new {rules=CatalogBalance.Version(6),matrix,note="Controlled material and research budget, not random acquisition odds or human win rates. 23/27/35 recruited copies incl. consumed growth/trait materials; five deployed units; 140G research. Hybrids spend the one-copy difference on a T-BA transfer to the frontline. No permanent bonuses. Branch choices are fixed; sample is not exhaustive.",rows}));
        Console.WriteLine("PASS "+routes.Count+" legal routes, "+actual+" real fights, "+isolated+" isolated replays, "+matrix+" controlled fights.");
    }
}
