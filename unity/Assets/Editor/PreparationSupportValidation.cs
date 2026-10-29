using System;
using System.IO;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class PreparationSupportValidation
    {
        static int checks;
        static void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Support preview: "+why);}
        public static void ValidateAndBuild()
        {
            checks=0;
            DragGrowthValidation.Validate();
            BattleReadabilityValidation.Validate();
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            Font font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},13);
            var name=new GUIStyle{font=font,fontSize=13,wordWrap=true,padding=new RectOffset(2,2,2,2)};
            var caption=new GUIStyle(name){fontSize=17,wordWrap=false};
            var rule=new GUIStyle(name){fontSize=16,wordWrap=true};
            try
            {
                foreach(var form in db.All)
                {
                    Check(name.CalcHeight(new GUIContent(form.Name),ExpeditionScreen.SupportLayout.Name(0).width)<=30,"name clipped: "+form.Name);
                    var support=CatalogPreparationPreview.ForSkill(form.Id);
                    if(support==null)continue;
                    rule.fontSize=14;
                    Check(rule.CalcHeight(new GUIContent(support.Rule),310)<=43,"target rule clipped: "+form.Id);
                }
                foreach(string text in new[]{"금색: 선택 / 공격   ·   청록: 지원 범위   ·   번호: 오른쪽 동료","동료를 눌러 범위 확인 · 끌어서 배치"})
                    Check(caption.CalcSize(new GUIContent(text)).x<=640,"board caption clipped");
                Check(caption.CalcSize(new GUIContent("동료 범위 안 4/4명  ·  모두 닿음")).x<=494,"summary clipped");
                for(int i=0;i<5;i++)
                {
                    Rect r=ExpeditionScreen.SupportLayout.Card(i);
                    Check(r.xMin>=908&&r.xMax<=1252&&r.yMin>=328&&r.yMax<=500,"card escapes panel or hits buttons");
                    Check(!ExpeditionScreen.SupportLayout.Name(i).Overlaps(ExpeditionScreen.SupportLayout.Status(i)),"name/status overlap");
                    if(i>0)Check(!r.Overlaps(ExpeditionScreen.SupportLayout.Card(i-1)),"recipient cards overlap");
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(font);}
            Directory.CreateDirectory("Logs/v052-fixtures");
            foreach(int wave in new[]{15,30})
            {
                var state=JsonUtility.FromJson<ExpeditionAccountSave>(File.ReadAllText("Logs/v051-fixtures/wave-"+wave+".json"));
                state.Run.Version=CatalogBalance.CurrentSaveVersion;state.Run.RulesVersion=CatalogBalance.CurrentRules;
                state.Run.UndoHistory=new ExpeditionPreparationSave[0];state.Validate(db);
                File.WriteAllText("Logs/v052-fixtures/wave-"+wave+".json",JsonUtility.ToJson(state));
            }
            Debug.Log("PASS support preview: "+checks+" layout checks plus drag/selection, tutorial and battle text regressions.");
            PrototypeSetup.BuildAndroid();
        }
    }
}
