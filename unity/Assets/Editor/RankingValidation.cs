using System;
using System.IO;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class RankingValidation
    {
        static int checks;
        static void Check(bool ok, string name) { checks++; if(!ok)throw new Exception("Ranking: "+name); }
        public static void ValidateAndBuild() { Validate(); CloudAccountValidation.Validate(); PrototypeSetup.BuildAndroid(); }
        public static void Validate()
        {
            string language = CatalogText.Language;
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var ticket = new RankedRunTicket {Id=Guid.NewGuid().ToString("N"),OwnerId="test-player",Seed=73001,
                Rules=CatalogBalance.CurrentRules,Formation="standard"};
            var run=ExpeditionRun.New(db,ticket.Seed); run.AttachRankedTicket(ticket);
            while(!run.HasEnded) { RankedReplay.Fight(run); if(!run.HasEnded)run.Continue(); }
            run=ExpeditionRun.Restore(db,JsonUtility.FromJson<ExpeditionRunSave>(JsonUtility.ToJson(run.Capture())));
            var proof=JsonUtility.FromJson<RankedRunProof>(JsonUtility.ToJson(run.RankedProof));
            Check(RankedReplay.Verify(db,ticket,proof).Score==run.Score,"Unity JSON proof replay");
            var before=JsonUtility.ToJson(run.RankedProof); run.Continue();
            Check(before==JsonUtility.ToJson(run.RankedProof),"terminal continue preserves submission digest");
            var history=new ExpeditionHistory(); history.Observe(run);
            Check(history.Recent[0].FinalRun.RankedProof==null && run.RankedProof!=null,"proof not duplicated in history");
            Check(ExpeditionScreen.RankingWindow.center==new Vector2(640,360),"centered window");
            var font=Resources.Load<Font>("Fonts/Galmuri11");
            var small=new GUIStyle {font=font,fontSize=15,wordWrap=true};
            try
            {
                foreach(var locale in new[]{"ko","en"})
                {
                    CatalogText.SetLanguage(locale);
                    foreach(var key in new[]{"help","failed","submitted","guest_help"})
                        Check(small.CalcHeight(new GUIContent(CatalogText.Get("rank."+key)),966)<=44,locale+key);
                    Check(small.CalcHeight(new GUIContent(CatalogText.Get("rank.description")),966)<=54,locale+" description");
                    foreach(var key in new[]{"start_help","local_help"})
                        Check(small.CalcHeight(new GUIContent(CatalogText.Get("rank."+key)),766)<=23,locale+key);
                    Check(small.CalcHeight(new GUIContent(CatalogText.Get("rank.account_help")),626)<=52,locale+" account help");
                }
                File.WriteAllText("Logs/v073-validation.txt","PASS "+checks+" checks; proof JSON/replay, terminal submission, history size, KO/EN layout.");
                Debug.Log("RANKING_VALIDATION_PASS "+checks);
            }
            finally { CatalogText.SetLanguage(language); }
        }
    }
}
