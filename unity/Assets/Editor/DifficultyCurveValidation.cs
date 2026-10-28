using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class DifficultyCurveValidation
    {
        static int checks;
        static void Check(bool value,string why) {checks++;if(!value)throw new Exception("Difficulty integration: "+why);}
        static void Advance(ExpeditionRun run,int wins)
        {while(run.Victories<wins){run.BeginBattle();run.ResolveBattle(true);if(!run.HasEnded)run.Continue();}}
        static ExpeditionAccountSave Snapshot(CatalogDatabase db,ExpeditionRun run,ExpeditionHistory history=null)
        {
            var progress=ExpeditionProgress.New();progress.Observe(run);
            var journal=ExpeditionJournal.New();journal.Observe(run);
            history=history??new ExpeditionHistory();history.Observe(run);
            var save=new ExpeditionAccountSave{Version=2,HasRun=true,Run=run.Capture(),Progress=progress.Capture(),
                Journal=journal.Capture(),History=history.Capture(),TutorialMask=3,LessonMask=7};
            JsonUtility.FromJson<ExpeditionAccountSave>(JsonUtility.ToJson(save)).Validate(db);checks++;
            return save;
        }
        public static void ValidateAndBuild()
        {
            ExpeditionResetValidation.Validate();
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            string root=Path.GetFullPath("Logs/v048-fixtures");Directory.CreateDirectory(root);
            var archives=new ExpeditionHistory();
            foreach(int rules in new[]{1,2,3,4})
            {
                var save=ExpeditionRun.New(db,(uint)(48000+rules)).Capture();save.RulesVersion=rules;save.Version=rules+8;
                var run=ExpeditionRun.Restore(db,save);Advance(run,30);archives.Observe(run);
            }
            var restored=ExpeditionHistory.Restore(db,JsonUtility.FromJson<ExpeditionHistorySave>(JsonUtility.ToJson(archives.Capture())));
            Check(restored.ArchivedBests.Count==3 && restored.Best.BalanceVersion==ExpeditionHistory.BalanceVersion,"all version bests preserved by Unity serialization");
            Check(restored.ArchivedBests.Any(r=>r.BalanceVersion=="normal-045") && restored.ArchivedBests.Any(r=>r.BalanceVersion=="normal-046"),"old best version loss");
            foreach(int wave in new[]{11,12,20})
            {
                var run=ExpeditionRun.New(db,48003,startFormationId:"frost");Advance(run,wave-1);
                while(run.DeployedIds.Count<5){string id=run.Summon();Check(id!=null,"fixture summon");run.SetDeployed(id,true);}
                foreach(string id in run.DeployedIds.ToArray())
                {
                    for(int n=0;n<6;n++)
                    {
                        string material=run.Summon();if(material==null)break;
                        Check(run.Transfer(material,id,false),"fixture growth");
                        string family=CatalogRoster.FamilyCode(run.Roster.Find(id).FormId);
                        if(n==1)run.Evolve(id,"E-"+family+"-"+(family=="KN"?"C":family=="IC"?"F":family=="PR"?"H":family=="AR"?"P":family=="BA"?"A":"B"));
                        if(n==5)run.Evolve(id,"F-"+family);
                    }
                }
                run.PurchaseTactic("bulwark");
                File.WriteAllText(Path.Combine(root,"wave-"+wave+".json"),JsonUtility.ToJson(Snapshot(db,run,restored)));
                Check(run.RulesVersion==CatalogBalance.CurrentRules && run.Capture().Version==CatalogBalance.CurrentSaveVersion,"fresh rules");
            }
            var font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},15);
            try
            {
                var body=new GUIStyle{font=font,fontSize=18,wordWrap=true};
                Check(body.CalcHeight(new GUIContent("후열 다음 앞열에 빙결 원형 공격이 이어집니다.\n동료 간격을 벌리세요 · 각 공격 전에 원이 표시됩니다."),600)<=64,"boss advice height");
                Check(body.CalcSize(new GUIContent("이전 버전 최고 기록")).x<268,"records button width");
            }
            finally {UnityEngine.Object.DestroyImmediate(font);}
            Debug.Log("PASS difficulty Unity integration: "+checks+" checks; version archives, account snapshots and label fit. Fixtures use forced victories for UI QA only.");
            PrototypeSetup.BuildAndroid();
        }
    }
}
