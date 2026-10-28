using System.Collections.Generic;
using UnityEngine;

namespace Expedition.Presentation
{
    // Code-native pixel ornament and layout: no per-frame textures or borrowed artwork.
    public static class CombatHud
    {
        static readonly Color Edge=new Color(.055f,.045f,.05f), Metal=new Color(.48f,.35f,.20f), Light=new Color(.9f,.72f,.42f);
        public static readonly Color Ally=new Color(.3f,.79f,.66f), Enemy=new Color(.86f,.29f,.23f), Shield=new Color(.4f,.76f,1f);
        static void Fill(Rect r,Color c){ExpeditionUiTheme.Fill(r,c);}
        public sealed class HealthTrail
        {
            public float Value {get;private set;}
            float last=-1f,hold;
            public void Tick(float hp,float dt,bool reducedMotion)
            {
                hp=Mathf.Clamp01(hp);
                if(last<0f||hp>last||reducedMotion){Value=hp;hold=0;}
                else if(hp<last && Value<=last+.001f)hold=.20f;
                last=hp;
                if(hold>0)hold=Mathf.Max(0,hold-dt);
                else Value=Mathf.MoveTowards(Value,hp,Mathf.Max(.3f,(Value-hp)*2.8f)*dt);
                Value=Mathf.Clamp(Value,hp,1f);
            }
        }
        public static Rect PlaceNearby(Rect desired,List<Rect> occupied,Rect limits)
        {
            // Prefer the same column. Both axes stay bounded, even for edge crowds.
            Rect fallback=Clamp(desired,limits);float best=float.MaxValue;
            for(int row=0;row<17;row++)
            for(int column=0;column<3;column++)
            {
                int vertical=row==0?0:((row+1)/2)*(row%2==1?-1:1);
                Rect r=Clamp(desired,limits);r.y+=vertical*(desired.height+3);
                r.x+=(column==0?0:column==1?-1:1)*(desired.width+5);
                r=Clamp(r,limits);float overlap=0;
                foreach(Rect used in occupied)if(r.Overlaps(used))
                    overlap+=Mathf.Max(0,Mathf.Min(r.xMax,used.xMax)-Mathf.Max(r.x,used.x))*Mathf.Max(0,Mathf.Min(r.yMax,used.yMax)-Mathf.Max(r.y,used.y));
                if(overlap==0)return r;
                if(overlap<best){best=overlap;fallback=r;}
            }
            return fallback;
        }
        static Rect Clamp(Rect r,Rect limits)
        {r.x=Mathf.Clamp(r.x,limits.x,limits.xMax-r.width);r.y=Mathf.Clamp(r.y,limits.y,limits.yMax-r.height);return r;}
        public static void Meter(Rect r,float hp,float trail,float shield,bool ally,bool boss=false)
        {
            Fill(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),Edge);
            Fill(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),boss?Metal:new Color(.29f,.29f,.30f));
            Fill(new Rect(r.x-1,r.y-1,r.width+2,r.height+2),Edge);
            Fill(r,new Color(.13f,.075f,.075f));
            Fill(new Rect(r.x,r.y,r.width*Mathf.Clamp01(trail),r.height),new Color(.91f,.68f,.38f));
            float w=r.width*Mathf.Clamp01(hp);Color color=ally?Ally:Enemy;
            if(w>0)
            {
                Fill(new Rect(r.x,r.y,w,r.height),color*.73f);
                Fill(new Rect(r.x,r.y,w,r.height*.62f),color);
                Fill(new Rect(r.x,r.y,w,boss?2:1),Color.Lerp(color,Color.white,.38f));
            }
            if(shield>0)Fill(new Rect(r.x,r.yMax+3,r.width*Mathf.Clamp01(shield),boss?3:2),Shield);
            if(boss)
            {
                for(int i=1;i<4;i++)Fill(new Rect(r.x+r.width*i/4f,r.y+2,1,r.height-4),new Color(.08f,.025f,.02f,.3f));
                Crest(new Vector2(r.x-13,r.center.y),false);Crest(new Vector2(r.xMax+13,r.center.y),false);
            }
            else
            {
                // Team shape as well as color: shield on allies, slash on enemies.
                float x=r.x-9;
                if(ally){Fill(new Rect(x,r.y,5,5),Ally);Fill(new Rect(x+1,r.y+5,3,2),Ally);}
                else{Fill(new Rect(x+3,r.y,2,3),Enemy);Fill(new Rect(x+1,r.y+2,2,3),Enemy);Fill(new Rect(x,r.y+4,2,3),Enemy);}
            }
        }
        public static void Crest(Vector2 p,bool crown)
        {
            if(crown)
            {
                Fill(new Rect(p.x-11,p.y-5,4,10),Metal);Fill(new Rect(p.x-2,p.y-9,4,14),Light);Fill(new Rect(p.x+7,p.y-5,4,10),Metal);
                Fill(new Rect(p.x-9,p.y+3,18,4),Light);Fill(new Rect(p.x-7,p.y+7,14,2),Metal);
                return;
            }
            Fill(new Rect(p.x-8,p.y-4,16,8),Edge);Fill(new Rect(p.x-4,p.y-8,8,16),Edge);
            Fill(new Rect(p.x-6,p.y-2,12,4),Metal);Fill(new Rect(p.x-2,p.y-6,4,12),Metal);
            Fill(new Rect(p.x-3,p.y-3,6,6),Light);Fill(new Rect(p.x-1,p.y-1,2,2),Edge);
        }
        static readonly string[][] Glyphs={
            new[]{"...x...",".x.x.x.","..xxx..","xxxxxxx","..xxx..",".x.x.x.","...x..."},
            new[]{"....x..","...xx..",".x.xxx.",".xxxxx.","xxxxxxx","xxoxxxx",".xxxxx."},
            new[]{"..xxx..",".xxxxx.","xxoxoxx","xxxxxxx",".xxxxx.","..x.x..",".x...x."}
        };
        public static Color StatusColor(int kind){return kind==0?new Color(.55f,.87f,1):kind==1?new Color(1,.56f,.24f):new Color(.68f,.87f,.34f);}
        public static void StatusIcon(Rect r,int kind,float remaining)
        {
            Color c=StatusColor(kind);Fill(r,Edge);Fill(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),new Color(c.r*.18f,c.g*.18f,c.b*.18f));
            float pixel=2;Vector2 origin=new Vector2(Mathf.Round(r.center.x-7),Mathf.Round(r.center.y-7));
            for(int y=0;y<7;y++)for(int x=0;x<7;x++)
                if(Glyphs[kind][y][x]!='.')Fill(new Rect(origin.x+x*pixel,origin.y+y*pixel,pixel,pixel),Glyphs[kind][y][x]=='o'?Light:c);
            Fill(new Rect(r.x+2,r.yMax-2,(r.width-4)*Mathf.Clamp01(remaining/5f),1),c);
        }
    }
}
