using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        public static readonly Rect NewExpeditionWindow = new Rect(80, 32, 1120, 656);
        static readonly string[][] MenuFormations = {
            CatalogLegacyResearch.GetFormationUnits("standard"), CatalogLegacyResearch.GetFormationUnits("tactical"),
            CatalogLegacyResearch.GetFormationUnits("ember"), CatalogLegacyResearch.GetFormationUnits("frost")
        };
        static readonly Color[] FormationColors = {
            new Color(.49f,.72f,.67f), new Color(.76f,.64f,.39f),
            new Color(.79f,.43f,.29f), new Color(.47f,.64f,.77f)
        };
        static readonly string[][] MenuGlyphs = {
            new[] {"01111100111110","11000111100011","10000111100001","10110111101101","10000111100001","10110111101101","10000111100001","10000111100001","11000111100011","01111100111110"},
            new[] {"001111111100","011000000110","011011110110","001000000100","001011110100","001000000100","001011110100","001000000100","001000000110","001111111110"},
            new[] {"001111111100","011111111110","011000000110","011001100110","011001100110","011001100110","001100001100","001110011100","000111111000","000001100000"},
            new[] {"000111111000","011111111110","010110011010","010110011010","011110011110","000011110000","000001100000","000001100000","000011110000","000111111000"}
        };
        GUIStyle menuCaption, menuName, menuQuiet, menuHero;
        GUIStyle menuCaptionOutline, menuNameOutline;
        readonly Texture2D[] menuGlyphTextures = new Texture2D[4];
        bool showMenuDevTools;
        static string MenuText(string key) { return CatalogText.Get("menu." + key); }

        void EnsureMenuStyles()
        {
            if (menuCaption != null) return;
            menuCaption = Style(15, ExpeditionUiTheme.Teal, true, TextAnchor.MiddleLeft);
            menuName = Style(15, Cream, false, TextAnchor.UpperCenter);
            menuCaption.padding = menuName.padding = new RectOffset(0,0,0,0);
            menuCaptionOutline = new GUIStyle(menuCaption);
            menuNameOutline = new GUIStyle(menuName);
            menuCaptionOutline.normal.textColor = menuNameOutline.normal.textColor = new Color(.015f,.025f,.03f,.94f);
            menuHero = Style(32, Cream, true, TextAnchor.MiddleLeft);
            menuQuiet = new GUIStyle(button);
            menuQuiet.normal.background = menuQuiet.onNormal.background = null;
            menuQuiet.normal.textColor = menuQuiet.onNormal.textColor = Muted;
        }

        void DrawMenuGlyph(int index, Vector2 center, float pixel, Color color)
        {
            var rows = MenuGlyphs[index];
            if(menuGlyphTextures[index]==null)
            {
                int width=rows[0].Length,height=rows.Length;
                var texture=new Texture2D(width,height,TextureFormat.RGBA32,false) {
                    filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave };
                var pixels=new Color[width*height];
                for(int r=0;r<height;r++)for(int c=0;c<width;c++)
                    pixels[(height-r-1)*width+c]=rows[r][c]=='1'?Color.white:Color.clear;
                texture.SetPixels(pixels);texture.Apply(false,true);menuGlyphTextures[index]=texture;
            }
            float x = Mathf.Round(center.x - rows[0].Length * pixel * .5f);
            float y = Mathf.Round(center.y - rows.Length * pixel * .5f);
            Color previous=GUI.color;GUI.color=color;
            GUI.DrawTexture(new Rect(x,y,rows[0].Length*pixel,rows.Length*pixel),menuGlyphTextures[index]);
            GUI.color=previous;
        }

        void ReleaseMenuArt()
        { for(int i=0;i<menuGlyphTextures.Length;i++)if(menuGlyphTextures[i]!=null)Destroy(menuGlyphTextures[i]); }

        bool MenuNav(Rect rect, int glyph, string label, string detail)
        {
            ExpeditionUiTheme.PanelBox(rect);
            DrawMenuGlyph(glyph, new Vector2(rect.x+34,rect.center.y),2,ExpeditionUiTheme.Teal);
            LocalizedGui.Label(new Rect(rect.x+64,rect.y+10,rect.width-78,27),label,body);
            LocalizedGui.Label(new Rect(rect.x+64,rect.y+40,rect.width-78,22),detail,small);
            return GUI.Button(rect,GUIContent.none,GUIStyle.none);
        }

        void OpenNewExpeditionMenu() { selectedFormationId = "standard"; modal = Modal.NewRun; }
        void ContinueFromMenu()
        { home = false; modal = Modal.None; playtest.Record("continue_run",run); }

        void DrawExpeditionHome()
        {
            EnsureMenuStyles();
            bool enabled = GUI.enabled, active = run != null && !run.HasEnded;
            int wave = run == null ? 1 : run.Wave;
            Texture2D region = wave <= 5 ? battlefield : wave <= 10 ? outpost :
                wave <= 15 ? furnace : wave <= 20 ? frost : wave <= 25 ? garden : palace;
            ExpeditionUiTheme.DrawBackdrop(new Rect(0,100,1280,620),region,false);
            // Broad stepped shade keeps the text readable without covering the party in a modal.
            for (int i=0;i<12;i++) Fill(new Rect(i*64,100,64,510),new Color(.025f,.055f,.065f,Mathf.Lerp(.96f,.12f,i/11f)));
            ExpeditionUiTheme.DrawHeaderPlate(new Rect(0,0,1280,118));
            LocalizedGui.Label(new Rect(56,18,760,58),CatalogText.Get("text.0613"),title);
            LocalizedGui.Label(new Rect(58,79,660,24),"I G N I S   /   A S H E N   O A T H",menuCaption);
            if(LocalizedGui.Button(new Rect(946,30,134,52),CloudText("account"),menuQuiet))modal=Modal.Account;
            if(LocalizedGui.Button(new Rect(1094,30,134,52),CatalogText.Get("text.0363"),menuQuiet))modal=Modal.Settings;

            LocalizedGui.Label(new Rect(56,154,470,26),MenuText(active ? "ongoing" : "next"),menuCaption);
            LocalizedGui.Label(new Rect(56,192,470,52),active ? MenuText("onward") : CatalogText.Get("text.0616"),menuHero);
            bool replay=journal!=null && journal.IsUnlocked("palace-clear");
            LocalizedGui.Label(new Rect(56,256,454,82),replay ? ExpeditionStory.ReplayBrief : ExpeditionStory.HomeBrief,body);
            LocalizedGui.Label(new Rect(56,354,470,28),active ?
                CatalogText.Format("menu.progress",run.Wave,run.ContentWaves,run.Lantern) : MenuText("fresh"),hint);
            if(active)for(int i=0;i<30;i++)
                Fill(new Rect(56+i*15.3f,394,i%5==4?12:9, i%5==4?5:3),
                    active && i<run.Victories ? Gold : new Color(.27f,.40f,.42f,.8f));
            GUI.enabled=enabled && !storageBlocked;
            if(LocalizedGui.Button(new Rect(56,424,456,64),CatalogText.Get(active ? "text.0622" : "text.0621"),primary))
            { if(active)ContinueFromMenu();else OpenNewExpeditionMenu(); }
            GUI.enabled=enabled && (!active || !storageBlocked);
            if(LocalizedGui.Button(new Rect(56,504,456,48),active ? CatalogText.Get("text.0621") :
                MenuText(run!=null?"last_result":"story"),button))
            { if(active)OpenNewExpeditionMenu();else if(run!=null)ContinueFromMenu();else OpenJournal("intro"); }
            GUI.enabled=enabled;
            DrawHomeParty(active);

            string warning=error ?? (legacySave || run!=null && run.UsesPreviousBalance ? CatalogText.Get("text.0620") : null);
            if(badProgress)warning=CatalogText.Get("text.0624");
            if(warning!=null)
            {
                Fill(new Rect(48,562,1184,44),new Color(.07f,.1f,.12f,.96f));
                LocalizedGui.Label(new Rect(60,568,1160,32),warning,hint);
            }
            Fill(new Rect(0,610,1280,110),new Color(.025f,.055f,.065f,.97f));
            if(MenuNav(new Rect(48,626,284,72),0,MenuText("story"),
                CatalogText.Format("menu.story_count",journal==null?1:journal.UnlockedCount,ExpeditionStory.Entries.Count)))OpenJournal(null);
            if(MenuNav(new Rect(348,626,284,72),1,CatalogText.Get("text.0497"),MenuText("records_hint")))
            {recordsPage=0;selectedRecord=null;modal=Modal.Records;}
            if(MenuNav(new Rect(648,626,284,72),2,CatalogText.Get("text.0617"),MenuText("progress_hint")))modal=Modal.Progress;
            if(MenuNav(new Rect(948,626,284,72),3,RankText("title"),MenuText("ranking_hint")))OpenRanking();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if(LocalizedGui.Button(new Rect(810,34,124,44),"DEV",menuQuiet))showMenuDevTools=!showMenuDevTools;
            if(showMenuDevTools)
            {
                ExpeditionUiTheme.PanelBox(new Rect(794,90,438,66));
                if(LocalizedGui.Button(new Rect(806,100,198,44),CatalogText.Get("text.0443"),button))modal=Modal.Playtest;
                if(LocalizedGui.Button(new Rect(1016,100,204,44),CatalogText.Get("text.0623"),button))OpenLab();
            }
#endif
            GUI.enabled=enabled;
        }

        void DrawHomeParty(bool active)
        {
            DrawOutlinedMenuCaption(new Vector2(620,170),MenuText(active ? "party" : "starting_party"),menuCaption,580,false);
            int count=active ? run.DeployedIds.Count : 3;
            if(count==0){LocalizedGui.Label(new Rect(620,300,596,60),MenuText("empty_party"),centered);return;}
            float slot=570f/count, size=count>3?156:216, baseline=452;
            float nameBottom=baseline+18;
            for(int i=0;i<count;i++)
            {
                var member=active ? run.Roster.Find(run.DeployedIds[i]) : null;
                string id=active ? member.FormId : MenuFormations[0][i];
                float x=630+(i+.5f)*slot;
                Fill(new Rect(x-31,baseline-5,62,8),new Color(.015f,.025f,.025f,.5f));
                if(art!=null)art.DrawPeaceful(id,new Rect(x-size*.5f,baseline-size,size,size),
                    GamePreferences.Current.ReduceMotion ? 0 : Time.unscaledTime,false);
                nameBottom=Mathf.Max(nameBottom,DrawOutlinedMenuCaption(new Vector2(x,baseline+18),
                    database.Get(id).Name,menuName,slot-8,true));
            }
            DrawOutlinedMenuCaption(new Vector2(640,Mathf.Max(540,nameBottom+20)),
                MenuText(active ? "party_hint" : "party_intro"),menuCaption,556,false);
        }

        float DrawOutlinedMenuCaption(Vector2 point,string text,GUIStyle style,float maxWidth,bool centeredAtPoint)
        {
            var content=new GUIContent(text);
            float width=Mathf.Min(maxWidth,Mathf.Ceil(style.CalcSize(content).x)+2);
            float height=Mathf.Ceil(style.CalcHeight(content,width))+2;
            Rect label=new Rect(centeredAtPoint?point.x-width*.5f:point.x,point.y,width,height);
            // One logical pixel of outline holds up on bright ground without covering the scene.
            GUIStyle outline=style==menuName?menuNameOutline:menuCaptionOutline;
            GUI.Label(new Rect(label.x+1,label.y+2,label.width,label.height),content,outline);
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                if(x!=0||y!=0)GUI.Label(new Rect(label.x+x,label.y+y,label.width,label.height),content,outline);
            LocalizedGui.Label(label,text,style);
            return label.yMax;
        }

        void DrawNewExpeditionMenu()
        {
            EnsureMenuStyles();
            Rect panel=NewExpeditionWindow, content=ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            LocalizedGui.Label(new Rect(content.x,64,600,40),CatalogText.Get("text.0621"),heading);
            LocalizedGui.Label(new Rect(content.xMax-270,69,270,30),MenuText("normal_length"),menuCaption);
            string warning=savePresent ? CatalogText.Get("text.0728") : legacySave ? CatalogText.Get("text.0729") : CatalogText.Get("text.0730");
            LocalizedGui.Label(new Rect(content.x,112,content.width,38),warning,small);
            ExpeditionUiTheme.PanelBox(new Rect(content.x,160,410,50));
            Fill(new Rect(content.x,160,4,50),ExpeditionUiTheme.Teal);
            LocalizedGui.Label(new Rect(content.x+20,170,370,30),CatalogText.Get("text.0732"),body);
            LocalizedGui.Label(new Rect(content.x+438,160,content.width-438,26),CatalogText.Get("text.0734"),small);
            LocalizedGui.Label(new Rect(content.x+438,188,content.width-438,26),
                progress!=null && progress.HardUnlocked && !badProgress ? CatalogText.Get("text.0735") : CatalogText.Get("text.0736"),small);
            LocalizedGui.Label(new Rect(content.x,222,content.width,25),RankedStartHelp,small);
            LocalizedGui.Label(new Rect(content.x,255,content.width,28),CatalogText.Get("text.0737"),menuCaption);
            bool enabled=GUI.enabled;
            for(int i=0;i<FormationIds.Length;i++)
            {
                Rect card=FormationMenuCard(i);
                bool selected=FormationIds[i]==selectedFormationId;
                GUI.enabled=enabled && ExpeditionRun.IsStartingFormation(FormationIds[i]);
                ExpeditionUiTheme.PanelBox(card,selected);
                Fill(new Rect(card.x+4,card.y+4,card.width-8,4),FormationColors[i]);
                if(selected)Fill(new Rect(card.x+4,card.yMax-5,card.width-8,2),Gold);
                LocalizedGui.Label(new Rect(card.x+16,card.y+16,card.width-32,30),FormationNames[i],centered);
                for(int j=0;j<3;j++)
                {
                    float cx=card.x+58+j*68, floor=card.y+142;
                    Fill(new Rect(cx-17,floor-2,34,4),new Color(0,0,0,.4f));
                    // Body-scale calibration is shared with the battlefield, including banner bearers.
                    if(art!=null)art.DrawPeaceful(MenuFormations[i][j],new Rect(cx-49,floor-98,98,98),
                        selected && !GamePreferences.Current.ReduceMotion ? Time.unscaledTime : 0,false);
                }
                LocalizedGui.Label(new Rect(card.x+14,card.y+153,card.width-28,42),FormationUnits[i],menuName);
                LocalizedGui.Label(new Rect(card.x+14,card.y+201,card.width-28,23),
                    selected ? CatalogText.Get("text.0738") : CatalogText.Get("text.0739"),selected?hint:small);
                if(GUI.Button(card,GUIContent.none,GUIStyle.none))selectedFormationId=FormationIds[i];
            }
            GUI.enabled=enabled;
            LocalizedGui.Label(new Rect(content.x,534,content.width,32),MenuText("formation_hint"),small);
            ExpeditionUiTheme.FooterRule(panel);
            DrawRankedStartButtons(panel,content);
        }

        public static Rect FormationMenuCard(int index) { return new Rect(112+index*268,290,252,232); }
    }
}
