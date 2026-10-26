using System;
using System.IO;
using System.Linq;
using Expedition.Core;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class ResearchRegionValidation
    {
        static int checks;
        static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception("Research region UI: " + why); }
        static void Advance(ExpeditionRun run, int wins)
        { while(run.Victories<wins) { run.BeginBattle();run.ResolveBattle(true);if(!run.HasEnded)run.Continue(); } }
        static ExpeditionAccountSave Snapshot(CatalogDatabase db, ExpeditionRun run, ExpeditionHistory history = null)
        {
            var progress=ExpeditionProgress.New();progress.Observe(run);
            var journal=ExpeditionJournal.New();journal.Observe(run);
            history=history??new ExpeditionHistory();history.Observe(run);
            var result=new ExpeditionAccountSave{Version=2,HasRun=true,Run=run.Capture(),Progress=progress.Capture(),
                Journal=journal.Capture(),History=history.Capture(),TutorialMask=3,LessonMask=7};
            JsonUtility.FromJson<ExpeditionAccountSave>(JsonUtility.ToJson(result)).Validate(db);checks++;
            return result;
        }
        public static void ValidateAndBuild()
        {
            ExpeditionResetValidation.Validate();
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            string root=Path.GetFullPath("Logs/v046-fixtures");Directory.CreateDirectory(root);
            foreach(int victories in new[]{4,5,9,10})
            {
                var run=ExpeditionRun.New(db,46001,startFormationId:"frost");Advance(run,victories);
                if(victories>=9)Check(run.PurchaseTactic("bulwark"),"base purchase after clear");
                string name=victories==4?"region-locked":victories==5?"base-open":victories==9?"advanced-locked":"advanced-open";
                File.WriteAllText(Path.Combine(root,name+".json"),JsonUtility.ToJson(Snapshot(db,run)));
                Check(run.HasResearchRegion(CatalogResearch.Get("spellcraft"))==(victories>=5),"forest gate");
                Check(run.CanPurchaseTactic("shieldcraft")== (victories==10),"castle gate");
            }
            var oldSave=ExpeditionRun.New(db,45001).Capture();oldSave.Version=9;oldSave.RulesVersion=1;
            var old=ExpeditionRun.Restore(db,oldSave);Advance(old,30);
            var record=new ExpeditionRecord{SavedUtc=DateTime.UtcNow.ToString("o"),Score=333000,ScoreVersion=1,
                BalanceVersion="normal-045",FinalRun=old.Capture()};
            var oldHistory=new ExpeditionHistorySave{Version=1,Recent=new[]{record},Best=record,HasBest=true};
            var migrated=ExpeditionHistory.Restore(db,JsonUtility.FromJson<ExpeditionHistorySave>(JsonUtility.ToJson(oldHistory)));
            Check(migrated.Best==null && migrated.ArchivedBest.Score==333000,"v045 best migration");
            var current=ExpeditionRun.New(db,46001);
            File.WriteAllText(Path.Combine(root,"v045-records.json"),JsonUtility.ToJson(Snapshot(db,current,migrated)));
            var restored=ExpeditionHistory.Restore(db,JsonUtility.FromJson<ExpeditionHistorySave>(JsonUtility.ToJson(migrated.Capture())));
            Check(restored.Best==null && restored.ArchivedBest.Score==333000,"v2 null best roundtrip");
            var full=ExpeditionRun.New(db,46004,startFormationId:"frost"); Advance(full,25);
            while(full.DeployedIds.Count<5) { string member=full.Summon(); if(member==null)throw new Exception("UI fixture summon"); full.SetDeployed(member,true); }
            foreach(string id in full.DeployedIds.ToArray())
            {
                for(int n=0;n<6;n++)
                {
                    string material=full.Summon(); if(material==null || !full.Transfer(material,id,false))throw new Exception("UI fixture growth");
                    var member=full.Roster.Find(id);string family=CatalogRoster.FamilyCode(member.FormId);
                    if(n==1)full.Evolve(id,"E-"+family+"-"+(family=="KN"?"C":family=="IC"?"F":family=="PR"?"H":family=="AR"?"P":family=="BA"?"A":"B"));
                    if(n==5)full.Evolve(id,"F-"+family);
                }
            }
            full.PurchaseTactic("bulwark");
            File.WriteAllText(Path.Combine(root,"full-roster.json"),JsonUtility.ToJson(Snapshot(db,full)));
            Font font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},15);
            try
            {
                var style=new GUIStyle{font=font,fontSize=15,wordWrap=true};
                foreach(string text in new[]{"성채 10전투 돌파 필요 · 60G\n이번 원정에서 지역 보스를 격파하세요.\n골드만으로는 열 수 없습니다.",
                    "성채 10전투 돌파 완료 · 60G\n먼저 연결된 연구를 여세요.\n술식 연구"})
                    Check(style.CalcHeight(new GUIContent(text),292)<=78,"requirement clipped");
                Check(style.CalcHeight(new GUIContent("숲 돌파 → 기초 · 성채 돌파 → 상위  /  새 원정에서 초기화"),760)<=30,"header clipped");
            }
            finally{UnityEngine.Object.DestroyImmediate(font);}
            Debug.Log("PASS research region Unity integration: "+checks+" checks; save gates, versioned best migration, empty current best, label fit.");
            PrototypeSetup.BuildAndroid();
        }
    }
}
