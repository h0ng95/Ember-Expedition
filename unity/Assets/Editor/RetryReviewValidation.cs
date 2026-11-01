using System;
using System.IO;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class RetryReviewValidation
    {
        static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static int checks;
        static void Check(bool condition,string why){checks++;if(!condition)throw new Exception("Retry review: "+why);}
        static void Set(object o,string key,object value){typeof(ExpeditionScreen).GetField(key,Hidden).SetValue(o,value);}
        static object Get(object o,string key){return typeof(ExpeditionScreen).GetField(key,Hidden).GetValue(o);}
        static object Call(object o,string key,params object[] args){return typeof(ExpeditionScreen).GetMethod(key,Hidden).Invoke(o,args);}
        public static void ValidateAndBuild(){Validate();PrototypeSetup.BuildAndroid();}
        public static void Validate()
        {
            checks=0;var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            Directory.CreateDirectory("Logs/v068-fixtures");
            foreach(string fixture in new[]{"legal-15-timeout","legal-18-defeat","legal-19-defeat","legal-18-win"})
            {
                string json=File.ReadAllText("../.local-tools/v067-holdout/"+fixture+".json");
                File.WriteAllText("Logs/v068-fixtures/"+fixture+".json",json);
                var run=ExpeditionRun.Restore(db,JsonUtility.FromJson<ExpeditionRunSave>(json));
                var battle=run.BeginBattle();var report=new CatalogBattleReport(battle);
                while(!battle.Ended&&battle.Time<75f)battle.Step(battle.Time<74.75f?.25f:1f/60f);
                bool won=battle.Ended&&battle.Winner==CatalogTeam.Ally;run.ResolveBattle(won);report.Complete(won);
                var go=new GameObject("Retry review validation");go.SetActive(false);
                try
                {
                    var screen=go.AddComponent<ExpeditionScreen>();Set(screen,"database",db);Set(screen,"run",run);
                    Set(screen,"badSave",true);Set(screen,"storageBlocked",true);Set(screen,"battleReport",report);
                    Set(screen,"battleReportRun",run);Set(screen,"battleReportComplete",true);
                    string before=JsonUtility.ToJson(run.Capture());
                    Check(!(bool)Call(screen,"PrepareRetryFor","missing-id"),"missing unit blocked");
                    Check(before==JsonUtility.ToJson(run.Capture()),"invalid action never consumes resources");
                    string target=report.FirstLostUnitId??report.Allies[0].Id;
                    if(won){Check(!(bool)Call(screen,"PrepareRetryFor",target),"victory does not enter retry");continue;}
                    Check((bool)Call(screen,"PrepareRetryFor",target),"failed battle enters preparation");
                    var expected=JsonUtility.FromJson<ExpeditionRunSave>(before);expected.Phase=ExpeditionPhase.Preparation;
                    Check(JsonUtility.ToJson(expected)==JsonUtility.ToJson(run.Capture()),"only phase changes; same wave, gold, chances, roster, RNG and undo");
                    Check((string)Get(screen,"selectedId")==target && (string)Get(screen,"preparationSelectionId")==target,
                        "same unit ID selected in details and formation, including duplicate forms");
                    Check(Call(screen,"PreparationSelection")!=null,"selected member available to range preview");
                    string prepared=JsonUtility.ToJson(run.Capture());
                    string second=report.Allies[1].Id;
                    Check((bool)Call(screen,"PrepareRetryFor",second),"reopened report can select a second ally");
                    Check(prepared==JsonUtility.ToJson(run.Capture()),"repeated focus has no economic or battle effect");
                    Set(screen,"battleReportComplete",false);
                    Check(!(bool)Call(screen,"PrepareRetryFor",target),"missing transient report blocked");
                    Set(screen,"battleReportComplete",true);Set(screen,"run",ExpeditionRun.New(db,68888));
                    Check(!(bool)Call(screen,"PrepareRetryFor",target),"old run report cannot change a new run");
                    run=ExpeditionRun.Restore(db,JsonUtility.FromJson<ExpeditionRunSave>(before));
                    Set(screen,"run",run);Set(screen,"battleReportRun",run);Set(screen,"retryReportRun",null);
                    Check((bool)Call(screen,"ContinueResultPreparation"),"ordinary retry button enters preparation");
                    Check(JsonUtility.ToJson(expected)==JsonUtility.ToJson(run.Capture()),"ordinary retry preserves same-wave resources");
                    Check((bool)typeof(ExpeditionScreen).GetProperty("HasBattleReport",Hidden).GetValue(screen,null),"ordinary retry retains previous report link");
                    Check((bool)Call(screen,"PrepareRetryFor",target),"ordinary retry can focus the recorded ally");
                    Call(screen,"BeginReport",run.BeginBattle());
                    Check(Get(screen,"retryReportRun")==null,"starting a new fight clears retry context");
                }
                finally {UnityEngine.Object.DestroyImmediate(go);}
            }
            // Same report evidence, but no remaining chances: must not reopen preparation.
            var exhausted=ExpeditionRun.Restore(db,JsonUtility.FromJson<ExpeditionRunSave>(File.ReadAllText("../.local-tools/v067-holdout/legal-19-timeout.json")));
            Check(exhausted.Lantern==1,"actual final-chance checkpoint");
            var last=exhausted.BeginBattle();var evidence=new CatalogBattleReport(last);
            while(!last.Ended&&last.Time<75f)last.Step(.25f);
            exhausted.ResolveBattle(false);evidence.Complete(false);
            Check(evidence.Outcome==CatalogBattleOutcome.Timeout && exhausted.HasEnded,"actual last-chance timeout");
            var endedObject=new GameObject("No retry after final loss");endedObject.SetActive(false);
            try
            {
                var screen=endedObject.AddComponent<ExpeditionScreen>();Set(screen,"run",exhausted);Set(screen,"battleReport",evidence);
                Set(screen,"battleReportRun",exhausted);Set(screen,"battleReportComplete",true);Set(screen,"badSave",true);
                Check(!(bool)Call(screen,"PrepareRetryFor",evidence.Allies[0].Id),"exhausted run remains ended");
            }
            finally{UnityEngine.Object.DestroyImmediate(endedObject);}
            File.WriteAllText("Logs/v068-validation.txt","PASS "+checks+" checks: retry focus, same-wave resource preservation, duplicate-safe IDs, repeated focus, stale/missing/victory/ended guards.");
            Debug.Log("RETRY_REVIEW_VALIDATION_PASS "+checks);
        }
    }
}
