using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class AllyCombatPresentationValidation
    {
        static int checks;
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Ally combat: "+why);}
        static object Field(object o,string n){return o.GetType().GetField(n,BindingFlags.Public|Private).GetValue(o);}
        static void Set(object o,string n,object v){o.GetType().GetField(n,BindingFlags.Public|Private).SetValue(o,v);}
        static void Prop(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
        static void Call(object o,string n,params object[] args){o.GetType().GetMethod(n,Private).Invoke(o,args);}
        [Serializable] sealed class Row { public string form;public int speed,fps,basic,flightFrames;public bool projectile; }
        [Serializable] sealed class Report { public List<Row> cases=new List<Row>();public int checks; }
        public static void ValidateAndBuild(){Validate();PrototypeSetup.BuildAndroid();}
        public static void Validate()
        {
            checks=0;var report=new Report();
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            foreach(var group in CombatPresentationAudit.Groups)foreach(string form in group)
            foreach(int speed in new[]{1,2})foreach(int fps in new[]{30,60})
            {
                var go=new GameObject("Ally timing validation");
                try
                {
                    var battle=new CatalogBattle(db);
                    var actor=battle.AddUnit("actor",CatalogTeam.Ally,form,CombatPresentationAudit.Branch(form),null,0,0);
                    var target=battle.AddEnemy("target",100000,1,0,100,1,.01f,false,1,0);
                    Set(actor,"NextPrimaryAt",10000f);Set(actor,"NextFinalAt",10000f);Set(target,"NextBasicAt",10000f);
                    var lab=go.AddComponent<CatalogCombatLab>();lab.OpenExpeditionBattle(battle,15,30,"QA",_=>{},()=>{});
                    var views=(IList)Field(lab,"combatViews");object view=views[0];
                    var row=new Row{form=form,speed=speed,fps=fps,projectile=AllyAttackPresentation.Projectile(actor)};
                    bool impact=false;
                    battle.Event+=e=>{
                        if(e.SourceId==actor.Id && e.Kind==CatalogEventKind.BasicHit){row.basic++;impact=true;}
                        if(e.Kind==CatalogEventKind.Damage && e.SourceId==actor.Id && e.AbilityId=="BASIC")
                        {
                            Check(((IList)Field(lab,"floating")).Count>0,"damage number on simulation impact "+form);
                            Check((float)Field(view,"ReleaseAge")== (row.projectile?AllyAttackPresentation.ProjectileFlight:0f),"arrival pose "+form);
                        }
                    };
                    for(int frame=0;frame<fps*4/speed;frame++)
                    {
                        impact=false;
                        for(int tick=0;tick<60/fps*speed;tick++)battle.Step(1f/60f);
                        Call(lab,"AdvanceActors",speed/(float)fps,1f/fps);
                        if(AllyAttackPresentation.InFlight(actor))
                        {
                            row.flightFrames++;
                            Check(AllyAttackPresentation.AttackFrame(actor,(float)Field(view,"WindupAge"),
                                (float)Field(view,"ReleaseAge"),(float)Field(view,"ReleaseRemaining"))>=3,"projectile leaves in firing pose "+form);
                        }
                        if(impact)
                        {
                            int f=(int)typeof(CatalogCombatLab).GetMethod("BattleAnimationFrame",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new[]{view});
                            Check(f==(row.projectile?5:3),"first painted arrival frame at "+speed+"x/"+fps+"fps "+form);
                        }
                    }
                    Check(row.basic>0 && (!row.projectile || row.flightFrames>0),"basic and projectile exercised "+form);
                    report.cases.Add(row);
                }
                finally{UnityEngine.Object.DestroyImmediate(go);}
            }
            Check(AllyAttackPresentation.State(true,false,true,true,false)==2,"hit cannot hide attack");
            Check(AllyAttackPresentation.State(true,true,true,true,false)==0,"freeze interrupts attack");
            Check(AllyAttackPresentation.State(false,false,true,true,false)==4,"death interrupts attack");
            Rect body=new Rect(0,0,60,80);
            Check(BattleVisibility.CoveredFraction(body,new[]{new Rect(0,0,30,80),new Rect(30,0,30,80)})==1,"union of two blockers");
            Check(BattleVisibility.CoveredFraction(body,new[]{new Rect(0,0,15,80),new Rect(0,0,15,80)})<.6f,"overlapping blockers cannot double count");
            Check(BattleVisibility.CoveredFraction(body,new Rect[0])==0,"clear body has no outline");
            Check(Resources.Load<Shader>("Shaders/CatalogAllyOutline")!=null,"outline available in player resources");
            BattleVisibilityValidation.Validate();BattleReadabilityValidation.Validate();AllyBodyScaleValidation.Validate();
            report.checks=checks;Directory.CreateDirectory("Logs/v058");
            File.WriteAllText("Logs/v058/timing-validation.json",JsonUtility.ToJson(report,true));
            Debug.Log("PASS ally combat presentation: "+checks+" checks,120 cases (30 forms x 1x/2x x30/60fps).");
        }
    }
}
