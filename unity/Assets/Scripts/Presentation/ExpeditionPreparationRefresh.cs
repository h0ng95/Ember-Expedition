using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        // Drawing, touch, range projection and tutorial holes share these coordinates.
        public static class PreparationLayout
        {
            public const float OriginX = 824, ScaleX = 112, OriginY = 440, ScaleY = 40;
            public static readonly Rect Board = new Rect(32, 200, 850, 354);
            public static readonly Rect Sidebar = new Rect(908, 104, 344, 458);
            public static readonly Rect Summon = new Rect(960, 584, 294, 54);
            public static readonly Rect Battle = new Rect(960, 646, 294, 58);
            public static readonly Rect Research = new Rect(1070, 17, 188, 46);
            public static Rect Bench(int i) { return new Rect(140 + i * 160, 592, 150, 110); }
            public static Vector2 Point(float x, float y) { return new Vector2(OriginX + x * ScaleX, OriginY - y * ScaleY); }
            public static Vector2 World(Vector2 p) { return new Vector2(Mathf.Clamp((p.x-OriginX)/ScaleX,-6.2f,-.2f),Mathf.Clamp((OriginY-p.y)/ScaleY,-2.5f,2.2f)); }
            public static Rect Sprite(Vector2 p) { return new Rect(p.x-62,p.y-146,124,143); }
            public static Rect Hit(Vector2 p) { return new Rect(p.x-64,p.y-146,128,166); }
        }

        GUIStyle prepAdvice, prepSmall;
        void EnsureRefreshStyles()
        {
            EnsurePreparationArt();
            if (prepAdvice != null) return;
            prepAdvice = Style(17, Cream, false, TextAnchor.UpperLeft);
            prepSmall = Style(14, Muted, false, TextAnchor.UpperLeft);
        }

        void DrawPreparationBoard()
        {
            EnsureRefreshStyles();
            DrawHeader(run.IsBossWave ? "보스전 준비" : "전투 준비");
            Texture2D region = run.Wave <= 5 ? battlefield : run.Wave <= 10 ? outpost :
                run.Wave <= 15 ? furnace : run.Wave <= 20 ? frost : run.Wave <= 25 ? garden : palace;
            ExpeditionUiTheme.DrawBackdrop(new Rect(16,94,1248,468),region,false);
            Fill(new Rect(24,100,866,98),new Color(.035f,.045f,.06f,.55f));
            GUI.Label(new Rect(40,108,280,38),"원정대  " + run.DeployedIds.Count + " / 5",heading);
            DrawTutorialReplayButton();
            var combinations=run.GetCombinationOptions();
            if(combinations.Count>0 && GUI.Button(new Rect(506,110,204,44),"결합 가능  " + combinations.Count,primary)) OpenCombinationOptions();
            GUI.enabled=modal==Modal.None && !GrowthRevealActive && run.CanUndo;
            if(GUI.Button(new Rect(728,110,144,44),"되돌리기",button))
            {
                if(run.Undo()){selectedId=null;SelectPreparation(null);Save("undo");Notice("직전 준비 행동을 되돌렸습니다.");}
                else Notice("대기석 공간이 부족하거나 되돌릴 행동이 없습니다.");
            }
            GUI.enabled=modal==Modal.None && !GrowthRevealActive;
            DrawPreparationRange();
            // Small lane labels stay behind the units, not across their bodies.
            GUI.Label(new Rect(85,218,150,25),"후열 · 지원",small);
            GUI.Label(new Rect(710,218,150,25),"전열 · 방어",small);
            fieldMembers.Clear();
            foreach(string id in run.DeployedIds){var member=run.Roster.Find(id);if(member!=null)fieldMembers.Add(member);}
            fieldMembers.Sort((a,b)=>b.Y.CompareTo(a.Y));
            foreach(var member in fieldMembers)
            {
                Vector2 foot=ArenaPoint(member);
                DrawUnit(member,PreparationLayout.Sprite(foot),new Rect());
                DrawPreparationMarker(member,foot);
                if(PreparationSelection()==null)
                {
                    int n=0;foreach(string id in run.DeployedIds){n++;if(id==member.Id)break;}
                    Fill(new Rect(foot.x-12,foot.y+1,24,24),new Color(.04f,.05f,.07f,.85f));
                    GUI.Label(new Rect(foot.x-12,foot.y,24,26),n.ToString(),centered);
                }
            }
            DrawPreparationSidebar();
            DrawCompactDock();
            if(pointerDragging && pointerId!=null)
            {
                DrawGrowthPreview();
                var carried=run.Roster.Find(pointerId);
                if(carried!=null && art!=null)art.Draw(carried.FormId,PreparationLayout.Sprite(pointerCurrent),(int)(Time.unscaledTime*5)%4);
            }
        }

        void DrawPreparationSidebar()
        {
            ExpeditionUiTheme.PanelBox(PreparationLayout.Sidebar);
            CatalogMember member=PreparationSelection();
            if(member!=null && CurrentTutorialStep()==ExpeditionTutorialStep.None)
            {
                GUI.Label(new Rect(924,117,312,42),database.Get(member.FormId).Name,heading);
                GUI.Label(new Rect(924,162,310,40),CatalogUnitInfo.Role(member.FormId),hint);
                if(GUI.Button(new Rect(924,209,150,40),"동료 상세",button)){selectedId=member.Id;modal=Modal.Detail;}
                if(GUI.Button(new Rect(1086,209,150,40),"선택 해제",button)){SelectPreparation(null);return;}
                DrawCompactSupport(member);
                return;
            }
            GUI.Label(new Rect(924,116,310,28),"NEXT / " + (run.IsBossWave?"보스전":"일반전"),hint);
            GUI.Label(new Rect(924,151,310,64),run.EncounterName,heading);
            ExpeditionUiTheme.Rule(new Rect(924,222,312,2));
            GUI.Label(new Rect(924,232,310,27),"주의할 패턴",hint);
            // Compact hints use sentence breaks without leaving a lone full stop on a line.
            GUI.Label(new Rect(924,269,310,114),PreparationAdvice().Replace(".\n","\n").TrimEnd('.'),prepAdvice);
            GUI.Label(new Rect(924,387,310,27),"적 편성",small);
            GUI.Label(new Rect(924,418,310,65),run.EncounterDescription,prepSmall);
            if(GUI.Button(new Rect(924,498,312,46),"공략 · 지역 이야기",button))modal=Modal.Encounter;
        }

        void DrawCompactSupport(CatalogMember member)
        {
            var primarySupport=CatalogPreparationPreview.ForSkill(CatalogPreparationPreview.PrimarySkill(member));
            bool two=member.FormId.StartsWith("F-")&&primarySupport!=null&&CatalogPreparationPreview.ForSkill(member.FormId)!=null;
            if(two)
            {
                if(GUI.Button(new Rect(924,258,150,36),"주 기술",!previewFinalSkill?primary:button))previewFinalSkill=false;
                if(GUI.Button(new Rect(1086,258,150,36),"최종 기술",previewFinalSkill?primary:button))previewFinalSkill=true;
            }
            var support=SelectedSupport(member);
            if(support==null)
            {
                GUI.Label(new Rect(924,269,310,36),"기본 사거리  " + database.Get(member.FormId).Range.ToString("0.0"),hint);
                GUI.Label(new Rect(924,322,310,68),CatalogUnitInfo.Placement(member.FormId),body);
                GUI.Label(new Rect(924,410,310,108),"금색 원은 기본 공격 범위입니다.\n기술과 특성의 조건은 동료 상세에서 확인하세요.",prepSmall);
                return;
            }
            int inside=0;
            foreach(string id in run.DeployedIds){var target=run.Roster.Find(id);if(target!=member&&IsSupportCandidate(member,target,support))inside++;}
            GUI.Label(new Rect(924,two?299:267,310,28),"지원 범위 안  " + inside + " / " + (run.DeployedIds.Count-1) + "명",hint);
            for(int i=0;i<run.DeployedIds.Count;i++)
            {
                var target=run.Roster.Find(run.DeployedIds[i]);bool self=target==member,eligible=IsSupportCandidate(member,target,support);
                Color color=self?Gold:eligible?SupportColor:OutsideColor;
                Rect r=SupportLayout.Card(i);Fill(r,new Color(color.r*.13f,color.g*.13f,color.b*.13f,.95f));
                Fill(new Rect(r.x,r.y,3,r.height),color);
                supportNumber.normal.textColor=color;GUI.Label(new Rect(r.x+5,r.y+4,20,24),(i+1).ToString(),supportNumber);
                if(art!=null)art.Draw(target.FormId,new Rect(r.x+29,r.y+1,33,30),0);
                GUI.Label(SupportLayout.Name(i),database.Get(target.FormId).Name,supportName);
                supportStatus.normal.textColor=color;GUI.Label(SupportLayout.Status(i),self?"자신":eligible?"범위 안":"범위 밖",supportStatus);
            }
            GUI.Label(new Rect(924,511,310,43),support.Rule,prepSmall);
        }

        void DrawCompactDock()
        {
            Fill(new Rect(0,574,Width,146),new Color(.055f,.064f,.08f,.98f));
            ExpeditionUiTheme.Rule(new Rect(16,574,1248,2));
            bench.Clear();foreach(var member in run.Roster.Members)if(!IsDeployed(member.Id))bench.Add(member);
            GUI.Label(new Rect(24,588,116,32),"대기 " + bench.Count + "/7",body);
            int pages=Mathf.Max(1,(bench.Count+4)/5);benchPage=Mathf.Clamp(benchPage,0,pages-1);
            if(pages>1)
            {
                if(GUI.Button(new Rect(24,632,48,48),"‹",button))benchPage=(benchPage+pages-1)%pages;
                if(GUI.Button(new Rect(79,632,48,48),"›",button))benchPage=(benchPage+1)%pages;
                GUI.Label(new Rect(24,684,100,25),(benchPage+1)+" / "+pages,small);
            }
            if(bench.Count==0)GUI.Label(new Rect(170,618,720,56),"소환한 동료가 이곳에 합류합니다.\n끌어서 출전시키거나, 동료에게 겹쳐 성장시켜 보세요.",small);
            for(int i=0;i<5&&benchPage*5+i<bench.Count;i++)
            {
                Rect r=BenchRect(i);Fill(r,new Color(.12f,.14f,.18f));ExpeditionUiTheme.Rule(new Rect(r.x,r.yMax-2,r.width,2));
                var member=bench[benchPage*5+i];DrawUnit(member,new Rect(r.x+33,r.y,84,81),new Rect());
                GUI.Label(new Rect(r.x+5,r.y+80,r.width-10,29),database.Get(member.FormId).Name,prepSmall);
            }
            GUI.enabled=modal==Modal.None&&!GrowthRevealActive&&run.CanSummon;
            string text=bench.Count>=7?"대기석 가득 참 · 7/7":run.Gold<ExpeditionRun.SummonCost?"소환 · "+(ExpeditionRun.SummonCost-run.Gold)+"G 부족":"동료 소환  ·  "+ExpeditionRun.SummonCost+"G";
            if(GUI.Button(PreparationLayout.Summon,text,button))Summon();
            GUI.enabled=modal==Modal.None&&!GrowthRevealActive&&run.DeployedIds.Count>0;
            if(GUI.Button(PreparationLayout.Battle,"전투 시작  ›",primary))StartBattle(false);
            GUI.enabled=modal==Modal.None&&!GrowthRevealActive;
        }

        void DrawEncounterBrief()
        {
            Rect panel=new Rect(220,70,840,580);ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(252,110,776,55),run.EncounterName,heading);
            GUI.Label(new Rect(252,180,776,72),PreparationAdvice(),body);
            GUI.Label(new Rect(252,265,776,55),run.EncounterDescription,hint);
            string[] checks=run.IsBossWave?ExpeditionPreparationHints.ForBoss(run,database):new[]{"동료를 눌러 사거리·지원 범위를 확인하세요.","같은 동료를 다시 누르면 기술과 성장 정보를 봅니다.","드래그 배치와 연구 선택은 준비 중 되돌릴 수 있습니다."};
            for(int i=0;i<checks.Length&&i<3;i++)GUI.Label(new Rect(252,335+i*38,776,36),"· "+checks[i],body);
            Rect footer=ExpeditionUiTheme.FooterRect(panel);
            if(GUI.Button(ExpeditionUiTheme.FooterButton(footer,0,2),"지역 이야기",button)){var story=ExpeditionStory.ForWave(run.Wave);if(story!=null)OpenJournal(story.Id);}
            if(GUI.Button(ExpeditionUiTheme.FooterButton(footer,1,2),"배치로 돌아가기",primary))modal=Modal.None;
        }
    }
}
