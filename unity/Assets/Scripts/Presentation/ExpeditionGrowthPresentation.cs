using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        bool growthDetails, revealCombination;
        string growthPreviewKey, revealBefore, revealDonor, revealResult;
        float revealStarted = -10;
        GUIStyle growthName, growthRole;
        bool GrowthRevealActive { get { return revealResult != null && Time.unscaledTime - revealStarted < 1.45f; } }

        void EnsureGrowthStyles()
        {
            EnsureInfoStyles();EnsurePreparationArt();
            if(growthName!=null)return;
            growthName=Style(29,Gold,true,TextAnchor.MiddleLeft);
            growthRole=Style(18,Cream,false,TextAnchor.UpperLeft);
        }

        void DrawGrowthOverview(Rect area, ExpeditionUnitPreview preview, CatalogMember donor)
        {
            EnsureGrowthStyles();
            CatalogMember original=run.Roster.Find(selectedId);
            float y=area.y;
            GUI.Label(new Rect(area.x,y+7,310,30),donor==null?"현재 동료":"결합할 동료",hint);
            DrawGrowthIngredient(original.FormId,new Rect(area.x+6,y+42,donor==null?260:146,204));
            if(donor!=null)
            {
                GUI.Label(new Rect(area.x+146,y+96,28,35),"+",heading);
                DrawGrowthIngredient(donor.FormId,new Rect(area.x+174,y+42,146,204));
            }
            float arrow=area.x+340;
            Fill(new Rect(arrow,y+125,34,5),Gold);
            for(int i=0;i<5;i++)Fill(new Rect(arrow+28+i*3,y+113+i*3,3,29-i*6),Gold);
            Rect stage=new Rect(area.x+392,y,246,272);
            Fill(stage,new Color(.12f,.13f,.16f));
            ExpeditionUiTheme.Rule(new Rect(stage.x,stage.yMax-2,stage.width,2));
            PreviewTexture(preparationDisc,new Rect(stage.x+22,stage.y+36,202,204),new Color(.95f,.65f,.24f,.13f));
            PreviewTexture(preparationRing,new Rect(stage.x+24,stage.y+214,198,28),new Color(.95f,.72f,.37f,.65f));
            if(art!=null)art.DrawPreview(preview.Member.FormId,new Rect(stage.x+12,stage.y+2,222,238),Time.unscaledTime);
            GUI.Label(new Rect(stage.x+12,stage.y+240,222,28),"새로운 모습",centered);
            float tx=area.x+664,tw=area.width-664;
            GUI.Label(new Rect(tx,y+8,tw,76),database.Get(preview.Member.FormId).Name,growthName);
            GUI.Label(new Rect(tx,y+88,tw,58),CatalogUnitInfo.Role(preview.Member.FormId),growthRole);
            ExpeditionUiTheme.Rule(new Rect(tx,y+157,tw,2));
            GUI.Label(new Rect(tx,y+164,tw,108),GrowthOverviewText(preview.Member.FormId),growthRole);
        }

        static string GrowthOverviewText(string id)
        {
            string summary=CatalogUnitInfo.Summary(id), role=CatalogUnitInfo.Role(id);
            string placement=id=="E-KN-C"?"앞줄 · 보호막 지원을 받도록 배치":CatalogUnitInfo.Placement(id);
            return summary==role?placement:summary+"\n"+placement;
        }

        void DrawGrowthIngredient(string form,Rect card)
        {
            // Space is reserved around the body for long weapons/flags.
            if(art!=null)art.Draw(form,new Rect(card.center.x-70,card.y,140,151),(int)(Time.unscaledTime*5)%4);
            GUI.Label(new Rect(card.x,card.y+157,card.width,45),database.Get(form).Name,centered);
        }

        void BeginGrowthReveal(string before,string donor,string result,bool combine)
        {
            // The run was already committed and saved. This overlay never applies growth.
            revealBefore=before;revealDonor=combine?donor:null;revealResult=result;
            revealCombination=combine;revealStarted=Time.unscaledTime;toast=null;
            CancelPointer();SelectPreparation(selectedId);
        }

        void DrawGrowthReveal()
        {
            EnsureGrowthStyles();
            float elapsed=Time.unscaledTime-revealStarted;
            bool reduce=GamePreferences.Current.ReduceMotion;
            Fill(new Rect(0,0,Width,Height),new Color(.025f,.03f,.045f,.91f));
            GUI.Label(new Rect(290,88,700,52),revealCombination?"결합 완성":"새로운 진화",StyleForRevealTitle());
            float amount=reduce?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.55f));
            if(!reduce && elapsed<.5f)
            {
                float offset=Mathf.Lerp(220,38,amount);
                Color old=GUI.color;GUI.color=new Color(1,1,1,1-amount);
                if(art!=null){art.Draw(revealBefore,new Rect(640-offset-80,270,160,184),0);if(revealDonor!=null)art.Draw(revealDonor,new Rect(640+offset-80,270,160,184),0);}
                GUI.color=old;
            }
            float appear=reduce?1:Mathf.Clamp01((elapsed-.3f)/.25f);
            Color tint=GUI.color;GUI.color=new Color(1,1,1,appear);
            if(!GamePreferences.Current.ReduceFlashes)
            {
                PreviewTexture(preparationDisc,new Rect(488,191,304,290),new Color(1,.65f,.2f,.1f*appear));
                for(int i=0;i<18;i++)
                {
                    float angle=i*Mathf.PI*2/18f,rad=100+(reduce?0:elapsed*36);
                    Vector2 p=new Vector2(640+Mathf.Cos(angle)*rad,350+Mathf.Sin(angle)*rad*.75f);
                    Fill(new Rect(p.x,p.y,4+i%2*2,4+i%2*2),new Color(1,.79f,.4f,appear*.65f));
                }
            }
            if(art!=null)art.DrawPreview(revealResult,new Rect(510,185+(reduce?0:(1-appear)*12),260,290),Mathf.Max(0,elapsed-.4f));
            GUI.Label(new Rect(310,492,660,50),database.Get(revealResult).Name,StyleForRevealTitle());
            GUI.Label(new Rect(310,552,660,40),CatalogUnitInfo.Role(revealResult),centered);
            GUI.color=tint;
            GUI.Label(new Rect(380,640,520,30),"탭하여 계속 · 준비 중 되돌리기 가능",small);
            if(elapsed>.15f && Event.current.type==EventType.MouseDown)
            {revealResult=null;Event.current.Use();}
        }
        GUIStyle revealTitle;
        GUIStyle StyleForRevealTitle()
        { if(revealTitle==null)revealTitle=Style(32,Gold,true,TextAnchor.MiddleCenter);return revealTitle; }
    }
}
