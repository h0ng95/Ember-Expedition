using System;
using System.IO;
using System.Collections;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class PixelBattleEffectsValidation
    {
        const string Root = "Assets/Resources/Art/Effects/v070/";
        static void Check(bool ok, string why) { if (!ok) throw new Exception("PixelFX: " + why); }
        public static void ValidateAndBuild() { Validate(); PrototypeSetup.BuildAndroid(); }
        [MenuItem("Expedition/Validate Pixel Battle Effects")]
        public static void Validate()
        {
            Sheet("sword-hit",144,128,8); Sheet("healing",80,104,16); Sheet("frozen",64,64,16);
            // Healing must not replace shields; paused effects must not age.
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var battle=new CatalogBattle(db);
            battle.AddUnit("a",CatalogTeam.Ally,"B-KN",null,null,0,0);
            battle.AddUnit("b",CatalogTeam.Ally,"B-PR",null,null,-1,0);
            var type=typeof(CatalogCombatLab).Assembly.GetType("Expedition.Presentation.CatalogBattleEffects");
            var go=new GameObject("PixelFX validation");
            var fx=Activator.CreateInstance(type,new object[]{go});
            try
            {
                foreach (var kind in new[]{CatalogEventKind.Heal,CatalogEventKind.Shield})
                {
                    var e=new CatalogBattleEvent();
                    typeof(CatalogBattleEvent).GetProperty("Kind").SetValue(e,kind);
                    typeof(CatalogBattleEvent).GetProperty("SourceId").SetValue(e,"b");
                    typeof(CatalogBattleEvent).GetProperty("TargetId").SetValue(e,"a");
                    type.GetMethod("Observe").Invoke(fx,new object[]{e,battle});
                }
                var effects=(IList)type.GetField("effects",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(fx);
                Check(effects.Count==2,"heal and shield records");
                Check(effects[0].GetType().GetField("Kind").GetValue(effects[0]).ToString()=="Heal","healing routing");
                Check(effects[1].GetType().GetField("Kind").GetValue(effects[1]).ToString()=="Support","shield kept distinct");
                type.GetMethod("Tick").Invoke(fx,new object[]{1f,0f,battle});
                Check((float)effects[0].GetType().GetField("Age").GetValue(effects[0])==0f,"pause freezes playback");
                type.GetMethod("Tick").Invoke(fx,new object[]{1f,1f,battle});
                Check(effects.Count==0,"expired effects removed");
            }
            finally { ((IDisposable)fx).Dispose(); UnityEngine.Object.DestroyImmediate(go); }
            Debug.Log("PIXELFX_VALIDATION_PASS: 40 occupied frames, transparent gutters, point imports, heal/shield separation, pause and expiry.");
        }
        static void Sheet(string id,int w,int h,int count)
        {
            string path=Root+id+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            Check(importer!=null && importer.filterMode==FilterMode.Point && !importer.mipmapEnabled &&
                importer.wrapMode==TextureWrapMode.Clamp && !importer.isReadable && importer.maxTextureSize==1024 &&
                importer.alphaSource==TextureImporterAlphaSource.FromInput &&
                importer.textureCompression==TextureImporterCompression.Uncompressed,"import "+id);
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                Check(texture.LoadImage(File.ReadAllBytes(path)),"decode "+id);
                Check(texture.width==w*4 && texture.height==h*(count/4),"atlas dimensions "+id);
                var pixels=texture.GetPixels32();
                for(int f=0;f<count;f++)
                {
                    int visible=0;
                    for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                    {
                        int a=pixels[((f/4)*h+y)*texture.width+(f%4)*w+x].a;
                        if(x<2||y<2||x>=w-2||y>=h-2)Check(a==0,"frame seam "+id+"/"+f);
                        if(a>0)visible++;
                    }
                    Check(visible>0,"empty frame "+id+"/"+f);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
