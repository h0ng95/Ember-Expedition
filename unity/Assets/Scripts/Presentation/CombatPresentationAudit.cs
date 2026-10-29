#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Explicit, one-shot local QA only. Never reads/writes expedition saves.
    public sealed class CombatPresentationAudit : MonoBehaviour
    {
        public static readonly string[][] Groups = {
            new[]{"E-PR-H","H-05","F-IC","E-FI-E","F-BA"},
            new[]{"B-KN","B-AR","B-FI","B-IC","B-PR"},
            new[]{"B-BA","E-KN-G","E-KN-C","E-AR-P","E-AR-T"},
            new[]{"E-FI-B","E-IC-F","E-IC-S","E-PR-G","E-BA-G"},
            new[]{"E-BA-A","F-KN","F-AR","F-FI","F-PR"},
            new[]{"H-01","H-02","H-03","H-04","H-06"}
        };
        public static string Branch(string id)
        { return id.StartsWith("F-",StringComparison.Ordinal) ? "E-"+id.Substring(2)+
            (id=="F-KN"||id=="F-BA"?"-G":id=="F-AR"?"-T":id=="F-FI"?"-E":id=="F-PR"?"-H":"-F") : null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            string flag=Path.Combine(Application.persistentDataPath,"combat-art-audit.flag");
            if(!File.Exists(flag))return;
            File.Delete(flag);
            var go=new GameObject("Explicit combat presentation QA");DontDestroyOnLoad(go);
            go.AddComponent<CombatPresentationAudit>();
        }

        [Serializable] public sealed class CaseResult
        { public string name;public int speed;public string[] forms;public int basic,skill,damage;public string screenshot; }
        [Serializable] public sealed class Report { public List<CaseResult> cases=new List<CaseResult>();public bool completed; }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(4);
            var screen=FindFirstObjectByType<ExpeditionScreen>();if(screen!=null)screen.enabled=false;
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            string directory=Path.Combine(Application.persistentDataPath,"CombatArtAudit");Directory.CreateDirectory(directory);
            var report=new Report();
            for(int group=0;group<8;group++)for(int speed=1;speed<=2;speed++)
            {
                string[] ids=group<6?Groups[group]:new[]{"F-KN","H-01","H-02","E-KN-G","E-IC-S"};
                var battle=new CatalogBattle(db);
                for(int i=0;i<5;i++)
                {
                    var a=battle.AddUnit("qa-"+i,CatalogTeam.Ally,ids[i],Branch(ids[i]),null,
                        group>=6?-1.3f-i*.08f:-3.2f-i*.35f,group>=6?i*.18f:2f-i);
                    typeof(CatalogBattleUnit).GetProperty("MaxHp").SetValue(a,10000f,null);
                    typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(a,8000f,null);
                }
                if(group==7)
                    battle.AddEnemy("palace-boss",100000,5,15,1.8f,3.2f,.48f,true,3,0,CatalogEnemyRole.CrownDrake,true);
                else
                    for(int i=0;i<5;i++)battle.AddEnemy("frost-qa-"+i,100000,4,12,1.8f,1.4f,.55f,false,
                        1.2f+i*.13f,group==6?i*.18f:2f-i);
                var go=new GameObject("Combat audit case");var lab=go.AddComponent<CatalogCombatLab>();
                string name=group<6?"forms-"+group:group==6?"dense":"dragon";
                lab.OpenExpeditionBattle(battle,group==7?30:20,30,"QA "+name, _=>{},()=>{});
                lab.ConfigureExpeditionSpeed(speed,null);
                var result=new CaseResult{name=name,speed=speed,forms=ids,screenshot=name+"-"+speed+"x.png"};
                battle.Event+=e=>{if(e.Kind==CatalogEventKind.BasicHit)result.basic++;
                    if(e.Kind==CatalogEventKind.SkillUsed)result.skill++;if(e.Kind==CatalogEventKind.Damage)result.damage++;};
                Debug.Log("ART_AUDIT_BEGIN "+name+" "+speed+"x "+string.Join(",",ids));
                float duration=group==7?18f/speed:10f/speed;
                yield return new WaitForSecondsRealtime(duration*.65f);
                yield return new WaitForEndOfFrame();
                var capture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
                capture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);capture.Apply();
                File.WriteAllBytes(Path.Combine(directory,result.screenshot+".rgb"),capture.GetRawTextureData());
                File.WriteAllText(Path.Combine(directory,"capture-size.txt"),Screen.width+","+Screen.height);Destroy(capture);
                yield return new WaitForSecondsRealtime(duration*.35f);
                report.cases.Add(result);File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
                Destroy(go);yield return null;
            }
            report.completed=true;File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("ART_AUDIT_COMPLETE 30 forms, 1x/2x, dense and dragon; saves untouched.");
            if(screen!=null)screen.enabled=true;
            Destroy(gameObject);
        }
    }
}
#endif
