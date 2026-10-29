using System;
using System.IO;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class PreparationPresentationValidation
    {
        static int checks;
        static readonly System.Collections.Generic.List<string> failures = new System.Collections.Generic.List<string>();
        static void Check(bool ok, string why) { checks++; if (!ok) failures.Add(why); }
        public static void ValidateAndBuild() { Validate(); PrototypeSetup.BuildAndroid(); }
        public static void Validate()
        {
            checks = 0;
            failures.Clear();
            DragGrowthValidation.Validate();
            ExpeditionTutorialValidation.Validate();
            ExpeditionLearningValidation.Validate();
            BattleReadabilityValidation.Validate();
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var go = new GameObject("Preparation layout QA"); go.SetActive(false);
            var screen = go.AddComponent<ExpeditionScreen>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ExpeditionScreen).GetField("database", flags).SetValue(screen, db);
            Font font = Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},19);
            var style = new GUIStyle { font=font, wordWrap=true, padding=new RectOffset(2,2,2,2) };
            Action<string,int,float,float,string> fit = (text,size,width,height,label) => {
                style.fontSize=size;
                Check(style.CalcHeight(new GUIContent(text),width)<=height,label+" clipped: "+text);
            };
            try
            {
                foreach(var form in db.All)
                {
                    fit(form.Name,26,312,42,"selected name");
                    fit(form.Name,13,170,28,"support name");
                    fit(form.Name,17,146,45,"ingredient name");
                    fit(form.Name,29,292,76,"result name");
                    fit(CatalogUnitInfo.Role(form.Id),17,310,40,"role");
                    string overview=(string)typeof(ExpeditionScreen).GetMethod("GrowthOverviewText",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{form.Id});
                    fit(overview,18,292,108,"result summary");
                    var support=CatalogPreparationPreview.ForSkill(form.Id);
                    if(support!=null)fit(support.Rule,14,310,43,"support rule");
                }
                for(int i=0;i<5;i++)
                {
                    Rect row=ExpeditionScreen.SupportLayout.Card(i);
                    Check(ExpeditionScreen.PreparationLayout.Sidebar.Contains(row.min)&&ExpeditionScreen.PreparationLayout.Sidebar.Contains(row.max),"support row escaped");
                    Check(!ExpeditionScreen.SupportLayout.Name(i).Overlaps(ExpeditionScreen.SupportLayout.Status(i)),"name/status overlap");
                    if(i>0)Check(!row.Overlaps(ExpeditionScreen.SupportLayout.Card(i-1)),"support rows overlap");
                    Check(!ExpeditionScreen.PreparationLayout.Bench(i).Overlaps(ExpeditionScreen.PreparationLayout.Summon),"bench/summon overlap");
                }
                for(float x=-6.2f;x<=-.2f+.01f;x+=.5f)for(float y=-2.5f;y<=2.2f;y+=.5f)
                {
                    Vector2 p=ExpeditionScreen.PreparationLayout.Point(x,y);
                    Check(Vector2.Distance(new Vector2(x,y),ExpeditionScreen.PreparationLayout.World(p))<.001f,"placement round trip");
                    Check(ExpeditionScreen.PreparationLayout.Board.Contains(p),"legal position off board");
                }
                Directory.CreateDirectory("Logs/v056-fixtures");
                for(int wave=1;wave<=30;wave++)
                {
                    var run=ExpeditionRun.New(db,56001);
                    while(run.Wave<wave){run.BeginBattle();run.ResolveBattle(true);run.Continue();}
                    typeof(ExpeditionScreen).GetField("run",flags).SetValue(screen,run);
                    string advice=(string)typeof(ExpeditionScreen).GetMethod("PreparationAdvice",flags).Invoke(screen,null);
                    fit(advice,17,310,114,"enemy advice");
                    fit(run.EncounterName,26,310,64,"encounter name");
                    fit(run.EncounterDescription,14,310,65,"encounter composition");
                }
                // Use legal progression and existing saved QA investment, never players' real data.
                foreach(int wave in new[]{15,30})
                {
                    var state=JsonUtility.FromJson<ExpeditionAccountSave>(File.ReadAllText("../docs/design-v0.56/fixtures/wave-"+wave+".json"));
                    state.Validate(db); File.WriteAllText("Logs/v056-fixtures/wave-"+wave+".json",JsonUtility.ToJson(state));
                }
                var template=JsonUtility.FromJson<ExpeditionAccountSave>(File.ReadAllText("../docs/design-v0.56/fixtures/wave-15.json"));
                var choice=ExpeditionRun.Restore(db,JsonUtility.FromJson<ExpeditionRunSave>(File.ReadAllText("Logs/v016-device-choice.json")));
                Export("combination",choice,template,db);
                var evolution=ExpeditionRun.New(db,56002);
                while(evolution.Wave<3){evolution.BeginBattle();evolution.ResolveBattle(true);evolution.Continue();}
                evolution.Transfer(evolution.Summon(),"ally-01",false);evolution.Transfer(evolution.Summon(),"ally-01",false);
                Export("evolution",evolution,template,db);
                var fresh=ExpeditionRun.New(db,56003); Export("tutorial",fresh,template,db,0,0);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(font); }
            if(failures.Count>0)throw new Exception("Preparation presentation: "+string.Join("\n",failures));
            Debug.Log("PASS preparation presentation: "+checks+" layout/text checks; drag, undo, double confirmation, tutorial, battle HUD regressions; five QA fixtures.");
        }
        static void Export(string name,ExpeditionRun run,ExpeditionAccountSave template,CatalogDatabase db,int tutorial=3,int lessons=7)
        {
            template.Run=run.Capture();template.HasRun=true;template.TutorialMask=tutorial;template.LessonMask=lessons;
            var progress=ExpeditionProgress.New();progress.Observe(run);template.Progress=progress.Capture();
            var journal=ExpeditionJournal.New();journal.Observe(run);template.Journal=journal.Capture();
            template.Validate(db);File.WriteAllText("Logs/v056-fixtures/"+name+".json",JsonUtility.ToJson(template));
        }
    }
}
