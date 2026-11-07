using System;
using System.Collections;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class BattleNumberValidation
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Field(object obj,string name){return obj.GetType().GetField(name,Hidden|BindingFlags.Public).GetValue(obj);}
        static void Check(bool ok,string why){if(!ok)throw new Exception("Battle numbers: "+why);}
        public static void Validate()
        {
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var battle=new CatalogBattle(db);
            var ally=battle.AddUnit("ally",CatalogTeam.Ally,"B-KN",null,null,0,0);
            for(int i=0;i<10;i++)battle.AddEnemy("target"+i,1000,1,0,5,1,.01f,false,3,i*.1f);
            var go=new GameObject("Numeric clutter validation");
            try
            {
                var lab=go.AddComponent<CatalogCombatLab>();lab.OpenExpeditionBattle(battle,5,30,"QA",_=>{},()=>{});
                var floats=(IList)Field(lab,"floating");
                Action<CatalogEventKind,string,string,float> emit=(kind,id,ability,amount)=>{
                    var e=new CatalogBattleEvent();
                    foreach(var entry in new[]{new object[]{"Kind",kind},new object[]{"SourceId","ally"},
                        new object[]{"TargetId",id},new object[]{"AbilityId",ability},new object[]{"Amount",amount},new object[]{"Time",1f}})
                        typeof(CatalogBattleEvent).GetProperty((string)entry[0]).SetValue(e,entry[1]);
                    typeof(CatalogCombatLab).GetMethod("OnBattleEvent",Hidden).Invoke(lab,new object[]{e});
                };
                foreach(string ability in new[]{"BASIC","BURN","POISON"})
                    for(int i=0;i<20;i++)emit(CatalogEventKind.Damage,"target0",ability,10);
                emit(CatalogEventKind.Shield,"ally","B-PR",40);
                Check(floats.Count==0,"basic, periodic and shield values stay out of floating text");
                for(int i=0;i<6;i++)emit(CatalogEventKind.Damage,"target0","E-KN-C",25);
                Check(floats.Count==1 && (float)Field(floats[0],"Amount")==150f,"skill volley merges exactly");
                floats.Clear();
                typeof(CatalogBattleUnit).GetProperty("MaxHp").SetValue(ally,1000f);
                typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(ally,800f);
                emit(CatalogEventKind.Heal,"ally","B-PR",100);
                Check(floats.Count==0,"routine healing remains visible through FX/HP only");
                typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(ally,400f);
                emit(CatalogEventKind.Heal,"ally","B-PR",150);emit(CatalogEventKind.Heal,"ally","B-PR",150);
                Check(floats.Count==1 && (float)Field(floats[0],"Amount")==300f,"low-health recovery is merged");
                floats.Clear();
                for(int i=0;i<20;i++)emit(CatalogEventKind.Poison,"target0","GARDEN_BRIARS",3);
                Check(floats.Count==1,"reapplied status is not repeated every hit");
                for(int i=0;i<10;i++)emit(CatalogEventKind.Freeze,"target"+i,"FROST_NOVA",2);
                Check(floats.Count==8,"crowded field capped at eight callouts");
                emit(CatalogEventKind.Damage,"target0","E-KN-C",100);
                Check(floats.Count==8,"numbers cannot evict an all-status screen");
                foreach(object f in floats)Check((CatalogCombatLab.BattleFloatKind)Field(f,"Kind")==CatalogCombatLab.BattleFloatKind.Freeze,
                    "freeze information kept over numeric detail");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
            Debug.Log("BATTLE_NUMBERS_VALIDATION_PASS: tick suppression, exact skill/heal merge, low-health recovery, status priority, eight-label bound.");
        }
    }
}
