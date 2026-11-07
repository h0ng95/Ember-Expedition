using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class CombatSpriteEffectsValidation
    {
        [Serializable] sealed class Manifest { public Entry[] effects; }
        [Serializable] sealed class Entry { public string id; public int[] frameSize; public int frames; }
        static void Check(bool ok,string why){if(!ok)throw new Exception("Combat FX: "+why);}
        public static void ValidateAndBuild()
        { Validate(); PixelBattleEffectsValidation.Validate(); BattleNumberValidation.Validate(); AllyCombatPresentationValidation.Validate(); BossHitReactionValidation.Validate(); PrototypeSetup.BuildAndroid(); }
        public static void Validate()
        {
            const string root="Assets/Resources/Art/Effects/v071/";
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(root+"manifest.json"));
            Check(manifest.effects.Length==16,"16 sheets");
            foreach(var entry in manifest.effects)
            {
                string path=root+entry.id+".png";
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                Check(importer!=null && importer.filterMode==FilterMode.Point && !importer.mipmapEnabled &&
                    importer.wrapMode==TextureWrapMode.Clamp && !importer.isReadable &&
                    importer.textureCompression==TextureImporterCompression.Uncompressed,"import "+entry.id);
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    Check(texture.LoadImage(File.ReadAllBytes(path)),"decode "+entry.id);
                    int w=entry.frameSize[0],h=entry.frameSize[1];
                    Check(texture.width==w*4 && texture.height==h*2,"atlas size "+entry.id);
                    var pixels=texture.GetPixels32();
                    for(int f=0;f<8;f++)
                    {
                        int occupied=0;
                        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                        {
                            byte a=pixels[((f/4)*h+y)*texture.width+(f%4)*w+x].a;
                            if(x<2||x>=w-2||y<2||y>=h-2)Check(a==0,"cell seam "+entry.id+"/"+f);
                            if(a>0)occupied++;
                        }
                        Check(occupied>0,"empty "+entry.id+"/"+f);
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(texture);}
            }
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var enemyBattle=new CatalogBattle(db);
            var slinger=enemyBattle.AddEnemy("mine-slinger-1",100,10,0,2,3.6f,.55f,false,2,0,CatalogEnemyRole.Raider);
            var spitter=enemyBattle.AddEnemy("garden-spitter-1",100,10,0,2,3.6f,.55f,false,2,1,CatalogEnemyRole.GardenSpitter);
            Check(CombatEffectProfile.Basic(slinger)==CombatEffectStyle.Stone,"slinger must not inherit archer art from shared AI");
            Check(CombatEffectProfile.Basic(spitter)==CombatEffectStyle.Nature,"spitter must not use arrows");
            var type=typeof(CatalogCombatLab).Assembly.GetType("Expedition.Presentation.CatalogBattleEffects");
            int count=0;
            foreach(var group in CombatPresentationAudit.Groups)foreach(string form in group)
            {
                var go=new GameObject("Contact FX validation");var fx=Activator.CreateInstance(type,new object[]{go});
                try
                {
                    var battle=new CatalogBattle(db);
                    var actor=battle.AddUnit("actor",CatalogTeam.Ally,form,CombatPresentationAudit.Branch(form),null,0,0);
                    battle.AddEnemy("target",100000,1,0,100,1,.01f,false,1,0);
                    var list=(IList)type.GetField("effects",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(fx);
                    Emit(type,fx,battle,CatalogEventKind.BasicHit,"BASIC");
                    Emit(type,fx,battle,CatalogEventKind.SkillUsed,"SUPPORT");
                    Check(list.Count==0,"no premature slash on windup/support "+form);
                    Emit(type,fx,battle,CatalogEventKind.Damage,"BASIC");
                    Check(list.Count==1,"one contact record "+form);
                    string kind=list[0].GetType().GetField("Kind").GetValue(list[0]).ToString();
                    bool sword=form=="B-KN" || form=="E-KN-C" || form=="F-KN" || form=="H-01";
                    Check(kind==(sword?"Slash":"WeaponImpact"),"approved weapon identity "+form);
                    if(form=="H-02" || form=="E-KN-G")Check(CombatEffectProfile.Basic(actor)==CombatEffectStyle.Blunt,"hammer/shield "+form);
                    Emit(type,fx,battle,CatalogEventKind.Damage,"BURN");
                    Check(list[1].GetType().GetField("Kind").GetValue(list[1]).ToString()=="Impact","periodic damage stays small");
                    Emit(type,fx,battle,CatalogEventKind.ShieldBlocked,"BASIC");
                    Check(list[2].GetType().GetField("Kind").GetValue(list[2]).ToString()=="ShieldBlock","blocked distinct from grant");
                    for(int i=0;i<40;i++)Emit(type,fx,battle,CatalogEventKind.Damage,"BASIC");
                    Check(list.Count==24,"bounded burst "+form);
                    type.GetMethod("Tick").Invoke(fx,new object[]{1f,0f,battle});
                    Check((float)list[0].GetType().GetField("Age").GetValue(list[0])==0,"paused contact "+form);
                    count++;
                }
                finally{((IDisposable)fx).Dispose();UnityEngine.Object.DestroyImmediate(go);}
            }
            Debug.Log("COMBAT_FX_VALIDATION_PASS: 16 atlases /128 frames, "+count+" forms, contact routing, shields, pause, bounded records.");
        }
        static void Emit(Type type,object fx,CatalogBattle battle,CatalogEventKind kind,string ability)
        {
            var e=new CatalogBattleEvent();
            typeof(CatalogBattleEvent).GetProperty("Kind").SetValue(e,kind);
            typeof(CatalogBattleEvent).GetProperty("SourceId").SetValue(e,"actor");
            typeof(CatalogBattleEvent).GetProperty("TargetId").SetValue(e,"target");
            typeof(CatalogBattleEvent).GetProperty("AbilityId").SetValue(e,ability);
            type.GetMethod("Observe").Invoke(fx,new object[]{e,battle});
        }
    }
}
