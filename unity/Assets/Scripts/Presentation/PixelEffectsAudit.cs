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
    // Explicit development-only device fixture. No account, run or preference writes.
    public sealed class PixelEffectsAudit : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            string flag=Path.Combine(Application.persistentDataPath,"pixel-effects-audit.flag");
            if(!File.Exists(flag))return;
            File.Delete(flag);
            var go=new GameObject("Explicit PixelFX audit");DontDestroyOnLoad(go);go.AddComponent<PixelEffectsAudit>();
        }
        [Serializable] public sealed class Case
        { public string name; public int basic,heal,freeze,frames; public float seconds,maxFrameMs; }
        [Serializable] public sealed class Report
        { public bool complete;public List<Case> cases=new List<Case>(); }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(5);
            var screen=FindFirstObjectByType<ExpeditionScreen>();
            if(screen==null)yield break;
            bool enabledBefore=screen.enabled;screen.enabled=false;
            bool flashes=GamePreferences.Current.ReduceFlashes,motion=GamePreferences.Current.ReduceMotion;
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            string dir=Path.Combine(Application.persistentDataPath,"PixelEffectsAudit");Directory.CreateDirectory(dir);
            var report=new Report(); GameObject go=null;
            try
            {
                for(int c=0;c<3;c++)
                {
                    GamePreferences.Current.ReduceFlashes=c==2;GamePreferences.Current.ReduceMotion=c==2;
                    int speed=c==0?1:2;
                    var result=new Case{name=c==0?"normal-1x":c==1?"normal-2x":"reduced-2x"};
                    var battle=new CatalogBattle(db);
                    string[] ids={"B-KN","E-KN-G","H-02","F-PR","B-IC"};
                    for(int i=0;i<ids.Length;i++)
                    {
                        var unit=battle.AddUnit("qa-"+i,CatalogTeam.Ally,ids[i],i==3?"E-PR-H":null,null,
                            -2.1f-i*.35f,(i-2)*.5f);
                        typeof(CatalogBattleUnit).GetProperty("MaxHp").SetValue(unit,4000f);
                        typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(unit,1800f);
                    }
                    battle.AddEnemy("frost-boss",100000,18,15,1.8f,1.6f,.48f,true,2f,0,CatalogEnemyRole.FrostWarden,true);
                    battle.AddEnemy("frost-guard",100000,12,12,1.5f,1.2f,.55f,false,1f,-1);
                    go=new GameObject("PixelFX case");var lab=go.AddComponent<CatalogCombatLab>();
                    lab.OpenExpeditionBattle(battle,20,30,"PixelFX / "+result.name,_=>{},()=>{});
                    lab.ConfigureExpeditionSpeed(speed,null);
                    var shots=new HashSet<string>();string pending=null;float due=0;
                    battle.Event+=e=>{
                        string kind=null;
                        if(e.Kind==CatalogEventKind.BasicHit){result.basic++;kind="slash";}
                        if(e.Kind==CatalogEventKind.Heal){result.heal++;kind="heal";}
                        if(e.Kind==CatalogEventKind.Freeze){result.freeze++;kind="freeze";}
                        if(kind!=null && !shots.Contains(kind) && pending==null)
                        {pending=kind;due=battle.Time+.06f;}
                    };
                    float began=Time.realtimeSinceStartup;
                    while(battle.Time<32 && Time.realtimeSinceStartup-began<42)
                    {
                        yield return null;result.frames++;result.maxFrameMs=Mathf.Max(result.maxFrameMs,Time.unscaledDeltaTime*1000);
                        if(pending!=null && battle.Time>=due)
                        {
                            yield return new WaitForEndOfFrame();
                            var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
                            texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);texture.Apply();
                            File.WriteAllBytes(Path.Combine(dir,result.name+"-"+pending+".rgb"),texture.GetRawTextureData());
                            File.WriteAllText(Path.Combine(dir,"capture-size.txt"),Screen.width+","+Screen.height);
                            Destroy(texture);shots.Add(pending);pending=null;
                        }
                    }
                    result.seconds=Time.realtimeSinceStartup-began;
                    report.cases.Add(result);File.WriteAllText(Path.Combine(dir,"report.json"),JsonUtility.ToJson(report,true));
                    Debug.Log("PIXELFX_AUDIT_CASE "+result.name+" basic="+result.basic+" heal="+result.heal+" freeze="+result.freeze);
                    Destroy(go);go=null;yield return null;
                }
                report.complete=true;File.WriteAllText(Path.Combine(dir,"report.json"),JsonUtility.ToJson(report,true));
                Debug.Log("PIXELFX_AUDIT_COMPLETE");
            }
            finally
            {
                if(go!=null)Destroy(go);
                GamePreferences.Current.ReduceFlashes=flashes;GamePreferences.Current.ReduceMotion=motion;
                screen.enabled=enabledBefore;
            }
            Destroy(gameObject);
        }
    }
}
#endif
