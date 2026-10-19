using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Expedition.Core;
class EdgeReview {
static CatalogDatabase db; static int total,fail; static CatalogBattle B(){return new CatalogBattle(db);}
static object C(CatalogBattle b,string method,params object[] args){return typeof(CatalogBattle).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(b,args);}
static CatalogBattleUnit A(CatalogBattle b,string id,string form,string trait,float x=0,float y=0){return b.AddUnit(id,CatalogTeam.Ally,form,null,trait,x,y);}
static CatalogBattleUnit E(CatalogBattle b,string id="e",float x=1){var e=b.AddEnemy(id,100000,1,0,100,1,.5f,false,x,0);e.NextBasicAt=1000;return e;}
static void Check(bool v,string msg){total++;if(!v)fail++;Console.WriteLine((v?"PASS ":"FAIL ")+msg);}
static void Run(CatalogBattle b,float seconds){for(int i=0;i<(int)Math.Round(seconds*60);i++)b.Step(1f/60f);}
static int Main(string[] args){db=CatalogDatabase.FromJson(File.ReadAllText(args[0])); CatalogBattle b;CatalogBattleUnit a,e,f;int n;string source;
// Follow all three timed arrows and find any basic before the last.
b=B();a=A(b,"a","E-AR-T",null);e=E(b);a.NextPrimaryAt=1000;a.NextBasicAt=0;int arrows=0;bool overlap=false;b.Event+=ev=>{if(ev.SourceId=="a"&&ev.Kind==CatalogEventKind.Damage&&ev.AbilityId=="E-AR-T")arrows++;if(ev.SourceId=="a"&&ev.Kind==CatalogEventKind.BasicHit&&arrows<3)overlap=true;};C(b,"ExecuteSkill",a,e,"E-AR-T");Run(b,.4f);Check(arrows==3&&!overlap,"Volley completes before basic attack can proceed");
// Team symmetry for real catalog opponents.
b=B();a=A(b,"a","B-KN",null);a.Hp=a.MaxHp=100000;e=b.AddUnit("e",CatalogTeam.Enemy,"B-FI",null,null,1,0);n=0;b.Event+=ev=>{if(ev.SourceId=="e"&&ev.Kind==CatalogEventKind.SkillUsed)n++;};Run(b,4);Check(n>0,"Catalog enemy uses its own skill");
// Harmony is applicable to every own support skill, including H06 extra-basic commands.
b=B();a=A(b,"a","H-06","T-H05");f=A(b,"f","B-KN",null,.5f);e=E(b);n=0;b.Event+=ev=>{if(ev.Kind==CatalogEventKind.TraitTriggered&&ev.AbilityId=="T-H05")n++;};C(b,"ExecuteSkill",a,e,"H-06");Check(n==1,"H06 command triggers inherited Harmony when another ally receives buff");
// Trait healing is not an own support skill and must not recursively trigger Harmony.
b=B();a=A(b,"a","H-06","T-H05");f=A(b,"f","B-KN",null,.5f);f.Hp=1;e=E(b);e.Hp=1;n=0;b.Event+=ev=>{if(ev.Kind==CatalogEventKind.TraitTriggered&&ev.AbilityId=="T-H05")n++;};C(b,"Damage",a,e,10f,"BASIC",true,false);Check(n==0,"Spoils trait healing does not count as support skill for Harmony");
// Native Harmony and inherited Spoils are independent conditions and cooldowns.
b=B();a=A(b,"a","H-05","T-H06");f=A(b,"f","B-KN",null,.5f);e=E(b);C(b,"ExecuteSkill",a,null,"H-05");f.Hp=1;e.Hp=1;n=0;b.Event+=ev=>{if(ev.Kind==CatalogEventKind.TraitTriggered&&ev.AbilityId=="T-H06")n++;};C(b,"Damage",a,e,10f,"BASIC",true,false);Check(n==1,"Harmony cooldown does not block independent Spoils kill trigger");
// Shield actual absorption credited to provider and replacement does not stack.
b=B();a=A(b,"a","B-PR",null);f=A(b,"f","B-AR",null,.5f);e=E(b);source=null;float amount=0;b.Event+=ev=>{if(ev.Kind==CatalogEventKind.ShieldBlocked){source=ev.SourceId;amount=ev.Amount;}};C(b,"GiveShield",a,f,50f,4f,"B-PR");C(b,"GiveShield",a,f,40f,4f,"B-PR");C(b,"Damage",e,f,100f,"BASIC",true,false);Check(source=="a"&&Math.Abs(amount-50)<.001f,"Shield absorption credited to provider, weaker shields do not stack");
// Duplicate inherited/native trait does not duplicate first-hit protection.
b=B();a=A(b,"a","B-KN","T-KN");e=E(b);float before=a.Hp;C(b,"Damage",e,a,100f,"BASIC",true,false);Check(Math.Abs((before-a.Hp)-100f*100/135*.8f)<.001f,"Duplicate native/inherited trait applies once");
// Warmth modifies actual HP healing, not shield.
b=B();a=A(b,"a","B-PR",null);f=A(b,"f","H-02",null,.5f);f.Hp=10;C(b,"Heal",a,f,100f,"E-PR-H");C(b,"GiveShield",a,f,100f,4f,"E-PR-G");Check(Math.Abs(f.Hp-125)<.001f&&f.Shield==100,"Warmth increases heal 15 percent but not shield");
// Existing slow required at basic-hit start; duplicate on-hit at same impact cannot self-prime.
b=B();a=A(b,"a","H-03","T-IC");e=E(b);a.PendingTargetId=e.Id;C(b,"ReleaseBasic",a);bool first=e.MarkRemaining==0&&e.SlowPercent>0;C(b,"ReleaseBasic",a);Check(first&&e.MarkRemaining>0,"H03 on-hit slow only marks on the following hit");
// Melt states applied before its own direct skill, burn does not receive skill bonus.
b=B();a=A(b,"a","H-04",null);a.Power=100;e=E(b);float direct=0,periodic=0;b.Event+=ev=>{if(ev.Kind==CatalogEventKind.Damage&&ev.AbilityId=="H-04")direct+=ev.Amount;if(ev.Kind==CatalogEventKind.Damage&&ev.AbilityId=="BURN")periodic+=ev.Amount;};C(b,"ExecuteSkill",a,e,"H-04");a.NextBasicAt=a.NextPrimaryAt=1000;Run(b,1.1f);Check(Math.Abs(direct-230)<.01f&&Math.Abs(periodic-15)<.01f,"Melt applies own 15 percent direct bonus but not to burn tick");
// H06 one valid hit consumes command; an invalid out-of-range hit must preserve it.
b=B();a=A(b,"a","H-06",null);a.Power=100;e=E(b);C(b,"ExecuteSkill",a,e,"H-06");a.PendingTargetId=e.Id;e.X=100;C(b,"ReleaseBasic",a);bool kept=a.ExtraBasicMultiplier==.6f;e.X=1;float damage=0;b.Event+=ev=>{if(ev.Kind==CatalogEventKind.Damage&&ev.SourceId=="a"&&ev.AbilityId=="BASIC")damage+=ev.Amount;};C(b,"ReleaseBasic",a);C(b,"ReleaseBasic",a);Check(kept&&Math.Abs(damage-260)<.01f,"Command persists across invalid hit and affects exactly one valid basic");
// Counter shield expiry/replacement cannot count as break; damage kill cannot counter.
b=B();a=A(b,"a","E-KN-C",null);e=E(b);a.NextBasicAt=1000;n=0;b.Event+=ev=>{if(ev.Kind==CatalogEventKind.SkillUsed&&ev.AbilityId=="E-KN-C")n++;};C(b,"GiveShield",a,a,10f,.1f,"TEST");C(b,"GiveShield",a,a,20f,.1f,"TEST");Run(b,.2f);Check(n==0&&a.Shield==0,"Shield expiry/replacement never emits counter");C(b,"GiveShield",a,a,10f,4f,"TEST");a.Hp=1;C(b,"Damage",e,a,100f,"BASIC",true,false);Check(n==0&&!a.IsAlive,"Lethal shield break does not counter");
// Boss slow is halved, never scales attack interval, weaker refresh ignored.
b=B();a=A(b,"a","B-IC",null);e=b.AddEnemy("e",100000,1,0,2,1,1,true,1,0);C(b,"Slow",a,e,.4f,3f);C(b,"Slow",a,e,.2f,10f);Check(Math.Abs(e.SlowPercent-.2f)<.001f&&Math.Abs(e.SlowUntil-3)<.001f&&e.AttackInterval==2,"Boss slow halves movement only and ignores weaker refresh");
// Explicit self+2 must work even when three earlier allies share the same point.
b=B();var p1=A(b,"p1","B-KN",null);var p2=A(b,"p2","B-KN",null);var p3=A(b,"p3","B-KN",null);a=A(b,"a","H-05",null);a.Hp=1;C(b,"ExecuteSkill",a,null,"H-05");Check(a.Hp>1&&a.Buffs.Count>0&&p1.Buffs.Count>0&&p2.Buffs.Count>0&&p3.Buffs.Count==0,"H05 self plus exactly two others when all co-located");
b=B();p1=A(b,"p1","B-KN",null);p2=A(b,"p2","B-KN",null);p3=A(b,"p3","B-KN",null);a=A(b,"a","H-06",null);e=E(b);C(b,"ExecuteSkill",a,e,"H-06");Check(a.ExtraBasicMultiplier==.6f&&p1.ExtraBasicMultiplier==.6f&&p2.ExtraBasicMultiplier==.6f&&p3.ExtraBasicMultiplier==0,"H06 self plus exactly two others when all co-located");
Console.WriteLine("EDGE REVIEW "+total+" checks; "+fail+" failures");return fail==0?0:1;
}}
