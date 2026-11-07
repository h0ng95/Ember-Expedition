using UnityEngine;

namespace Expedition.Presentation
{
    // Small cached offline atlases. No per-hit GameObjects, particles or texture allocations.
    internal sealed class CombatSpriteEffects
    {
        sealed class Sheet
        {
            public readonly string Id;
            public readonly Vector2 Pivot;
            public readonly float Width, Height, Duration;
            public Texture2D Texture;
            public bool Loaded;
            public Sheet(string id, float w, float h, float x, float y, float duration)
            { Id=id; Width=w; Height=h; Pivot=new Vector2(x,y); Duration=duration; }
            public bool Load()
            {
                if (!Loaded) { Loaded=true; Texture=Resources.Load<Texture2D>("Art/Effects/v071/"+Id); }
                return Texture != null;
            }
        }
        readonly Sheet arrow=new Sheet("arrow",112,32,92,16,.6f);
        readonly Sheet fire=new Sheet("firebolt",136,56,112,28,1.2f);
        readonly Sheet ice=new Sheet("icebolt",176,64,146,32,.8f);
        readonly Sheet holy=new Sheet("holybolt",64,32,48,16,1f);
        readonly Sheet venom=new Sheet("venombolt",64,32,48,16,1f);
        readonly Sheet stone=new Sheet("stone",64,32,48,16,1f);
        readonly Sheet blunt=new Sheet("blunt-hit",128,96,64,48,.25f);
        readonly Sheet pierce=new Sheet("pierce-hit",80,80,40,40,.22f);
        readonly Sheet frostHit=new Sheet("ice-hit",80,80,40,40,.28f);
        readonly Sheet holyHit=new Sheet("holy-hit",80,80,40,40,.25f);
        readonly Sheet venomHit=new Sheet("venom-hit",80,80,40,40,.25f);
        readonly Sheet fireHit=new Sheet("fire-hit",80,96,40,58,.28f);
        readonly Sheet claw=new Sheet("claw-hit",80,80,40,40,.20f);
        readonly Sheet shield=new Sheet("shield-block",80,80,40,40,.25f);
        readonly Sheet ground=new Sheet("ground-contact",80,48,40,28,.32f);
        readonly Sheet charge=new Sheet("ground-charge",80,48,40,28,1f);

        Sheet HitSheet(CombatEffectStyle style)
        {
            switch (style)
            {
                case CombatEffectStyle.Arrow: case CombatEffectStyle.Pierce: return pierce;
                case CombatEffectStyle.FrostArrow: case CombatEffectStyle.Ice: return frostHit;
                case CombatEffectStyle.Fire: return fireHit;
                case CombatEffectStyle.Holy: return holyHit;
                case CombatEffectStyle.Nature: return venomHit;
                case CombatEffectStyle.Claw: return claw;
                default: return blunt;
            }
        }
        internal float HitDuration(CombatEffectStyle style) { return HitSheet(style).Duration; }

        internal bool Impact(CombatEffectStyle style, Vector2 point, bool strong, float progress, int direction)
        {
            Sheet sheet=HitSheet(style);
            float scale=style==CombatEffectStyle.Blunt || style==CombatEffectStyle.Stone ? 1.25f : 1.35f;
            if(strong)scale*=1.15f;
            return Frame(sheet,point,scale,progress,direction<0,Color.white);
        }
        internal bool Block(Vector2 point,float progress,int direction)
        { return Frame(shield,point,1.2f,progress,direction<0,Color.white); }

        internal bool Projectile(CombatEffectStyle style,Vector2 from,Vector2 to,float progress,float battleTime)
        {
            Sheet sheet; float scale;
            switch(style)
            {
                case CombatEffectStyle.Arrow: case CombatEffectStyle.FrostArrow: sheet=arrow;scale=.95f;break;
                case CombatEffectStyle.Fire: sheet=fire;scale=.9f;break;
                case CombatEffectStyle.Ice: sheet=ice;scale=.7f;break;
                case CombatEffectStyle.Holy: sheet=holy;scale=1.4f;break;
                case CombatEffectStyle.Nature: sheet=venom;scale=1.4f;break;
                default: sheet=stone;scale=1.4f;break;
            }
            if(!sheet.Load())return false;
            Vector2 delta=to-from,point=Vector2.Lerp(from,to,progress);
            // Grow the tail only after it has travelled: no trail behind the firing unit.
            float tail=delta.magnitude*Mathf.Clamp01(progress)/scale;
            if(GamePreferences.Current.ReduceMotion)tail=Mathf.Min(tail,style==CombatEffectStyle.Arrow ||
                style==CombatEffectStyle.FrostArrow ? 48f : 20f);
            float crop=Mathf.Max(0,sheet.Pivot.x-tail);
            float frame=GamePreferences.Current.ReduceMotion ? .25f : Mathf.Repeat(battleTime/sheet.Duration,1f);
            Matrix4x4 saved=GUI.matrix;
            GUI.matrix=saved*Matrix4x4.TRS(new Vector3(point.x,point.y,0),
                Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg),Vector3.one);
            bool drawn=Frame(sheet,Vector2.zero,scale,frame,false,
                style==CombatEffectStyle.FrostArrow ? new Color(.74f,.88f,1f) : Color.white,crop);
            GUI.matrix=saved;
            return drawn;
        }

        internal void GroundContact(Vector2 center,float radiusPixels,float age,Color tint)
        {
            if(age>=ground.Duration)return;
            // The outermost fragments stay inside the existing telegraph's world radius.
            Frame(ground,center,Mathf.Max(0,radiusPixels)/40f,age/ground.Duration,false,tint);
        }
        internal void GroundCharge(Vector2 center,float radiusPixels,float battleTime,Color tint)
        {
            float progress=GamePreferences.Current.ReduceMotion ? .4f : Mathf.Repeat(battleTime*1.5f,1f);
            Frame(charge,center,Mathf.Max(0,radiusPixels)/40f,progress,false,tint);
        }

        static bool Frame(Sheet sheet,Vector2 point,float scale,float progress,bool flip,Color tint,float cropLeft=0)
        {
            if(!sheet.Load())return false;
            int frame=Mathf.Clamp(Mathf.FloorToInt(progress*8),0,7);
            float w=sheet.Width-cropLeft;
            var rect=new Rect(point.x-(flip ? sheet.Width-sheet.Pivot.x : sheet.Pivot.x-cropLeft)*scale,
                point.y-sheet.Pivot.y*scale,w*scale,sheet.Height*scale);
            var uv=new Rect((frame%4)*.25f+cropLeft/(sheet.Width*4),1f-(frame/4+1)*.5f,w/(sheet.Width*4),.5f);
            if(flip){uv.x+=uv.width;uv.width=-uv.width;}
            Color saved=GUI.color;
            float dim=GamePreferences.Current.ReduceFlashes ? .65f : 1f;
            GUI.color=saved*tint*new Color(dim,dim,dim,1);
            GUI.DrawTextureWithTexCoords(rect,sheet.Texture,uv,true);
            GUI.color=saved;
            return true;
        }
    }
}
