using System;
using System.IO;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class HybridRolesValidation
    {
        public static void ValidateAndBuild()
        {
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            int checks = 0;
            Action<bool,string> check = (ok, why) => { checks++; if (!ok) throw new Exception("Unit roles: " + why); };
            Font font = Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},19);
            try
            {
                var text = new GUIStyle { font=font,fontSize=19,wordWrap=true };
                var tab = new GUIStyle { font=font,fontSize=18,wordWrap=false };
                Rect area = new Rect(162,212,956,278);
                float rightWidth = area.width-248-24;
                Rect tabs = new Rect(area.x+272,area.y+82,rightWidth,44);
                foreach (string label in new[]{"✓  기술","✓  특성 · 연구","✓  강점 · 주의점"})
                    check(tab.CalcSize(new GUIContent(label)).x+24 <= ExpeditionUiTheme.FooterButton(tabs,0,3).width,"tab clipped: "+label);
                for (int i=0;i<3;i++)
                {
                    Rect r = ExpeditionUiTheme.FooterButton(tabs,i,3);
                    check(r.xMin>=tabs.xMin && r.xMax<=tabs.xMax,"tab escapes panel");
                    if(i>0)check(!r.Overlaps(ExpeditionUiTheme.FooterButton(tabs,i-1,3)),"tabs overlap");
                }
                foreach(var form in db.All)
                {
                    string decision = CatalogUnitInfo.Decision(form.Id);
                    check(text.CalcHeight(new GUIContent(CatalogUnitInfo.Role(form.Id)),rightWidth)<=30,"role clipped: "+form.Id);
                    check(!string.IsNullOrEmpty(CatalogUnitInfo.Strength(form.Id)) && !string.IsNullOrEmpty(CatalogUnitInfo.Caution(form.Id)),"missing role: "+form.Id);
                    float height=text.CalcHeight(new GUIContent(decision),rightWidth-34)+8;
                    check(height>0 && !float.IsInfinity(height),"invalid scroll content: "+form.Id);
                    check(Resources.Load<Texture2D>("Art/Units/v010/"+form.Id)!=null,"portrait missing: "+form.Id);
                    if(form.Id.StartsWith("H-"))check(decision.Contains("기존 기술·고유 특성") && decision.Contains("다시 결합하지"),"missing fusion cost: "+form.Id);
                }
                Directory.CreateDirectory("Logs/v053-fixtures");
                var state = JsonUtility.FromJson<ExpeditionAccountSave>(File.ReadAllText("Logs/v052-fixtures/wave-15.json"));
                var units=state.Run.Roster.Members;
                units[1].FormId="E-AR-T";units[1].BranchId="E-AR-T";units[1].Growth=2;
                units[2].FormId="E-PR-H";units[2].BranchId="E-PR-H";units[2].Growth=2;
                units[3].FormId="E-BA-G";units[3].BranchId="E-BA-G";units[3].Growth=2;
                units[4].FormId="B-PR";units[4].BranchId=null;units[4].Growth=2;
                state.Run.UndoHistory=new ExpeditionPreparationSave[0];
                state.Validate(db);
                File.WriteAllText("Logs/v053-fixtures/wave-15.json",JsonUtility.ToJson(state));
            }
            finally { UnityEngine.Object.DestroyImmediate(font); }
            Debug.Log("PASS hybrid roles: "+checks+" checks across 30 unit profiles and fusion tabs; QA checkpoint exported.");
            DragGrowthValidation.Validate();
            BattleReadabilityValidation.Validate();
            PrototypeSetup.BuildAndroid();
        }
    }
}
