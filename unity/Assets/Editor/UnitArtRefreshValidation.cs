using System;
using System.IO;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class UnitArtRefreshValidation
    {
        [Serializable] sealed class Manifest { public Sheet[] entries; }
        [Serializable] sealed class Sheet { public string id,resourcePath; public int width,height; public Frame[] frames; }
        [Serializable] sealed class Frame { public float x,y,width,height,pivotX,pivotY; public Region[] regions; }
        [Serializable] sealed class Region { public float x,y,width,height; }
        static int checks;
        static void Check(bool ok,string why) { checks++;if(!ok)throw new Exception("Art refresh: "+why); }
        public static void ValidateAndBuild()
        {
            checks=0;
            var manifest=JsonUtility.FromJson<Manifest>(Resources.Load<TextAsset>("Art/Units/v010/art-manifest").text);
            int refreshed=0, continued=0;
            foreach(var s in manifest.entries)
            {
                var texture=Resources.Load<Texture2D>(s.resourcePath);
                // Existing platform imports may downsample; UV crops use source dimensions.
                Check(texture!=null && texture.width>0 && texture.height>0 &&
                    Mathf.Abs((float)texture.width/texture.height-(float)s.width/s.height)<.005f,"missing/aspect mismatch "+s.id);
                Check(s.frames.Length==22,"missing animation slots "+s.id);
                foreach(var f in s.frames)
                {
                    Check(f!=null && f.x>=0 && f.y>=0 && f.width>0 && f.height>0 &&
                        f.x+f.width<=s.width && f.y+f.height<=s.height && f.pivotX>=0 && f.pivotX<=1 && f.pivotY>=0 && f.pivotY<=1,"invalid crop "+s.id);
                    if(f.regions==null)continue;
                    foreach(var r in f.regions)Check(r.width>0 && r.height>0 && r.x>=0 && r.y>=0 &&
                        r.x+r.width<=f.width && r.y+r.height<=f.height,"invalid isolated region "+s.id);
                    for(int i=0;i<f.regions.Length;i++)for(int j=i+1;j<f.regions.Length;j++)
                    {
                        var a=f.regions[i];var b=f.regions[j];
                        Check(a.x+a.width<=b.x || b.x+b.width<=a.x || a.y+a.height<=b.y || b.y+b.height<=a.y,"overlapping regions "+s.id);
                    }
                }
                if(s.resourcePath.StartsWith("Art/Units/v054/"))refreshed++;
                if(s.resourcePath.StartsWith("Art/Units/v055/"))continued++;
                if(s.id=="B-KN")Check(s.resourcePath=="Art/Units/v010/B-KN","master knight must be preserved");
            }
            Check(refreshed==11,"expected11 refreshed sheets");
            Check(continued==18,"expected18 advanced sheets");
            Font font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},19);
            try
            {
                var style=new GUIStyle{font=font,fontSize=19,wordWrap=true,padding=new RectOffset(2,2,2,2)};
                Check(style.CalcHeight(new GUIContent("일반 · 전투 30 / 30"),365)<=34,"battle header clipped");
                style.fontSize=15;
                Check(style.CalcHeight(new GUIContent("남은 기회 3 / 3"),365)<=28,"remaining chances clipped");
                Check(style.CalcHeight(new GUIContent("격파 300,000 + 완주 30,000 + 남은 기회 3,000\n새 원정에서는 동료·골드·연구가 초기화됩니다."),916)<=64,"summary score clipped");
            }
            finally { UnityEngine.Object.DestroyImmediate(font); }
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            Directory.CreateDirectory("Logs/v055-fixtures");
            var state=JsonUtility.FromJson<ExpeditionAccountSave>(File.ReadAllText("Logs/v053-fixtures/wave-15.json"));
            var units=state.Run.Roster.Members;
            state.Run.UndoHistory=new ExpeditionPreparationSave[0];
            // Each fixture retains the original 19-unit investment and five active members.
            string[][] rosters={
                new[]{"E-KN-G","E-KN-C","E-AR-P","E-AR-T","F-AR"},
                new[]{"E-FI-B","E-FI-E","E-IC-F","E-IC-S","F-FI"},
                new[]{"E-BA-G","E-BA-A","F-BA","B-KN","B-PR"},
                new[]{"H-01","H-03","B-KN","B-PR","B-BA"},
                new[]{"H-04","H-06","B-KN","B-PR","B-BA"},
                new[]{"F-IC","E-KN-G","E-IC-F","E-BA-G","E-FI-B"}
            };
            for(int group=0;group<rosters.Length;group++)
            {
                for(int i=0;i<5;i++)
                {
                    string id=rosters[group][i];units[i].FormId=id;units[i].InheritedTraitId=null;units[i].Growth=0;
                    units[i].BranchId=id.StartsWith("E-")?id:id=="F-AR"?"E-AR-P":id=="F-FI"?"E-FI-B":id=="F-BA"?"E-BA-G":id=="F-IC"?"E-IC-F":null;
                    if(group==2 && i>=3)units[i].Growth=2;
                    if((group==3||group==4) && i>=2)units[i].Growth=i==2?2:1;
                }
                state.Validate(db);File.WriteAllText("Logs/v055-fixtures/art-"+group+".json",JsonUtility.ToJson(state));
            }
            AllyBodyScaleValidation.Validate();
            BattleReadabilityValidation.Validate();
            Debug.Log("PASS unit art refresh: "+checks+" checks;18 new +11 previous sheets and30 calibrated forms;6 art fixtures.");
            PrototypeSetup.BuildAndroid();
        }
    }
}
