using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Expedition.Core;
class BattleReview {
 static int checks,failures; static CatalogDatabase db;
 static object Call(CatalogBattle battle,string method,params object[] args){return typeof(CatalogBattle).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(battle,args);}
 static void Check(bool c,string m){checks++;Console.WriteLine((c?"PASS ":"FAIL ")+m);if(!c)failures++;}
 static CatalogBattle Battle(){return new CatalogBattle(db);}
 static CatalogBattleUnit Add(CatalogBattle b,string id,string form,string trait){return b.AddUnit(id,CatalogTeam.Ally,form,form.StartsWith("F-")?"E-"+form.Substring(2)+"-G":null,trait,0,0);}
 static CatalogBattleUnit Enemy(CatalogBattle b,string id,float x){return b.AddEnemy(id,10000,1,0,100,1,.001f,false,x,0);}
 static int Main(string[] args){db=CatalogDatabase.FromJson(File.ReadAllText(args[0]));
 var b=Battle();var k=Add(b,"k","B-KN",null);var e=Enemy(b,"e",1);k.Defense=25;k.Hp=k.MaxHp=1000;Call(b,"Damage",e,k,100f,"TEST",false,false);Check(Math.Abs(k.Hp-936)<.001,"Resolve applies to first damage after armor");
 b=Battle();k=Add(b,"k","B-KN",null);e=Enemy(b,"e",1);k.Defense=25;k.Hp=k.MaxHp=1000;Call(b,"GiveBuff",k,k,CatalogBuffKind.DamageReduction,.2f,3f,"TEST");Call(b,"Damage",e,k,100f,"TEST",false,false);Check(Math.Abs(k.Hp-948.8)<.001,"Independent DR stacks multiplicatively");
 b=Battle();var a=Add(b,"a","B-AR","T-H01");e=Enemy(b,"e",1);a.Power=100;a.PendingTargetId="e";e.MarkUntil=10;e.BurnTicksLeft=3;e.BurnUntil=10;Call(b,"ReleaseBasic",a);Check(Math.Abs(e.Hp-9865)<.001,"Basic damage bonuses add 15%+20%=35%");
 b=Battle();a=Add(b,"a","F-KN",null);var ally=Add(b,"ally","B-FI",null);ally.X=1;Call(b,"ExecuteSkill",a,null,"F-KN");Check(Math.Abs(a.Shield-a.MaxHp*.18f)<.001&&ally.Shield>0,"F-KN shields self plus other allies");
 b=Battle();a=Add(b,"a","H-05",null);ally=Add(b,"ally","B-FI",null);ally.X=1;a.Hp-=50;Call(b,"ExecuteSkill",a,null,"H-05");Check(a.Hp>a.MaxHp-50&&a.Buffs.Count>0,"H05 heals and buffs self");
 b=Battle();a=Add(b,"a","H-06",null);ally=Add(b,"ally","B-FI",null);ally.X=1;e=Enemy(b,"e",1);Call(b,"ExecuteSkill",a,e,"H-06");Check(a.ExtraBasicMultiplier==.6f&&ally.ExtraBasicMultiplier==.6f,"H06 buffs self next basic");
 b=Battle();a=Add(b,"a","E-PR-G","T-H05");ally=Add(b,"ally","B-FI",null);ally.X=1;ally.Hp=1;int triggers=0;b.Event+=delegate(CatalogBattleEvent ev){if(ev.Kind==CatalogEventKind.TraitTriggered&&ev.AbilityId=="T-H05")triggers++;};Call(b,"ExecuteSkill",a,null,"E-PR-G");Check(triggers==1,"Inherited harmony triggers on actual shield to another ally");
 b=Battle();a=Add(b,"a","E-PR-H",null);e=Enemy(b,"e",1);bool nullSafe=true;try{Call(b,"ExecuteSkill",a,null,"E-PR-H");}catch(TargetInvocationException){nullSafe=false;}Check(nullSafe,"Healing cast with no remaining injured target does not throw");
 b=Battle();a=Add(b,"a","B-PR",null);var far=Add(b,"far","B-FI",null);far.X=3;far.Hp=far.MaxHp*.5f;var near=Add(b,"near","B-FI",null);near.X=1;near.Hp=near.MaxHp*.5f;Check(((CatalogBattleUnit)Call(b,"LowestAlly",a,4.5f,false,true)).Id=="near","Equal HP target uses distance before spawn order");
 b=Battle();a=Add(b,"a","B-AR",null);e=Enemy(b,"e",1);e.Hp=5000;var n=Enemy(b,"near",.5f);n.Hp=5000;Check(((CatalogBattleUnit)Call(b,"ChooseEnemy",a,true,false)).Id=="near","Low-HP enemy ties use nearest");
 Console.WriteLine("BATTLE REVIEW "+checks+" checks; "+failures+" failures");
 return failures == 0 ? 0 : 1;
 }
}
