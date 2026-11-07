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
    // Opt-in local QA; no account, run or preference persistence.
    public sealed class CombatEffectsAudit : MonoBehaviour
    {
        static readonly string[] BossIds={"forest-boss","golem-boss","furnace-boss","frost-boss","garden-boss","palace-boss"};
        static readonly CatalogEnemyRole[] Roles={CatalogEnemyRole.Beast,CatalogEnemyRole.Golem,CatalogEnemyRole.Furnace,
            CatalogEnemyRole.FrostWarden,CatalogEnemyRole.GardenLord,CatalogEnemyRole.CrownDrake};
        [Serializable] public sealed class Row
        {public string name;public int damage,blocks,impacts,flightFrames,visibleLabelsPeak;public List<string> abilities=new List<string>();}
        [Serializable] public sealed class Report {public bool complete;public List<Row> cases=new List<Row>();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            string flag=Path.Combine(Application.persistentDataPath,"combat-effects-audit.flag");
            if(!File.Exists(flag))return;File.Delete(flag);
            new GameObject("Explicit combat effects QA").AddComponent<CombatEffectsAudit>();
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(5);
            var screen=FindFirstObjectByType<ExpeditionScreen>();if(screen==null)yield break;
            bool wasEnabled=screen.enabled;screen.enabled=false;
            bool motion=GamePreferences.Current.ReduceMotion,flashes=GamePreferences.Current.ReduceFlashes;
            string dir=Path.Combine(Application.persistentDataPath,"CombatEffectsAudit");Directory.CreateDirectory(dir);
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var report=new Report();GameObject go=null;
            try
            {
                // Six bosses plus one quiet repeat. Both 1x and 2x are rendered on-device.
                for(int c=0;c<7;c++)
                {
                    int region=c%6,speed=c==0 || c==3 ? 1 : 2;
                    GamePreferences.Current.ReduceMotion=c==6;GamePreferences.Current.ReduceFlashes=c==6;
                    var row=new Row{name="boss-"+((region+1)*5)+"-"+speed+"x"+(c==6?"-reduced":"")};
                    var battle=new CatalogBattle(db);
                    string[] forms=c==0 ? new[]{"B-KN","H-02","B-AR","B-FI","B-IC"} :
                        new[]{"E-KN-G","E-BA-G","B-AR","B-FI","F-PR"};
                    for(int i=0;i<5;i++)
                    {
                        var ally=battle.AddUnit("qa-"+i,CatalogTeam.Ally,forms[i],forms[i]=="F-PR"?"E-PR-H":null,null,-1.8f-i*.4f,(i-2)*.6f);
                        typeof(CatalogBattleUnit).GetProperty("MaxHp").SetValue(ally,10000f);
                        typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(ally,8000f);
                    }
                    var boss=battle.AddEnemy(BossIds[region],100000,18,15,1.8f,1.4f,.55f,true,1.8f,0,Roles[region],true);
                    typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(boss,35000f);
                    // Exercise enemy leftward projectile orientation as well as allied rightward flight.
                    battle.AddEnemy("garden-spitter-qa",100000,8,8,1.6f,4.5f,.4f,false,3f,1.2f,CatalogEnemyRole.GardenSpitter);
                    go=new GameObject(row.name);var lab=go.AddComponent<CatalogCombatLab>();
                    lab.OpenExpeditionBattle(battle,(region+1)*5,30,"FX QA · "+row.name,_=>{},()=>{});lab.ConfigureExpeditionSpeed(speed,null);
                    var labels=(IList)typeof(CatalogCombatLab).GetField("floating",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(lab);
                    string pending=null;float due=0;var shots=new HashSet<string>();
                    battle.Event+=e=>{
                        if(e.Kind==CatalogEventKind.Damage)row.damage++;
                        if(e.Kind==CatalogEventKind.ShieldBlocked)row.blocks++;
                        string kind=null;
                        if(e.Kind==CatalogEventKind.BossImpact){row.impacts++;if(!row.abilities.Contains(e.AbilityId))row.abilities.Add(e.AbilityId);kind=e.AbilityId;}
                        else if(e.Kind==CatalogEventKind.Damage && e.AbilityId=="BASIC")kind="contact";
                        if(kind!=null && !shots.Contains(kind) && pending==null){pending=kind;due=battle.Time+.06f;}
                    };
                    float deadline=Time.realtimeSinceStartup+28;
                    Debug.Log("COMBAT_FX_AUDIT_BEGIN "+row.name);
                    while(battle.Time<18 && Time.realtimeSinceStartup<deadline)
                    {
                        yield return null;
                        row.visibleLabelsPeak=Mathf.Max(row.visibleLabelsPeak,labels.Count);
                        bool flight=false;foreach(var u in battle.Units)if(AllyAttackPresentation.InFlight(u))flight=true;
                        if(flight)row.flightFrames++;
                        if(pending!=null && battle.Time>=due)
                        {
                            yield return new WaitForEndOfFrame();Capture(dir,row.name+"-"+pending);shots.Add(pending);pending=null;
                        }
                    }
                    report.cases.Add(row);File.WriteAllText(Path.Combine(dir,"report.json"),JsonUtility.ToJson(report,true));
                    Debug.Log("COMBAT_FX_AUDIT_CASE "+row.name+" damage="+row.damage+" impacts="+row.impacts+" flight="+row.flightFrames);
                    Destroy(go);go=null;yield return null;
                }
                report.complete=true;File.WriteAllText(Path.Combine(dir,"report.json"),JsonUtility.ToJson(report,true));
                Debug.Log("COMBAT_FX_AUDIT_COMPLETE");
            }
            finally
            {
                if(go!=null)Destroy(go);screen.enabled=wasEnabled;
                GamePreferences.Current.ReduceMotion=motion;GamePreferences.Current.ReduceFlashes=flashes;
            }
            Destroy(gameObject);
        }
        static void Capture(string dir,string name)
        {
            var shot=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            shot.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);shot.Apply();
            File.WriteAllBytes(Path.Combine(dir,name+".rgb"),shot.GetRawTextureData());
            File.WriteAllText(Path.Combine(dir,"capture-size.txt"),Screen.width+","+Screen.height);Destroy(shot);
        }
    }
}
#endif
