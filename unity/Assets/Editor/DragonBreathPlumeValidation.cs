using System;
using System.IO;
using System.Linq;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class DragonBreathPlumeValidation
    {
        static int checks;
        static void Check(bool value,string message)
        { checks++; if (!value) throw new InvalidOperationException("Breath plume: " + message); }

        public static void ValidateAndBuild()
        {
            UnityEditor.AssetDatabase.ImportAsset("Assets/Resources/Art/Effects/v049/dragon-breath.png",
                UnityEditor.ImportAssetOptions.ForceUpdate);
            BossSpriteEffectsValidation.Validate();
            DragonBreathValidation.Validate();
            for (int frame=0;frame<8;frame++)
                foreach (float length in new[]{100f,275f,500f})
                    foreach (float halfWidth in new[]{.4f,.65f,1f})
                        foreach (float angle in new[]{-65f,-20f,0f,35f,75f})
                        {
                            Vector2 mouth = new Vector2(810f,345f);
                            Rect rect = DragonBreathArt.LocalBounds(frame,length,halfWidth);
                            Vector2 size = DragonBreathArt.CellSize(frame), nozzle = DragonBreathArt.Nozzle(frame);
                            Vector2 local = rect.position + Vector2.Scale(nozzle,
                                new Vector2(rect.width/size.x,rect.height/size.y));
                            var matrix = Matrix4x4.TRS(mouth,Quaternion.Euler(0,0,angle),Vector3.one);
                            Check(Vector2.Distance(matrix.MultiplyPoint3x4(local),mouth)<.01f,
                                "flame must stay on jaw at every scale and aiming angle");
                            float front = rect.x+DragonBreathArt.Front(frame)*rect.width/size.x;
                            Check(front >= -length-.01f && front < 0f,"flame exceeds locked endpoint");
                            if(frame>=2) Check(Mathf.Abs(front+length)<.01f,"full plume must reach endpoint");
                            if(frame>=6) Check(rect.x+rect.width*DragonBreathArt.SmokeCrop(frame)<-10f,
                                "smoke must leave a visible gap from closed mouth");
                        }
            Check(DragonBreathArt.EmissionFrame(0)==0 && DragonBreathArt.EmissionFrame(1)==5,
                "emission must not use smoke frames");
            // Forced progression is only a repeatable visual QA fixture, not a balance result.
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var run=ExpeditionRun.New(db,49003,startFormationId:"frost");
            while(run.Victories<29){run.BeginBattle();run.ResolveBattle(true);run.Continue();}
            while(run.DeployedIds.Count<5){string id=run.Summon();Check(id!=null,"QA summon");run.SetDeployed(id,true);}
            foreach(string id in run.DeployedIds.ToArray())
                for(int n=0;n<6;n++)
                {
                    string material=run.Summon();if(material==null)break;
                    Check(run.Transfer(material,id,false),"QA growth");
                    string family=CatalogRoster.FamilyCode(run.Roster.Find(id).FormId);
                    if(n==1)run.Evolve(id,"E-"+family+"-"+(family=="KN"?"C":family=="IC"?"F":family=="PR"?"H":family=="AR"?"P":family=="BA"?"A":"B"));
                    if(n==5)run.Evolve(id,"F-"+family);
                }
            var progress=ExpeditionProgress.New();progress.Observe(run);
            var journal=ExpeditionJournal.New();journal.Observe(run);
            var history=new ExpeditionHistory();history.Observe(run);
            var save=new ExpeditionAccountSave{Version=2,HasRun=true,Run=run.Capture(),Progress=progress.Capture(),
                Journal=journal.Capture(),History=history.Capture(),TutorialMask=3,LessonMask=7};
            string json=JsonUtility.ToJson(save);JsonUtility.FromJson<ExpeditionAccountSave>(json).Validate(db);
            Directory.CreateDirectory("Logs/v049-fixtures");File.WriteAllText("Logs/v049-fixtures/wave-30.json",json);
            Debug.Log("PASS breath plume: "+checks+" geometry/fixture checks, sheet seams and real breath clock; visual-only changes.");
            PrototypeSetup.BuildAndroid();
        }
    }
}
