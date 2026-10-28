using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Web.Script.Serialization;
using Expedition.Core;

// Every anchor comes from a legal expedition. Isolated comparisons never feed rewards back.
static class BossGateProbe
{
    public sealed class Outcome { public bool win, timeout; public float seconds, health, bossHp; public int survivors; }
    public sealed class Anchor { public int seed, wave, gold; public string starter, policy, branch, spending, party; public string[] tactics; public Outcome current, compact, previous; }
    public sealed class RunResult { public int seed, wins, stoppedAt, attempts; public string starter, policy, branch, spending; public bool complete; }
    static CatalogDatabase db;
    static int fights;
    static readonly MethodInfo place=typeof(HybridBalanceProbe).GetMethod("Place",BindingFlags.NonPublic|BindingFlags.Static);
    static readonly MethodInfo setWave=typeof(ExpeditionRun).GetProperty("Wave").GetSetMethod(true);
    static Outcome Fight(CatalogBattle battle)
    {
        fights++;
        while(!battle.Ended && battle.Time<75f) battle.Step(battle.Time<74.75f?.25f:1f/60f);
        var allies=battle.Units.Where(u=>u.Team==CatalogTeam.Ally).ToArray();
        var boss=battle.Units.FirstOrDefault(u=>u.IsBoss);
        return new Outcome {win=battle.Ended&&battle.Winner==CatalogTeam.Ally,timeout=!battle.Ended,seconds=battle.Time,
            health=allies.Average(u=>Math.Max(0,u.Hp)/u.MaxHp),survivors=allies.Count(u=>u.IsAlive),bossHp=boss==null?0:Math.Max(0,boss.Hp)/boss.MaxHp};
    }
    static void Main(string[] args)
    {
        db=CatalogDatabase.FromJson(File.ReadAllText(args[0]));
        int first=int.Parse(args[2]),count=int.Parse(args[3]);
        if(first<1||count<1||count>30)throw new ArgumentException("sample range");
        var anchors=new List<Anchor>();var runs=new List<RunResult>();int actual=0;
        var serializer=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};
        string output=Path.GetDirectoryName(args[1]);Directory.CreateDirectory(output);
        HybridBalanceProbe.ConfigureGrowthOrder("Balanced");
        foreach(string starter in new[]{"standard","tactical","ember","frost"})
        foreach(var policy in HybridBalanceProbe.Policies)
        foreach(string branch in new[]{"defense","precision","arcana"})
        foreach(string spending in new[]{"early","delayed"})
        for(int seed=first;seed<first+count;seed++)
        {
            var run=ExpeditionRun.New(db,(uint)seed,startFormationId:starter);
            var progress=new HybridBalanceProbe.Progress();var seen=new HashSet<int>();int attempts=0;
            string[] path=branch=="defense"?new[]{"bulwark","shieldcraft","marksmanship"}:
                branch=="precision"?new[]{"marksmanship","exploitation","bulwark"}:new[]{"spellcraft","cinders","bulwark"};
            while(run.Phase==ExpeditionPhase.Preparation)
            {
                if(run.Wave>=(spending=="early"?6:11))foreach(string id in path)
                    if(run.CanPurchaseTactic(id)){run.PurchaseTactic(id);if(spending=="delayed")break;}
                HybridBalanceProbe.Prepare(run,policy,progress,run.Lantern==2?"compact":"spread",new List<string>());
                Anchor anchor=null;
                if((run.Wave==15||run.Wave==30)&&seen.Add(run.Wave))
                {
                    var snapshot=run.Capture();
                    anchor=new Anchor{seed=seed,wave=run.Wave,gold=run.Gold,starter=starter,policy=policy.Id,branch=branch,spending=spending,
                        party=HybridBalanceProbe.Party(run),tactics=run.TacticIds.ToArray()};
                    var compact=ExpeditionRun.Restore(db,snapshot);place.Invoke(null,new object[]{compact,"compact"});anchor.compact=Fight(compact.BeginBattle());
                    var before=ExpeditionRun.Restore(db,snapshot);
                    if(run.Wave==30){var old=before.Capture();old.Version=11;old.RulesVersion=3;before=ExpeditionRun.Restore(db,old);}
                    else setWave.Invoke(before,new object[]{14});
                    anchor.previous=Fight(before.BeginBattle());
                    string fixture=Path.Combine(output,"legal-wave-"+run.Wave+".json");
                    if(!File.Exists(fixture))File.WriteAllText(fixture,serializer.Serialize(snapshot));
                }
                Outcome result=Fight(run.BeginBattle());attempts++;actual++;
                if(anchor!=null){anchor.current=result;anchors.Add(anchor);}
                run.ResolveBattle(result.win);run.Continue();
            }
            runs.Add(new RunResult{seed=seed,starter=starter,policy=policy.Id,branch=branch,spending=spending,wins=run.Victories,
                stoppedAt=run.Victories==30?0:run.Wave,attempts=attempts,complete=run.Victories==30});
            if(runs.Count%84==0)Console.WriteLine("Progress "+runs.Count+" legal expeditions");
        }
        var report=new {firstSeed=first,seedsPerPolicy=count,rules=CatalogBalance.Version(CatalogBalance.CurrentRules),runs=runs,
            actualBattles=actual,isolatedBattles=fights-actual,
            note="Legal progression; first reached 15/30 preparation is the anchor. Same party compares compact placement; wave15 compares wave14 without extra growth, wave30 compares v048 melee dragon. Conditional samples and fixed policies are not human win rates.",anchors=anchors,
            summary=anchors.GroupBy(a=>a.wave).Select(g=>new{wave=g.Key,n=g.Count(),currentWins=g.Count(a=>a.current.win),compactWins=g.Count(a=>a.compact.win),previousWins=g.Count(a=>a.previous.win),timeouts=g.Count(a=>a.current.timeout),
                meanSeconds=g.Average(a=>a.current.seconds),meanBossHpOnTimeout=g.Where(a=>a.current.timeout).Select(a=>a.current.bossHp).DefaultIfEmpty().Average(),
                newlyLost=g.Count(a=>a.previous.win&&!a.current.win),newlyWon=g.Count(a=>!a.previous.win&&a.current.win)}).ToArray()};
        File.WriteAllText(args[1],serializer.Serialize(report));
        Console.WriteLine("PASS "+runs.Count+" legal expeditions, "+actual+" progression battles, "+(fights-actual)+" isolated comparisons.");
    }
}
