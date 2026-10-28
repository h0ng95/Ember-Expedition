using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Expedition.Core;

static class DrakePressureProbe
{
    sealed class Candidate
    {
        public string id; public float power, interval;
        public Candidate(string id,float power,float interval){this.id=id;this.power=power;this.interval=interval;}
    }
    sealed class Outcome
    {
        public string candidate; public bool win, timeout;
        public float seconds, health, bossHp, basicDamage, patternDamage;
        public int basics, breaths, bombs, lands;
    }
    sealed class Anchor
    {
        public int seed; public string starter, policy, branch, spending, party;
        public Outcome[] outcomes;
    }
    static readonly Candidate[] candidates = {
        new Candidate("current",1f,1.8f), new Candidate("power90",.9f,1.8f),
        new Candidate("power80",.8f,1.8f), new Candidate("interval21",1f,2.1f),
        new Candidate("interval24",1f,2.4f), new Candidate("power90_interval21",.9f,2.1f)
    };
    static int fights;
    static Outcome Fight(CatalogBattle battle,string candidate)
    {
        fights++;
        var result=new Outcome{candidate=candidate};
        battle.Event+=e=>{
            var source=battle.Get(e.SourceId);
            if(source==null||source.EnemyRole!=CatalogEnemyRole.CrownDrake)return;
            if(e.Kind==CatalogEventKind.BasicHit)result.basics++;
            if(e.Kind==CatalogEventKind.Damage){if(e.AbilityId=="BASIC")result.basicDamage+=e.Amount;else result.patternDamage+=e.Amount;}
            if(e.Kind==CatalogEventKind.BossImpact){if(e.AbilityId=="DRACONIC_BREATH")result.breaths++;if(e.AbilityId=="DRACONIC_BOMB")result.bombs++;if(e.AbilityId=="DRACONIC_LAND")result.lands++;}
        };
        while(!battle.Ended&&battle.Time<75f)battle.Step(battle.Time<74.75f?.25f:1f/60f);
        var allies=battle.Units.Where(u=>u.Team==CatalogTeam.Ally).ToArray();
        var boss=battle.Units.FirstOrDefault(u=>u.IsBoss);
        result.win=battle.Ended&&battle.Winner==CatalogTeam.Ally;result.timeout=!battle.Ended;result.seconds=battle.Time;
        result.health=allies.Average(u=>Math.Max(0,u.Hp)/u.MaxHp);result.bossHp=boss==null?0:Math.Max(0,boss.Hp)/boss.MaxHp;
        return result;
    }
    static void Main(string[] args)
    {
        var db=CatalogDatabase.FromJson(File.ReadAllText(args[0]));int first=int.Parse(args[2]),count=int.Parse(args[3]);
        if(first<1||count<1||count>30)throw new ArgumentException("seed range");
        var serializer=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};
        var anchors=new List<Anchor>();int routes=0,actual=0,completed=0;
        HybridBalanceProbe.ConfigureGrowthOrder("Balanced");
        foreach(string starter in new[]{"standard","tactical","ember","frost"})
        foreach(var policy in HybridBalanceProbe.Policies)
        foreach(string branch in new[]{"defense","precision","arcana"})
        foreach(string spending in new[]{"early","delayed"})
        for(int seed=first;seed<first+count;seed++)
        {
            // Freeze the route to v050; candidates never affect acquisition or who reaches the gate.
            var initial=ExpeditionRun.New(db,(uint)seed,startFormationId:starter).Capture();initial.Version=12;initial.RulesVersion=4;
            var run=ExpeditionRun.Restore(db,initial);var progress=new HybridBalanceProbe.Progress();bool seen=false;
            string[] path=branch=="defense"?new[]{"bulwark","shieldcraft","marksmanship"}:branch=="precision"?new[]{"marksmanship","exploitation","bulwark"}:new[]{"spellcraft","cinders","bulwark"};
            while(run.Phase==ExpeditionPhase.Preparation)
            {
                if(run.Wave>=(spending=="early"?6:11))foreach(string id in path)
                    if(run.CanPurchaseTactic(id)){run.PurchaseTactic(id);if(spending=="delayed")break;}
                HybridBalanceProbe.Prepare(run,policy,progress,run.Lantern==2?"compact":"spread",new List<string>());
                if(run.Wave==30&&!seen)
                {
                    seen=true;var snapshot=run.Capture();var results=new List<Outcome>();
                    foreach(var candidate in candidates)
                    {
                        var copy=ExpeditionRun.Restore(db,snapshot);var battle=copy.BeginBattle();
                        var drake=battle.Units.Single(u=>u.EnemyRole==CatalogEnemyRole.CrownDrake);
                        if(Math.Abs(drake.Power-145f)>.01f||Math.Abs(drake.Range-3.2f)>.01f)throw new Exception("unexpected dragon anchor stats");
                        drake.BasicPowerScale=candidate.power;drake.AttackInterval=candidate.interval;
                        results.Add(Fight(battle,candidate.id));
                    }
                    anchors.Add(new Anchor{seed=seed,starter=starter,policy=policy.Id,branch=branch,spending=spending,party=HybridBalanceProbe.Party(run),outcomes=results.ToArray()});
                }
                Outcome result=Fight(run.BeginBattle(),"route");actual++;run.ResolveBattle(result.win);run.Continue();
            }
            routes++;if(run.Victories==30)completed++;
            if(routes%168==0)Console.WriteLine("Progress "+routes+" legal routes, "+anchors.Count+" gate anchors");
        }
        var report=new{firstSeed=first,seedsPerPolicy=count,routes,completed,actualBattles=actual,isolatedBattles=fights-actual,
            note="Fixed v050 legal routes. First reached wave30; candidates share exact party, research and placement. Policies share seeds; not independent human samples. Damage includes shields. Basic scale changes only BASIC, not skill power.",
            candidates,anchors,summary=candidates.Select(c=>new{id=c.id,n=anchors.Count,wins=anchors.Count(a=>a.outcomes.Single(o=>o.candidate==c.id).win),
                lost=anchors.Count(a=>a.outcomes[0].win&&!a.outcomes.Single(o=>o.candidate==c.id).win),
                gained=anchors.Count(a=>!a.outcomes[0].win&&a.outcomes.Single(o=>o.candidate==c.id).win),
                timeouts=anchors.Count(a=>a.outcomes.Single(o=>o.candidate==c.id).timeout)}).ToArray()};
        File.WriteAllText(args[1],serializer.Serialize(report));Console.WriteLine(serializer.Serialize(report.summary));
        Console.WriteLine("PASS "+routes+" legal routes, "+actual+" progression battles, "+(fights-actual)+" isolated fights.");
    }
}
