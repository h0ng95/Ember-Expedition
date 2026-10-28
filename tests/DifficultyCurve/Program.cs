using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using Expedition.Core;

// Live expeditions use legal preparation and stop when their lantern is exhausted.
// Adjacent-stage/boss fights are isolated comparisons with the same allies,
// placement, research and full health.
// It never grants currency, changes RNG, or advances the live expedition.
static class DifficultyCurveProbe
{
    sealed class Outcome { public bool win, timeout; public double seconds, health; }
    sealed class Pair { public int seed, wave; public string starter, policy, branch, party; public Outcome previous, current, next, boss, before, bossBefore; }
    static CatalogDatabase db;
    static readonly MethodInfo setWave=typeof(ExpeditionRun).GetProperty("Wave").GetSetMethod(true);
    static Outcome Fight(CatalogBattle battle)
    {
        while(!battle.Ended && battle.Time<75f)battle.Step(battle.Time<74.75f?.25f:1f/60f);
        return new Outcome {win=battle.Ended && battle.Winner==CatalogTeam.Ally,timeout=!battle.Ended,
            seconds=battle.Time,health=battle.Units.Where(u=>u.Team==CatalogTeam.Ally).Average(u=>Math.Max(0,u.Hp)/u.MaxHp)};
    }
    static void Main(string[] args)
    {
        db=CatalogDatabase.FromJson(File.ReadAllText(args[0]));
        int first=int.Parse(args[2]),count=int.Parse(args[3]);
        bool previousRules = args.Length > 4 && args[4] == "v046";
        if(first<1||count<1||count>40)throw new ArgumentException("Invalid sample size");
        HybridBalanceProbe.ConfigureGrowthOrder("Balanced");
        var pairs=new List<Pair>();int runs=0,completed=0,attempts=0,counterfactuals=0;
        foreach(string starter in new[]{"standard","tactical","ember","frost"})
        foreach(var policy in HybridBalanceProbe.Policies.Where(p=>p.Id=="final"||p.Id=="H-04"||p.Id=="H-05"))
        foreach(string branch in new[]{"defense","precision","arcana"})
        for(int seed=first;seed<first+count;seed++)
        {
            var run=ExpeditionRun.New(db,(uint)seed,startFormationId:starter);
            if (previousRules)
            { var old = run.Capture(); old.Version = 10; old.RulesVersion = 2; run = ExpeditionRun.Restore(db, old); }
            var progress=new HybridBalanceProbe.Progress();var seen=new HashSet<int>();
            string[] path=branch=="defense"?new[]{"bulwark","shieldcraft","marksmanship"}:
                branch=="precision"?new[]{"marksmanship","exploitation","bulwark"}:new[]{"spellcraft","cinders","bulwark"};
            while(run.Phase==ExpeditionPhase.Preparation)
            {
                if(run.Wave>=11)foreach(string id in path)if(run.CanPurchaseTactic(id)){run.PurchaseTactic(id);break;}
                HybridBalanceProbe.Prepare(run,policy,progress,run.Lantern==2?"compact":"spread",new List<string>());
                Pair pair=null;
                if(seen.Add(run.Wave)&&run.Wave>1)
                {
                    var fixedParty=ExpeditionRun.Restore(db,run.Capture());setWave.Invoke(fixedParty,new object[]{run.Wave-1});
                    pair=new Pair {seed=seed,wave=run.Wave,starter=starter,policy=policy.Id,branch=branch,
                        party=HybridBalanceProbe.Party(run),previous=Fight(fixedParty.BeginBattle())};
                    counterfactuals++;
                    if(run.Wave<30)
                    {
                        var next=ExpeditionRun.Restore(db,run.Capture());setWave.Invoke(next,new object[]{run.Wave+1});
                        pair.next=Fight(next.BeginBattle());counterfactuals++;
                    }
                    if(run.Wave%5==3)
                    {
                        var boss=ExpeditionRun.Restore(db,run.Capture());setWave.Invoke(boss,new object[]{run.Wave+2});
                        pair.boss=Fight(boss.BeginBattle());counterfactuals++;
                    }
                    if (args.Contains("compare") && run.RulesVersion >= 3 &&
                        new[]{11,12,13,14,17,18,20}.Contains(run.Wave))
                    {
                        var old = run.Capture(); old.Version = 10; old.RulesVersion = 2;
                        pair.before = Fight(ExpeditionRun.Restore(db, old).BeginBattle()); counterfactuals++;
                        if (run.Wave == 18)
                        {
                            var oldBoss = ExpeditionRun.Restore(db, old); setWave.Invoke(oldBoss,new object[]{20});
                            pair.bossBefore = Fight(oldBoss.BeginBattle()); counterfactuals++;
                        }
                    }
                }
                Outcome result=Fight(run.BeginBattle());attempts++;
                if(pair!=null){pair.current=result;pairs.Add(pair);}
                run.ResolveBattle(result.win);run.Continue();
            }
            runs++;if(run.Victories==30)completed++;
        }
        var encounters=new List<object>();
        for(int wave=1;wave<=30;wave++)
        {
            var probe=ExpeditionRun.New(db,1);
            if (previousRules) { var old = probe.Capture(); old.Version=10; old.RulesVersion=2; probe=ExpeditionRun.Restore(db,old); }
            setWave.Invoke(probe,new object[]{wave});
            var enemies=probe.BeginBattle().Units.Where(u=>u.Team==CatalogTeam.Enemy).ToArray();
            encounters.Add(new{wave=wave,count=enemies.Length,hp=enemies.Sum(u=>u.MaxHp),
                basicDps=enemies.Sum(u=>u.Power/u.AttackInterval),
                enemies=enemies.Select(u=>new{id=u.Id,hp=u.MaxHp,power=u.Power,x=u.X,y=u.Y,boss=u.IsBoss}).ToArray()});
        }
        var report=new {firstSeed=first,count=count,runs=runs,complete=completed,actualBattles=attempts,
            counterfactualBattles=counterfactuals,rules=CatalogBalance.Version(previousRules ? 2 : CatalogBalance.CurrentRules),
            note="Same-current-party adjacent-stage comparison, conditional on reaching the anchor stage. At the third stage of each region the next boss is also tested. Counterfactual fights do not count as legal expedition progress. Fixed delayed research policies, not human win rates.",
            rows=pairs.GroupBy(p=>p.wave).Select(g=>new{wave=g.Key,n=g.Count(),previousWins=g.Count(p=>p.previous.win),currentWins=g.Count(p=>p.current.win),
                easierWins=g.Count(p=>!p.previous.win&&p.current.win),harderWins=g.Count(p=>p.previous.win&&!p.current.win),
                previousHp=g.Average(p=>p.previous.health),currentHp=g.Average(p=>p.current.health),
                previousSeconds=g.Average(p=>p.previous.seconds),currentSeconds=g.Average(p=>p.current.seconds),
                nextWins=g.Count(p=>p.next!=null&&p.next.win),nextHp=g.Where(p=>p.next!=null).Select(p=>p.next.health).DefaultIfEmpty().Average(),
                nextSeconds=g.Where(p=>p.next!=null).Select(p=>p.next.seconds).DefaultIfEmpty().Average(),
                bossWins=g.Count(p=>p.boss!=null&&p.boss.win),bossHp=g.Where(p=>p.boss!=null).Select(p=>p.boss.health).DefaultIfEmpty().Average(),
                bossSeconds=g.Where(p=>p.boss!=null).Select(p=>p.boss.seconds).DefaultIfEmpty().Average(),
                compared=g.Count(p=>p.before!=null),beforeWins=g.Count(p=>p.before!=null&&p.before.win),
                beforeHp=g.Where(p=>p.before!=null).Select(p=>p.before.health).DefaultIfEmpty().Average(),
                beforeSeconds=g.Where(p=>p.before!=null).Select(p=>p.before.seconds).DefaultIfEmpty().Average(),
                bossBeforeWins=g.Count(p=>p.bossBefore!=null&&p.bossBefore.win),
                bossBeforeHp=g.Where(p=>p.bossBefore!=null).Select(p=>p.bossBefore.health).DefaultIfEmpty().Average()}).ToArray(),
            encounters=encounters,pairs=pairs};
        File.WriteAllText(args[1],new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Serialize(report));
        Console.WriteLine("PASS "+runs+" legal expeditions, "+attempts+" actual battles, "+counterfactuals+" isolated comparisons.");
    }
}
