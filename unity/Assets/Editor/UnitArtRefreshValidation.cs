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
            int refreshed=0;
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
                if(s.id=="B-KN")Check(s.resourcePath=="Art/Units/v010/B-KN","master knight must be preserved");
            }
            Check(refreshed==11,"expected11 refreshed sheets");
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
            Directory.CreateDirectory("Logs/v054-fixtures");
            var state=JsonUtility.FromJson<ExpeditionAccountSave>(File.ReadAllText("Logs/v053-fixtures/wave-15.json"));
            var units=state.Run.Roster.Members;
            string[] ids={"F-KN","H-02","E-PR-G","B-BA","B-PR"};
            for(int i=0;i<5;i++){units[i].FormId=ids[i];units[i].InheritedTraitId=null;units[i].BranchId=i==0?"E-KN-G":i==2?"E-PR-G":null;units[i].Growth=i==4?1:0;}
            // 7+6+3+1+2 = 19 original recruited units; preserve gold and supply accounting.
            state.Run.UndoHistory=new ExpeditionPreparationSave[0];
            state.Validate(db);File.WriteAllText("Logs/v054-fixtures/art-advanced.json",JsonUtility.ToJson(state));
            string[] basics={"B-KN","B-AR","B-FI","B-PR","B-BA"};
            for(int i=0;i<5;i++){units[i].FormId=basics[i];units[i].BranchId=null;units[i].Growth=i==4?2:3;}
            state.Validate(db);File.WriteAllText("Logs/v054-fixtures/art-base.json",JsonUtility.ToJson(state));
            units[1].FormId="B-IC";
            state.Validate(db);File.WriteAllText("Logs/v054-fixtures/art-frost.json",JsonUtility.ToJson(state));
            AllyBodyScaleValidation.Validate();
            BattleReadabilityValidation.Validate();
            Debug.Log("PASS unit art refresh: "+checks+" checks;11 new sheets and30 calibrated forms.");
            PrototypeSetup.BuildAndroid();
        }
    }
}
