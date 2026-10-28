using System;
using System.IO;
using System.Collections.Generic;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class CombatHudValidation
    {
        static int checks;
        static void Check(bool condition,string message)
        {checks++;if(!condition)throw new Exception("Combat HUD: "+message);}
        public static void ValidateAndBuild()
        {
            checks=0;
            BattleReadabilityValidation.Validate();
            var trail=new CombatHud.HealthTrail();trail.Tick(1,0,false);trail.Tick(.4f,.05f,false);
            Check(trail.Value>.9f,"damage trail must retain the lost segment briefly");
            for(int i=0;i<120;i++){trail.Tick(.4f,1f/60f,false);Check(trail.Value>=.4f&&trail.Value<=1,"trail limits");}
            Check(trail.Value==.4f,"trail must settle after damage");
            trail.Tick(.9f,.02f,false);Check(trail.Value==.9f,"healing must immediately raise trail");
            trail.Tick(.2f,.01f,true);Check(trail.Value==.2f,"reduced motion removes trail animation");
            trail.Tick(1,.01f,false);
            for(int i=0;i<80;i++)trail.Tick(1-i*.005f,.02f,false);
            Check(trail.Value<.9f,"continuous damage must not indefinitely reset trail hold");
            Rect limits=new Rect(28,102,1224,530);
            foreach(Vector2 point in new[]{new Vector2(40,108),new Vector2(1240,108),new Vector2(640,400),new Vector2(1240,620)})
            {
                var placed=new List<Rect>();
                for(int n=0;n<12;n++)
                {
                    Rect r=CombatHud.PlaceNearby(new Rect(point.x-44,point.y,88,36),placed,limits);
                    Check(r.xMin>=limits.xMin&&r.xMax<=limits.xMax&&r.yMin>=limits.yMin&&r.yMax<=limits.yMax,"crowd label escaped safe area");
                    foreach(Rect old in placed)Check(!r.Overlaps(old),"twelve compact health blocks must not overlap");
                    placed.Add(r);
                }
            }
            for(int count=1;count<=4;count++)
            {
                Rect first=CatalogCombatLab.HudRules.BossChip(0,count),last=CatalogCombatLab.HudRules.BossChip(count-1,count);
                Check(Mathf.Abs((first.xMin+last.xMax)/2f-640)<.01f,"status row not centered");
            }
            Check(CatalogCombatLab.HudRules.BossHealth.center.x==640&&CatalogCombatLab.HudRules.BossName.center.x==640,"boss center alignment");
            Font font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},12);
            var partyName=CatalogCombatLab.HudRules.HeaderText(font,12,TextAnchor.MiddleLeft,true);
            var partyHealth=CatalogCombatLab.HudRules.HeaderText(font,11,TextAnchor.MiddleCenter);
            var catalog=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            foreach(var form in catalog.All)
            {
                Rect r=CatalogCombatLab.HudRules.PartyName(0);var text=new GUIContent(form.Name);
                Check(partyName.CalcSize(text).x<=r.width&&partyName.CalcHeight(text,r.width)<=r.height,"party name clipped: "+form.Name);
            }
            foreach(string text in new[]{"99999 / 99999","전투 불능"})
            {
                Rect r=CatalogCombatLab.HudRules.PartyHealth(0);var content=new GUIContent(text);
                Check(partyHealth.CalcSize(content).x<=r.width&&partyHealth.CalcHeight(content,r.width)<=r.height,"party HP clipped");
            }
            UnityEngine.Object.DestroyImmediate(font);
            for(int n=1;n<=5;n++)
            {
                Rect p=CatalogCombatLab.HudRules.PartyPanel(n),last=CatalogCombatLab.HudRules.PartyRow(n-1);
                Check(p.yMax<664&&last.yMax<=p.yMax&&p.xMax<CatalogCombatLab.HudRules.BattleArena.x,"party panel overlaps footer/arena");
            }
            // Horizontal mapping reserves the roster rail; sprites retain their original dimensions.
            var project=typeof(CatalogCombatLab).GetMethod("ExpeditionPoint",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            foreach(float x in new[]{-6.4f,-3f,0f,3f,6.4f})
            {
                Vector2 point=(Vector2)project.Invoke(null,new object[]{new Vector2(x,0),CatalogCombatLab.HudRules.BattleArena});
                Check(point.x-60>CatalogCombatLab.HudRules.PartyPanel(5).xMax,"ally silhouette covered by sidebar");
            }
            // Dedicated QA fixtures; no phone save or account data is read.
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            Directory.CreateDirectory("Logs/v051-fixtures");
            foreach(int wave in new[]{15,30})
            {
                var run=ExpeditionRun.Restore(db,JsonUtility.FromJson<ExpeditionRunSave>(File.ReadAllText("../.local-tools/v051-balance/legal-wave-"+wave+".json")));
                SaveFixture(db,run,wave);
            }
            var forced=ExpeditionRun.New(db,51101,startFormationId:"frost");
            while(forced.Wave<=20)
            {
                if(forced.Wave==5||forced.Wave==12||forced.Wave==20)SaveFixture(db,forced,forced.Wave);
                forced.BeginBattle();forced.ResolveBattle(true);if(forced.Wave==20)break;forced.Continue();
            }
            Debug.Log("PASS Combat HUD: "+checks+" trail, crowd and centered status checks; existing all-wave text validation passed.");
            PrototypeSetup.BuildAndroid();
        }
        static void SaveFixture(CatalogDatabase db,ExpeditionRun run,int wave)
        {
            var progress=ExpeditionProgress.New();progress.Observe(run);
            var journal=ExpeditionJournal.New();journal.Observe(run);
            var history=new ExpeditionHistory();history.Observe(run);
            var save=new ExpeditionAccountSave{Version=2,HasRun=true,Run=run.Capture(),Progress=progress.Capture(),
                Journal=journal.Capture(),History=history.Capture(),TutorialMask=3,LessonMask=7};
            string json=JsonUtility.ToJson(save);JsonUtility.FromJson<ExpeditionAccountSave>(json).Validate(db);
            File.WriteAllText("Logs/v051-fixtures/wave-"+wave+".json",json);
        }
    }
}
