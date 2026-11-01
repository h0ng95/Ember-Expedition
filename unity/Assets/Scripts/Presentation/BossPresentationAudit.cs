#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Explicit one-shot QA. Inflated health isolates animation; no account or run writes.
    public sealed class BossPresentationAudit : MonoBehaviour
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string[] Ids = {"forest-boss","golem-boss","furnace-boss","frost-boss","garden-boss","palace-boss"};
        static readonly CatalogEnemyRole[] Roles = { CatalogEnemyRole.Beast,CatalogEnemyRole.Golem,CatalogEnemyRole.Furnace,
            CatalogEnemyRole.FrostWarden,CatalogEnemyRole.GardenLord,CatalogEnemyRole.CrownDrake };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            string path=Path.Combine(Application.persistentDataPath,"boss-motion-audit.flag");
            if(!File.Exists(path))return;
            File.Delete(path); new GameObject("Explicit boss motion QA").AddComponent<BossPresentationAudit>();
        }
        IEnumerator Start()
        {
            var screen=FindFirstObjectByType<ExpeditionScreen>();
            float deadline=Time.realtimeSinceStartup+30;
            while(screen!=null && !(bool)typeof(ExpeditionScreen).GetField("startupReady",Hidden).GetValue(screen) &&
                Time.realtimeSinceStartup<deadline) yield return null;
            if(screen==null || !(bool)typeof(ExpeditionScreen).GetField("startupReady",Hidden).GetValue(screen))
            { Debug.LogError("BOSS_MOTION_AUDIT_FAIL startup"); Destroy(gameObject); yield break; }
            screen.enabled=false;
            bool originalMotion=GamePreferences.Current.ReduceMotion, originalFlashes=GamePreferences.Current.ReduceFlashes;
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            string directory=Path.Combine(Application.persistentDataPath,"BossMotionAudit"); Directory.CreateDirectory(directory);
            for(int quiet=0;quiet<2;quiet++) for(int speed=1;speed<=2;speed++) for(int region=0;region<6;region++)
            {
                GamePreferences.Current.ReduceMotion=quiet==1; GamePreferences.Current.ReduceFlashes=quiet==1;
                int wave=(region+1)*5; string name="boss-"+wave+"-"+speed+"x-"+(quiet==1?"quiet":"motion");
                bool capture=quiet==0 && speed==1;
                var battle=new CatalogBattle(db,null,null,CatalogBalance.CurrentRules);
                string[] forms={"B-KN","B-AR","B-PR"};
                for(int i=0;i<3;i++)
                {
                    var ally=battle.AddUnit("qa-"+i,CatalogTeam.Ally,forms[i],null,null,-1.2f-i*.8f,(i-1)*1.2f);
                    Set(ally,"Hp",10000f);Set(ally,"MaxHp",10000f);
                }
                var boss=battle.AddEnemy(Ids[region],100000,5,15,1.8f,1.4f,.55f,true,1.6f,0,Roles[region],true);
                Set(boss,"Hp",35000f);
                int basics=0,impacts=0,results=0,completed=0; var abilities=new HashSet<string>();
                battle.Event+=e=>{if(e.SourceId!=boss.Id)return;
                    if(e.Kind==CatalogEventKind.BasicHit)basics++;
                    if(e.Kind==CatalogEventKind.BossImpact){impacts++;abilities.Add(e.AbilityId);}};
                var go=new GameObject(name);var lab=go.AddComponent<CatalogCombatLab>();
                lab.OpenExpeditionBattle(battle,wave,30,"QA · "+wave+"전투 "+speed+"×",won=>{if(won)results++;},()=>{},()=>completed++);
                lab.ConfigureExpeditionSpeed(speed,null);
                object bossView=null;
                foreach(object view in (IList)typeof(CatalogCombatLab).GetField("combatViews",Hidden).GetValue(lab))
                    if(ReferenceEquals(view.GetType().GetField("Unit").GetValue(view),boss))bossView=view;
                var seen=new HashSet<int>(); var shots=new HashSet<int>();
                Debug.Log("BOSS_MOTION_AUDIT_BEGIN "+name);
                deadline=Time.realtimeSinceStartup+25;
                while(battle.Time<12f && Time.realtimeSinceStartup<deadline)
                {
                    yield return new WaitForEndOfFrame();
                    int frame=(int)bossView.GetType().GetField("RenderedEnemyFrame").GetValue(bossView);seen.Add(frame);
                    if(capture && (frame==13 || frame>=22 || frame==15 || frame==0) && shots.Add(frame))
                        Capture(directory,name+"-pose-"+frame);
                }
                Set(boss,"Hp",0);
                yield return null;yield return new WaitForEndOfFrame();
                if(capture)Capture(directory,name+"-fall");
                yield return new WaitForSecondsRealtime(4.8f);
                yield return new WaitForEndOfFrame();
                int settled=(int)bossView.GetType().GetField("RenderedEnemyFrame").GetValue(bossView);
                if(capture)Capture(directory,name+"-story");
                typeof(CatalogCombatLab).GetMethod("FinishDialogue",Hidden).Invoke(lab,null);
                bool good=basics>0 && impacts>0 && results==1 && completed==1;
                if(wave==15)good &= abilities.Contains("FURNACE_BLAST") && abilities.Contains("FURNACE_EMBER");
                if(wave==20 || wave==25) foreach(int frame in new[]{24,25,26,27})good &= seen.Contains(frame);
                if(wave==20)good &= settled==21;
                if(wave==25)good &= settled==19;
                if(wave==30)good &= abilities.Contains("DRACONIC_BREATH") && abilities.Contains("DRACONIC_BOMB") && abilities.Contains("DRACONIC_LAND");
                string detail=name+" basics="+basics+" impacts="+impacts+" frames="+string.Join(",",seen)+" settled="+settled+
                    " result="+results+" finish="+completed+" abilities="+string.Join(",",abilities);
                if(good)Debug.Log("BOSS_MOTION_AUDIT_PASS "+detail);else Debug.LogError("BOSS_MOTION_AUDIT_FAIL "+detail);
                Destroy(go);yield return null;
            }
            GamePreferences.Current.ReduceMotion=originalMotion;GamePreferences.Current.ReduceFlashes=originalFlashes;
            screen.enabled=true; Debug.Log("BOSS_MOTION_AUDIT_COMPLETE");Destroy(gameObject);
        }
        static void Set(CatalogBattleUnit unit,string property,float value)
        {typeof(CatalogBattleUnit).GetProperty(property).SetValue(unit,value,null);}
        static void Capture(string directory,string name)
        {
            var shot=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            shot.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);shot.Apply();
            File.WriteAllBytes(Path.Combine(directory,name+".rgb"),shot.GetRawTextureData());
            File.WriteAllText(Path.Combine(directory,"capture-size.txt"),Screen.width+","+Screen.height);Destroy(shot);
        }
    }
}
#endif
