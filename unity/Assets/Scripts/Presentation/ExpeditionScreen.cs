using System;
using System.Collections.Generic;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Main expedition route: gold-funded recruitment and fifteen encounters.
    public sealed class ExpeditionScreen : MonoBehaviour
    {
        const string SaveKey = "ember.expedition.run.v013";
        const string LegacySaveKey = "ember.expedition.run.v011";
        const float Width = 1280f, Height = 720f;
        const float GroundY = 380f, GroundYScale = 50f;
        static readonly Color Background = new Color(.045f, .09f, .075f);
        static readonly Color Panel = new Color(.075f, .16f, .135f);
        static readonly Color PanelLight = new Color(.12f, .23f, .185f);
        static readonly Color Cream = new Color(.97f, .94f, .82f);
        static readonly Color Gold = new Color(.9f, .72f, .39f);
        static readonly Color Muted = new Color(.68f, .77f, .69f);
        static readonly string[][] Branches = {
            new[] { "E-KN-G", "E-KN-C" }, new[] { "E-AR-P", "E-AR-T" },
            new[] { "E-FI-B", "E-FI-E" }, new[] { "E-PR-G", "E-PR-H" },
            new[] { "E-IC-F", "E-IC-S" }, new[] { "E-BA-G", "E-BA-A" }
        };
        static readonly string[] Families = { "KN", "AR", "FI", "PR", "IC", "BA" };
        const float GrowthHoverSeconds = .45f;
        static readonly string[] TraitIds = { "T-KN", "T-AR", "T-FI", "T-PR", "T-IC", "T-BA",
            "T-H01", "T-H02", "T-H03", "T-H04", "T-H05", "T-H06" };
        static readonly string[] TraitNames = { "버팀", "집중", "잔불", "첫 빛", "한기", "서약",
            "재가르기", "온기", "서리 추적", "열균열", "한마음", "전리품 보급" };

        enum Modal { None, Detail, ChooseDonor, ChoosePartner, CombinationOptions, Confirm, NewRun }
        enum GrowthAction { None, Transfer, Evolve, Combine }

        CatalogDatabase database;
        ExpeditionRun run;
        CatalogArtView art;
        Texture2D battlefield, outpost, furnace;
        Camera backgroundCamera;
        GUIStyle title, heading, body, small, centered, button, primary, hint;
        Font font;
        Rect viewport;
        readonly List<CatalogMember> bench = new List<CatalogMember>();
        readonly List<CatalogMember> fieldMembers = new List<CatalogMember>(5);
        bool home = true, savePresent, badSave, inheritTrait, pointerDragging, legacySave, showHelp;
        bool pointerTouch, hoverArmed, evolutionFromDrag, combinationFromList;
        string error, selectedId, pendingOtherId, pendingFormId, toast, pointerId;
        string hoverRecipientId, growthPulseId;
        float toastUntil, hoverStartTime = -1f, growthPulseUntil;
        Vector2 pointerStart, pointerCurrent, pointerGrabOffset;
        int benchPage, choosePage, combinationPage, pointerFingerId;
        Modal modal;
        GrowthAction pendingAction;

        void Awake()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            try
            {
                TextAsset content = Resources.Load<TextAsset>("Content/unit-catalog-v010");
                if (content == null) throw new InvalidOperationException("동료 자료를 찾을 수 없습니다.");
                database = CatalogDatabase.FromJson(content.text);
                battlefield = Resources.Load<Texture2D>("Art/forest-battlefield");
                outpost = Resources.Load<Texture2D>("Art/ruined-outpost-battlefield");
                furnace = Resources.Load<Texture2D>("Art/Environments/v024/furnace-battlefield");
                LoadSave();
            }
            catch (Exception exception) { error = "원정 자료를 열 수 없습니다: " + exception.Message; }
            backgroundCamera = GetComponent<Camera>();
            if (backgroundCamera == null) backgroundCamera = gameObject.AddComponent<Camera>();
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = Background;
            backgroundCamera.cullingMask = 0;
            backgroundCamera.depth = -10;
        }

        void OnEnable() { if (art == null) art = new CatalogArtView(); }
        void OnDisable() { CancelPointer(); if (art != null) { art.Dispose(); art = null; } }
        void OnDestroy() { if (font != null) Destroy(font); }
        void OnApplicationPause(bool paused) { if (paused) { CancelPointer(); Save(); } }
        void OnApplicationFocus(bool focused) { if (!focused) CancelPointer(); }

        void LoadSave()
        {
            savePresent = PlayerPrefs.HasKey(SaveKey);
            legacySave = !savePresent && PlayerPrefs.HasKey(LegacySaveKey);
            if (!savePresent) return;
            try
            {
                string json = PlayerPrefs.GetString(SaveKey, "");
                if (string.IsNullOrEmpty(json)) throw new InvalidOperationException("빈 저장 내용");
                ExpeditionRunSave state = JsonUtility.FromJson<ExpeditionRunSave>(json);
                if (state == null) throw new InvalidOperationException("저장 형식을 읽을 수 없음");
                run = ExpeditionRun.Restore(database, state);
            }
            catch (Exception exception)
            {
                badSave = true;
                run = null;
                error = "이전 원정 저장을 읽을 수 없습니다. 새 원정을 선택하면 확인 후 다시 시작합니다. (" +
                    exception.Message + ")";
            }
        }

        void Save()
        {
            if (run == null || badSave) return;
            try
            {
                PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(run.Capture()));
                PlayerPrefs.Save();
                savePresent = true;
            }
            catch (Exception exception) { Notice("저장하지 못했습니다: " + exception.Message); }
        }

        void NewRun()
        {
            try
            {
                run = ExpeditionRun.New(database, unchecked((uint)DateTime.UtcNow.Ticks));
                badSave = false;
                legacySave = false;
                error = null;
                home = false;
                modal = Modal.None;
                selectedId = null;
                benchPage = 0;
                Save();
            }
            catch (Exception exception) { Notice("새 원정을 시작하지 못했습니다: " + exception.Message); }
        }

        void Notice(string message) { toast = message; toastUntil = Time.unscaledTime + 4f; }

        void EnsureStyles()
        {
            if (title != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] {
                "Noto Sans CJK KR", "Noto Sans KR", "Malgun Gothic", "Apple SD Gothic Neo", "Arial"
            }, 18);
            title = Style(38, Gold, true, TextAnchor.MiddleLeft);
            heading = Style(26, Cream, true, TextAnchor.MiddleLeft);
            body = Style(19, Cream, false, TextAnchor.MiddleLeft);
            small = Style(15, Muted, false, TextAnchor.MiddleLeft);
            centered = Style(17, Cream, true, TextAnchor.MiddleCenter);
            hint = Style(17, Gold, false, TextAnchor.MiddleLeft);
            button = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, wordWrap = true };
            primary = new GUIStyle(button);
            if (font != null) { button.font = font; primary.font = font; }
            button.normal.textColor = Cream;
            button.hover.textColor = Color.white;
            primary.normal.textColor = Gold;
            primary.hover.textColor = Gold;
        }

        GUIStyle Style(int size, Color color, bool bold, TextAnchor align)
        {
            var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, alignment = align,
                wordWrap = true };
            style.normal.textColor = color;
            return style;
        }

        void OnGUI()
        {
            Rect safe = Screen.safeArea;
            float scale = Mathf.Max(.01f, Mathf.Min(safe.width / Width, safe.height / Height));
            viewport = new Rect(safe.x + (safe.width - Width * scale) * .5f,
                Screen.height - safe.yMax + (safe.height - Height * scale) * .5f,
                Width * scale, Height * scale);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture);
            EnsureStyles();
            GUI.matrix = Matrix4x4.TRS(new Vector3(viewport.x, viewport.y), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            Fill(new Rect(0, 0, Width, Height), Background);
            GUI.enabled = modal == Modal.None;
            if (home || run == null) DrawHome();
            else if (run.Phase == ExpeditionPhase.Preparation) DrawPreparation();
            else if (run.Phase == ExpeditionPhase.Battle) DrawBattleResume();
            else DrawResult();
            GUI.enabled = true;
            if (modal != Modal.None) DrawModal();
            if (toast != null && Time.unscaledTime < toastUntil)
            {
                Fill(new Rect(340, 660, 600, 48), new Color(.14f, .27f, .21f, .97f));
                GUI.Label(new Rect(350, 667, 580, 34), toast, centered);
            }
            GUI.matrix = Matrix4x4.identity;
        }

        void DrawHome()
        {
            Fill(new Rect(0, 0, Width, 90), Panel);
            GUI.Label(new Rect(42, 17, 920, 60), "잔불 원정대", title);
            Fill(new Rect(155, 150, 970, 425), Panel);
            GUI.Label(new Rect(220, 192, 840, 70), "꺼진 길에 다시 불을 밝히다", heading);
            GUI.Label(new Rect(220, 265, 840, 70), "승리로 얻은 골드로 동료를 소환하고 성장시키세요.\n열다섯 번의 전투, 세 명의 보스가 기다립니다.", body);
            if (GUI.Button(new Rect(230, 385, 385, 76), "새 원정", primary))
            {
                if (savePresent || legacySave) modal = Modal.NewRun; else NewRun();
            }
            GUI.enabled = modal == Modal.None && run != null;
            if (GUI.Button(new Rect(665, 385, 385, 76), "이어하기", button))
            { home = false; modal = Modal.None; }
            GUI.enabled = modal == Modal.None;
            if (error != null) GUI.Label(new Rect(220, 475, 840, 75), error, hint);
            else if (legacySave) GUI.Label(new Rect(220, 475, 840, 75),
                "이전 6전투 원정은 보관되어 있습니다. 새 원정을 눌러 골드 원정을 시작하세요.", hint);
            if (GUI.Button(new Rect(1020, 650, 225, 48), "개발용 전투 시험실", button)) OpenLab();
        }

        void DrawHeader(string phase)
        {
            Fill(new Rect(0, 0, Width, 80), Panel);
            if (GUI.Button(new Rect(18, 15, 90, 50), "거점", button)) { home = true; modal = Modal.None; Save(); }
            GUI.Label(new Rect(130, 10, 350, 58), "잔불 원정대", title);
            GUI.Label(new Rect(530, 18, 320, 43), "전투 " + run.Wave + " / " + ExpeditionRun.TotalWaves +
                "     등불 " + run.Lantern, body);
            GUI.Label(new Rect(880, 18, 170, 43), run.Gold + " G", heading);
            Fill(new Rect(1070, 17, 188, 43), PanelLight);
            GUI.Label(new Rect(1080, 21, 168, 33), phase, centered);
        }

        void DrawPreparation()
        {
            DrawHeader(run.IsBossWave ? "보스전 준비" : "전투 준비");
            Rect field = new Rect(17, 93, 1246, 449);
            Texture2D region = run.Wave <= 5 ? battlefield : run.Wave <= 10 ? outpost : furnace;
            if (region != null) GUI.DrawTexture(field, region, ScaleMode.ScaleAndCrop);
            else Fill(field, new Color(.075f, .15f, .12f));
            Fill(field, new Color(.015f, .045f, .03f, .23f));
            Fill(new Rect(32, 143, 687, 363), new Color(.04f, .12f, .075f, .26f));
            Fill(new Rect(724, 94, 523, 430), new Color(.025f, .075f, .06f, .86f));
            Fill(new Rect(48, 185, 615, 1), new Color(.57f, .62f, .41f, .32f));
            Fill(new Rect(48, 350, 615, 1), new Color(.57f, .62f, .41f, .32f));
            GUI.Label(new Rect(46, 100, 630, 34), "출전 배치  " + run.DeployedIds.Count + " / 5", heading);
            GUI.Label(new Rect(735, 101, 505, 37), run.EncounterName, heading);
            GUI.Label(new Rect(735, 146, 490, 35), run.EncounterDescription, hint);
            GUI.Label(new Rect(735, 193, 490, 65), PreparationAdvice(), body);
            if (GUI.Button(new Rect(735, 273, 240, 42), showHelp ? "도움말 접기  −" : "배치 · 성장 도움말  +", button))
                showHelp = !showHelp;
            GUI.Label(new Rect(735, 326, 480, 125), showHelp ?
                "동료를 겹쳐 잠시 유지한 뒤 놓으면 성장 +1\n결합은 아래 목록에서 결과를 보고 선택\n재료 보호 중인 동료는 소모되지 않습니다\n출전 최대 5명 · 대기 최대 7명" :
                "동료를 겹쳐 놓으면 성장 +1\n결합은 가능한 조합 목록에서 선택\n남은 골드는 다음 전투에도 유지됩니다.", body);
            List<CatalogCombinationOption> combinations = run.GetCombinationOptions();
            if (combinations.Count > 0 && GUI.Button(new Rect(735, 468, 315, 52),
                combinations.Count == 1 ? database.Get(combinations[0].ResultId).Name + " 결합 가능" :
                "결합 가능 " + combinations.Count + "쌍 보기", primary)) OpenCombinationOptions();
            fieldMembers.Clear();
            foreach (string id in run.DeployedIds)
            {
                CatalogMember member = run.Roster.Find(id);
                if (member != null) fieldMembers.Add(member);
            }
            fieldMembers.Sort((a, b) => b.Y.CompareTo(a.Y));
            foreach (CatalogMember member in fieldMembers)
            {
                Vector2 foot = ArenaPoint(member);
                DrawUnit(member, new Rect(foot.x - 48, foot.y - 113, 96, 110),
                    new Rect(foot.x - 64, foot.y, 128, 31));
            }
            DrawDock();
            if (pointerDragging && pointerId != null)
            {
                DrawGrowthPreview();
                CatalogMember carried = run.Roster.Find(pointerId);
                if (carried != null && art != null)
                {
                    Fill(new Rect(pointerCurrent.x - 52, pointerCurrent.y - 103, 104, 111),
                        new Color(.88f, .73f, .43f, .25f));
                    art.Draw(carried.FormId, new Rect(pointerCurrent.x - 48, pointerCurrent.y - 99, 96, 99),
                        (int)(Time.unscaledTime * 5f) % 4);
                }
            }
        }

        string PreparationAdvice()
        {
            switch (run.Wave)
            {
                case 1: return "기사를 앞에, 궁수와 사제를 뒤에 두세요.\n소환한 동료는 대기석에 합류합니다.";
                case 2: return "가시등 짐승은 약해진 동료를 노립니다.\n전열 뒤에 궁수와 사제를 배치하세요.";
                case 3: return "측면의 가시등이 후열을 노립니다.\n동료를 늘리거나 성장 2로 첫 진화를 하세요.";
                case 4: return "정예 뿔짐승이 전열로 돌진합니다.\n기사가 버티는 동안 후열에서 공격하세요.";
                case 5: return "직선 돌진 · 체력 절반부터 간격 단축\n전열과 후열을 같은 선에 겹치지 마세요.";
                case 6: return "돌편 보병 뒤 석궁병이 약한 동료를 노립니다.\n새 구간의 첫 전투에서 진형을 점검하세요.";
                case 7: return "석궁병 둘이 약한 동료를 집중 공격합니다.\n전열을 키우고 후열이 노출되지 않게 하세요.";
                case 8: return "보병 셋과 석궁병 둘이 함께 압박합니다.\n동료의 진화·전승 상태를 점검하세요.";
                case 9: return "갑주 석상이 돌진하고 석궁병이 약한 동료를 노립니다.\n전열과 후열을 벌리고 성장한 동료를 배치하세요.";
                case 10: return "범위 내려찍기 · 체력 40%부터 후속파\n탱커 주변에 동료를 밀집시키지 마세요.";
                case 11: return "잿재 채굴병은 돌진 없이 전열을 압박합니다.\n뒤의 불씨 투척병부터 견제하세요.";
                case 12: return "불씨 투척병 둘이 약한 동료를 노립니다.\n후열을 전열에서 너무 멀리 두지 마세요.";
                case 13: return "채굴병 셋이 전열을 묶고 투척병이 후열을 노립니다.\n보호와 회복을 나누어 배치하세요.";
                case 14: return "감독관은 가까운 적 둘에게 4초 보호막을 줍니다.\n보호막은 7초마다 다시 생깁니다.";
                case 15: return "범위 폭발에 대비해 동료를 흩어 배치하세요.\n체력 45%부터 후속 불씨파가 추가됩니다.";
                default: return "승리하면 골드를 얻습니다.\n소환한 동료는 대기석에 합류합니다.";
            }
        }

        void DrawDock()
        {
            Fill(new Rect(0, 550, Width, 170), Panel);
            bench.Clear();
            foreach (CatalogMember member in run.Roster.Members)
                if (!IsDeployed(member.Id)) bench.Add(member);
            GUI.Label(new Rect(23, 557, 145, 31), "대기 " + bench.Count + "/7", heading);
            int pages = Mathf.Max(1, (bench.Count + 4) / 5);
            benchPage = Mathf.Clamp(benchPage, 0, pages - 1);
            if (GUI.Button(new Rect(23, 598, 55, 58), "‹", button)) benchPage = (benchPage + pages - 1) % pages;
            if (GUI.Button(new Rect(84, 598, 55, 58), "›", button)) benchPage = (benchPage + 1) % pages;
            GUI.Label(new Rect(26, 663, 117, 31), (benchPage + 1) + " / " + pages, small);
            for (int i = 0; i < 5; i++)
            {
                Rect card = BenchRect(i);
                Fill(card, PanelLight);
                int index = benchPage * 5 + i;
                if (index < bench.Count)
                {
                    CatalogMember member = bench[index];
                    DrawUnit(member, new Rect(card.x + 32, card.y + 3, 75, 93),
                        new Rect(card.x + 4, card.y + 100, card.width - 8, 27));
                }
            }
            GUI.enabled = modal == Modal.None && run.CanSummon;
            string summonLabel = bench.Count >= 7 ? "대기석 가득 참 · 7/7" : run.Gold < ExpeditionRun.SummonCost ?
                "소환 · " + (ExpeditionRun.SummonCost - run.Gold) + "G 부족" : "동료 소환  ·  " + ExpeditionRun.SummonCost + "G";
            if (GUI.Button(new Rect(944, 574, 310, 58), summonLabel, button)) Summon();
            GUI.enabled = modal == Modal.None && run.DeployedIds.Count > 0;
            if (GUI.Button(new Rect(944, 641, 310, 58), "전투 시작  ›", primary))
                StartBattle(false);
            GUI.enabled = modal == Modal.None && run.CanUndo;
            if (GUI.Button(new Rect(1075, 483, 164, 44), "되돌리기", button))
            {
                if (run.Undo()) { selectedId = null; Save(); Notice("직전 준비 행동을 되돌렸습니다."); }
                else Notice("대기석 공간이 부족하거나 되돌릴 행동이 없습니다.");
            }
            GUI.enabled = modal == Modal.None;
        }

        static Rect BenchRect(int index) { return new Rect(150 + index * 155, 573, 145, 127); }

        void DrawUnit(CatalogMember member, Rect sprite, Rect name)
        {
            if (Time.unscaledTime < growthPulseUntil && member.Id == growthPulseId)
                Fill(new Rect(sprite.x - 5, sprite.y - 5, sprite.width + 10, sprite.height + 10),
                    new Color(.95f, .74f, .30f, .36f));
            Fill(new Rect(sprite.center.x - 28, sprite.yMax - 10, 56, 7), new Color(.02f, .06f, .04f, .7f));
            if (art != null) art.Draw(member.FormId, sprite, (int)(Time.unscaledTime * 5f) % 4);
            string label = database.Get(member.FormId).Name;
            GUI.Label(name, label, centered);
            if (member.MaterialLocked)
            {
                Fill(new Rect(sprite.x, sprite.y + 2, 54, 24), new Color(.09f, .18f, .15f, .9f));
                GUI.Label(new Rect(sprite.x, sprite.y, 54, 25), "보호", small);
            }
        }

        void DrawBattleResume()
        {
            DrawHeader("전투 대기");
            Fill(new Rect(185, 170, 910, 380), Panel);
            GUI.Label(new Rect(245, 218, 790, 62), run.EncounterName, heading);
            GUI.Label(new Rect(245, 285, 790, 92), "이전 전투가 중단되었습니다. 같은 편성과 전투 시작 상태로 이어갑니다.", body);
            if (GUI.Button(new Rect(285, 418, 710, 72), "전투 재개", primary)) StartBattle(true);
        }

        void DrawResult()
        {
            bool victory = run.Phase == ExpeditionPhase.Completed ||
                (run.Phase == ExpeditionPhase.Result && run.LastVictory);
            DrawHeader(run.Phase == ExpeditionPhase.Result ? "전투 결과" : "원정 결과");
            Fill(new Rect(175, 140, 930, 430), Panel);
            GUI.Label(new Rect(225, 180, 830, 72),
                run.Phase == ExpeditionPhase.Completed ? "원정 완료" :
                run.Phase == ExpeditionPhase.Defeated ? "원정 종료" : victory ? "승리" : "패배", title);
            string outcome = run.Phase == ExpeditionPhase.Completed ? "모든 길을 돌파했습니다." :
                run.Phase == ExpeditionPhase.Defeated ? "이번 원정은 여기서 끝납니다." :
                run.Phase == ExpeditionPhase.Result && run.Lantern <= 0 ?
                "등불을 모두 소진했습니다." : victory ?
                (run.Wave == ExpeditionRun.TotalWaves ? "마지막 보스를 쓰러뜨렸습니다." : "다음 길을 준비하세요.") :
                "등불 −1 · 편성을 바꾸고 같은 전투에 재도전하세요.";
            GUI.Label(new Rect(225, 275, 830, 92),
                "승리 " + run.Victories + " / " + ExpeditionRun.TotalWaves + "     등불 " + run.Lantern +
                "\n" + outcome, body);
            string reward = run.LastVictory && run.LastReward > 0 ?
                "+" + run.LastReward + " G   ·   " + (run.Gold - run.LastReward) + " → " + run.Gold + " G" :
                run.LastVictory && run.Wave == ExpeditionRun.TotalWaves ?
                ExpeditionRun.TotalWaves + "전투 돌파 · 원정 완료" :
                "획득 골드 0 G   ·   보유 " + run.Gold + " G";
            GUI.Label(new Rect(225, 370, 830, 43), reward, heading);
            if (run.Phase == ExpeditionPhase.Result)
            {
                string next = run.Lantern <= 0 || run.LastVictory && run.Wave == ExpeditionRun.TotalWaves ?
                    "원정 결과" : run.LastVictory ? "다음 전투 준비  ›" : "같은 전투 재도전  ›";
                if (GUI.Button(new Rect(260, 425, 760, 75), next, primary))
                { if (run.Continue()) { Save(); selectedId = null; } }
            }
            else if (GUI.Button(new Rect(260, 425, 760, 75), "거점으로", primary)) home = true;
        }

        void DrawModal()
        {
            Fill(new Rect(0, 0, Width, Height), new Color(0f, .015f, .01f, .82f));
            if (modal == Modal.Detail) DrawDetail();
            else if (modal == Modal.ChooseDonor || modal == Modal.ChoosePartner) DrawChooseMember();
            else if (modal == Modal.CombinationOptions) DrawCombinationOptions();
            else DrawConfirmation();
        }

        void DrawDetail()
        {
            CatalogMember member = run == null ? null : run.Roster.Find(selectedId);
            if (member == null) { modal = Modal.None; return; }
            Rect panel = new Rect(245, 76, 790, 570);
            Fill(panel, Panel);
            CatalogForm form = database.Get(member.FormId);
            GUI.Label(new Rect(277, 99, 560, 48), form.Name, heading);
            if (GUI.Button(new Rect(956, 91, 60, 54), "×", button)) { modal = Modal.None; return; }
            if (art != null) art.Draw(member.FormId, new Rect(290, 154, 155, 175), (int)(Time.unscaledTime * 5f) % 4);
            GUI.Label(new Rect(468, 157, 515, 91),
                "체력 " + form.Hp.ToString("0") + "   공격 " + form.Power.ToString("0") +
                "   방어 " + form.Defense.ToString("0") + "\n" +
                "공격 간격 " + form.AttackInterval.ToString("0.0") + "초   사거리 " + form.Range.ToString("0.0"), body);
            int target = member.FormId.StartsWith("B-", StringComparison.Ordinal) ? 2 :
                member.FormId.StartsWith("E-", StringComparison.Ordinal) ? 6 : 0;
            GUI.Label(new Rect(468, 259, 510, 58), target > 0 ?
                "진화 진행  " + member.Growth + " / " + target : "최종 형태", hint);
            GUI.Label(new Rect(468, 309, 510, 34), "고유 특성: " + TraitName(form.InnateTrait) +
                "    전승 특성: " + TraitName(member.InheritedTraitId), small);
            GUI.Label(new Rect(285, 347, 700, 40), "성장과 편성", heading);
            if (GUI.Button(new Rect(277, 407, 225, 62), IsDeployed(member.Id) ? "대기석으로" : "출전시키기", button))
            {
                if (run.SetDeployed(member.Id, !IsDeployed(member.Id))) { Save(); modal = Modal.None; }
                else Notice("출전 1~5명 · 대기석 최대 7명을 유지해야 합니다.");
            }
            if (GUI.Button(new Rect(527, 407, 225, 62), "전승", button))
            { inheritTrait = false; choosePage = 0; modal = Modal.ChooseDonor; }
            GUI.enabled = target > 0 && member.Growth >= target;
            if (GUI.Button(new Rect(776, 407, 225, 62), "진화", button)) PrepareEvolution(member);
            GUI.enabled = true;
            GUI.enabled = member.FormId.StartsWith("E-", StringComparison.Ordinal);
            if (GUI.Button(new Rect(527, 492, 225, 62), "결합", button))
            { choosePage = 0; modal = Modal.ChoosePartner; }
            GUI.enabled = true;
            if (GUI.Button(new Rect(277, 492, 225, 62), member.MaterialLocked ? "재료 보호 해제" : "재료 보호", button))
            {
                if (run.SetMaterialLocked(member.Id, !member.MaterialLocked)) Save();
                else Notice("재료 보호 상태를 바꾸지 못했습니다.");
            }
            GUI.Label(new Rect(277, 568, 725, 49), "끌어놓기로 성장합니다. 재료 보호 중인 동료는 전승·결합에 쓰이지 않습니다.", small);
        }

        void DrawChooseMember()
        {
            bool transfer = modal == Modal.ChooseDonor;
            Fill(new Rect(215, 75, 850, 585), Panel);
            GUI.Label(new Rect(245, 92, 760, 52), transfer ? "전승할 동료 선택" : "결합할 동료 선택", heading);
            if (GUI.Button(new Rect(980, 92, 59, 50), "×", button)) { modal = Modal.Detail; return; }
            if (transfer)
            {
                if (GUI.Button(new Rect(250, 150, 750, 48),
                    (inheritTrait ? "✓  " : "○  ") + "재료 동료의 고유 특성도 이어받기", button))
                    inheritTrait = !inheritTrait;
            }
            CatalogMember selected = run.Roster.Find(selectedId);
            var candidates = new List<CatalogMember>();
            foreach (CatalogMember other in run.Roster.Members)
            {
                if (other.Id == selectedId) continue;
                string result = transfer ? null : run.Roster.HybridResult(selected.FormId, other.FormId);
                if (!transfer && result == null) continue;
                if (other.MaterialLocked || transfer && !run.Roster.CanTransfer(other.Id, selected.Id, inheritTrait) ||
                    !transfer && !run.CanCombine(selected.Id, other.Id, result)) continue;
                candidates.Add(other);
            }
            int pages = Mathf.Max(1, (candidates.Count + 11) / 12);
            choosePage = Mathf.Clamp(choosePage, 0, pages - 1);
            int shown = 0;
            for (int index = choosePage * 12; index < candidates.Count && shown < 12; index++)
            {
                CatalogMember other = candidates[index];
                string result = transfer ? null : run.Roster.HybridResult(selected.FormId, other.FormId);
                int col = shown % 2, row = shown / 2;
                Rect slot = new Rect(250 + col * 385, 211 + row * 65, 365, 58);
                string label = (IsDeployed(other.Id) ? "출전 · " : "대기 · ") + database.Get(other.FormId).Name;
                if (result != null) label += "  →  " + database.Get(result).Name;
                else label += "  ·  " + TraitName(database.Get(other.FormId).InnateTrait);
                if (GUI.Button(slot, label, button))
                {
                    pendingAction = transfer ? GrowthAction.Transfer : GrowthAction.Combine;
                    pendingOtherId = other.Id;
                    pendingFormId = result;
                    combinationFromList = false;
                    modal = Modal.Confirm;
                }
                shown++;
            }
            if (shown == 0) GUI.Label(new Rect(250, 248, 740, 95),
                transfer ? "전승할 다른 동료가 없습니다." : "지금 결합할 수 있는 동료가 없습니다.", body);
            if (pages > 1)
            {
                if (GUI.Button(new Rect(254, 611, 112, 40), "이전", button)) choosePage--;
                GUI.Label(new Rect(375, 616, 170, 31), (choosePage + 1) + " / " + pages, small);
                if (GUI.Button(new Rect(545, 611, 112, 40), "다음", button)) choosePage++;
            }
        }

        void OpenCombinationOptions()
        {
            if (run == null || run.Phase != ExpeditionPhase.Preparation) return;
            List<CatalogCombinationOption> options = run.GetCombinationOptions();
            if (options.Count == 0) { Notice("지금 가능한 결합이 없습니다."); return; }
            combinationPage = 0;
            modal = Modal.CombinationOptions;
        }

        void ChooseCombinationOption(int index)
        {
            List<CatalogCombinationOption> options = run.GetCombinationOptions();
            if (index < 0 || index >= options.Count)
            { Notice("결합할 동료가 바뀌었습니다. 다시 선택해 주세요."); return; }
            CatalogCombinationOption option = options[index];
            if (!run.CanCombine(option.RecipientId, option.DonorId, option.ResultId))
            { Notice("지금 가능한 결합이 아닙니다."); return; }
            selectedId = option.RecipientId;
            pendingOtherId = option.DonorId;
            pendingFormId = option.ResultId;
            pendingAction = GrowthAction.Combine;
            combinationFromList = true;
            modal = Modal.Confirm;
        }

        void DrawCombinationOptions()
        {
            List<CatalogCombinationOption> options = run.GetCombinationOptions();
            Fill(new Rect(215, 75, 850, 585), Panel);
            GUI.Label(new Rect(245, 92, 720, 52), "가능한 결합  " + options.Count + "쌍", heading);
            if (GUI.Button(new Rect(980, 92, 59, 50), "×", button)) { modal = Modal.None; return; }
            GUI.Label(new Rect(250, 146, 760, 46), "결합은 선택 사항입니다. 결과와 재료를 확인한 뒤 진행하세요.", small);
            int pages = Mathf.Max(1, (options.Count + 4) / 5);
            combinationPage = Mathf.Clamp(combinationPage, 0, pages - 1);
            for (int row = 0; row < 5; row++)
            {
                int index = combinationPage * 5 + row;
                if (index >= options.Count) break;
                CatalogCombinationOption option = options[index];
                CatalogMember recipient = run.Roster.Find(option.RecipientId);
                CatalogMember donor = run.Roster.Find(option.DonorId);
                if (recipient == null || donor == null) continue;
                string label = database.Get(option.ResultId).Name + " 결합\n" +
                    "남는 동료 " + MemberPositionLabel(recipient.Id) + " " + database.Get(recipient.FormId).Name +
                    "  +  재료 " + MemberPositionLabel(donor.Id) + " " + database.Get(donor.FormId).Name +
                    " · 전승 " + TraitName(donor.InheritedTraitId);
                if (GUI.Button(new Rect(250, 195 + row * 78, 750, 70), label, button)) ChooseCombinationOption(index);
            }
            if (options.Count == 0) GUI.Label(new Rect(250, 240, 740, 80), "가능한 결합이 없습니다.", body);
            if (pages > 1)
            {
                if (GUI.Button(new Rect(250, 605, 112, 40), "이전", button)) combinationPage--;
                GUI.Label(new Rect(375, 610, 170, 30), (combinationPage + 1) + " / " + pages, small);
                if (GUI.Button(new Rect(545, 605, 112, 40), "다음", button)) combinationPage++;
            }
            if (GUI.Button(new Rect(806, 605, 193, 40), "나중에", button)) modal = Modal.None;
        }

        void PrepareEvolution(CatalogMember member, bool fromDrag = false)
        {
            evolutionFromDrag = fromDrag;
            selectedId = member.Id;
            if (member.FormId.StartsWith("E-", StringComparison.Ordinal))
            {
                pendingFormId = "F-" + CatalogRoster.FamilyCode(member.FormId);
                pendingAction = GrowthAction.Evolve;
                modal = Modal.Confirm;
                return;
            }
            string family = CatalogRoster.FamilyCode(member.FormId);
            for (int i = 0; i < Families.Length; i++)
                if (Families[i] == family) { pendingFormId = Branches[i][0]; break; }
            pendingAction = GrowthAction.Evolve;
            modal = Modal.Confirm;
        }

        void DrawConfirmation()
        {
            if (modal == Modal.Confirm && pendingAction == GrowthAction.Combine)
            { DrawCombinationConfirmation(); return; }
            Fill(new Rect(335, 155, 610, 385), Panel);
            if (modal == Modal.NewRun)
            {
                GUI.Label(new Rect(375, 188, 530, 54), "새 원정을 시작할까요?", heading);
                GUI.Label(new Rect(375, 255, 530, 115),
                    legacySave ? "이전 6전투 원정은 별도로 보관됩니다.\n골드 40G와 기본 동료 3명으로 새로 출발합니다." :
                    "현재 저장된 원정을 새 출발로 바꿉니다.\n진행과 동료 편성은 되돌릴 수 없습니다.", body);
                ConfirmationButtons("새 원정 시작", NewRun, () => modal = Modal.None);
                return;
            }
            CatalogMember selected = run.Roster.Find(selectedId);
            CatalogMember other = run.Roster.Find(pendingOtherId);
            if (selected == null) { modal = Modal.None; return; }
            string headingText = pendingAction == GrowthAction.Transfer ? "전승 확인" :
                pendingAction == GrowthAction.Combine ? "결합 확인" : "진화 확인";
            GUI.Label(new Rect(375, 184, 530, 55), headingText, heading);
            string selectedName = database.Get(selected.FormId).Name;
            string resultName = pendingAction == GrowthAction.Transfer ? selectedName :
                pendingFormId == null ? "" : database.Get(pendingFormId).Name;
            string message = pendingAction == GrowthAction.Evolve ? selectedName + " → " + resultName :
                other == null ? "동료를 다시 선택해 주세요." :
                selectedName + " + " + database.Get(other.FormId).Name + " → " + resultName +
                "\n" + database.Get(other.FormId).Name + "은(는) 재료로 떠납니다.";
            if (pendingAction == GrowthAction.Transfer)
                message += "\n진화 진행이 가능하면 1 증가" + (inheritTrait ? " · 고유 특성 전승" : "");
            GUI.Label(new Rect(375, 248, 530, 134), message, body);
            if (pendingAction == GrowthAction.Evolve && selected.FormId.StartsWith("B-", StringComparison.Ordinal))
            {
                string family = CatalogRoster.FamilyCode(selected.FormId);
                int familyIndex = Array.IndexOf(Families, family);
                if (familyIndex >= 0)
                {
                    if (GUI.Button(new Rect(375, 370, 250, 48), database.Get(Branches[familyIndex][0]).Name, button))
                        pendingFormId = Branches[familyIndex][0];
                    if (GUI.Button(new Rect(635, 370, 250, 48), database.Get(Branches[familyIndex][1]).Name, button))
                        pendingFormId = Branches[familyIndex][1];
                    GUI.Label(new Rect(375, 422, 500, 29), "선택: " + database.Get(pendingFormId).Name, small);
                }
            }
            ConfirmationButtons("실행", ExecuteGrowth,
                () => modal = pendingAction == GrowthAction.Evolve && evolutionFromDrag ? Modal.None : Modal.Detail);
        }

        void DrawCombinationConfirmation()
        {
            CatalogMember recipient = run.Roster.Find(selectedId);
            CatalogMember donor = run.Roster.Find(pendingOtherId);
            if (recipient == null || donor == null || !run.CanCombine(selectedId, pendingOtherId, pendingFormId))
            {
                modal = combinationFromList ? Modal.CombinationOptions : Modal.Detail;
                Notice("결합할 동료가 바뀌었습니다. 다시 선택해 주세요.");
                return;
            }
            CatalogForm result = database.Get(pendingFormId);
            Fill(new Rect(245, 72, 790, 576), Panel);
            GUI.Label(new Rect(277, 91, 720, 45), "결합 결과 확인", heading);
            GUI.Label(new Rect(277, 140, 710, 40),
                MemberPositionLabel(recipient.Id) + " " + database.Get(recipient.FormId).Name +
                " + " + MemberPositionLabel(donor.Id) + " " + database.Get(donor.FormId).Name +
                "  →  " + result.Name, body);
            if (art != null) art.Draw(pendingFormId, new Rect(282, 191, 170, 184),
                (int)(Time.unscaledTime * 5f) % 4);
            GUI.Label(new Rect(468, 192, 510, 43), result.Name, heading);
            GUI.Label(new Rect(468, 238, 520, 56),
                "체력 " + result.Hp.ToString("0") + "   공격 " + result.Power.ToString("0") +
                "   방어 " + result.Defense.ToString("0"), body);
            GUI.Label(new Rect(468, 293, 510, 50), "기술: " + CombinationSkill(pendingFormId), body);
            GUI.Label(new Rect(277, 386, 710, 36),
                "결과를 유지하는 동료: " + MemberPositionLabel(recipient.Id) + " " +
                database.Get(recipient.FormId).Name +
                " · 전승 특성 " + TraitName(recipient.InheritedTraitId) + " 유지", small);
            int deployedAfter = run.DeployedIds.Count -
                (IsDeployed(donor.Id) && IsDeployed(recipient.Id) ? 1 : 0);
            GUI.Label(new Rect(277, 423, 710, 85),
                "재료: " + MemberPositionLabel(donor.Id) + " " + database.Get(donor.FormId).Name +
                " · 전승 특성 " + TraitName(donor.InheritedTraitId) + "\n이 동료가 소모됩니다. 출전 " +
                run.DeployedIds.Count + "명 → " + deployedAfter + "명 · 되돌리기 가능", body);
            if (GUI.Button(new Rect(277, 545, 337, 65), "나중에", button))
                modal = combinationFromList ? Modal.CombinationOptions : Modal.Detail;
            if (GUI.Button(new Rect(650, 545, 337, 65), "결합하기", primary)) ExecuteGrowth();
        }

        static string CombinationSkill(string resultId)
        {
            // Summaries correspond to the H-01..06 execution cases in CatalogSkills.cs.
            switch (resultId)
            {
                case "H-01": return "전방 범위 공격과 화상";
                case "H-02": return "적 공격 후 가까운 아군 둘 회복";
                case "H-03": return "직선 공격과 둔화";
                case "H-04": return "범위 내 적에게 화상·둔화·피해";
                case "H-05": return "자신과 가까운 아군 회복·공격력 강화";
                case "H-06": return "적 표식과 가까운 아군의 추가 기본 공격";
                default: return "고유 특성 " + resultId;
            }
        }

        void ConfirmationButtons(string confirm, Action yes, Action no)
        {
            if (GUI.Button(new Rect(374, 462, 247, 60), "돌아가기", button)) no();
            if (GUI.Button(new Rect(657, 462, 247, 60), confirm, primary)) yes();
        }

        void ExecuteGrowth()
        {
            bool applied = pendingAction == GrowthAction.Transfer ?
                run.Transfer(pendingOtherId, selectedId, inheritTrait) :
                pendingAction == GrowthAction.Evolve ? run.Evolve(selectedId, pendingFormId) :
                pendingAction == GrowthAction.Combine && run.Combine(selectedId, pendingOtherId, pendingFormId);
            if (!applied) { Notice("현재 조건에서는 실행할 수 없습니다."); modal = Modal.Detail; return; }
            Save();
            Notice("동료의 성장이 반영되었습니다. 되돌리기로 취소할 수 있습니다.");
            if (pendingAction == GrowthAction.Transfer)
            {
                CatalogMember grown = run.Roster.Find(selectedId);
                if (ReadyToEvolve(grown)) { PrepareEvolution(grown); return; }
            }
            modal = pendingAction == GrowthAction.Combine && combinationFromList ||
                pendingAction == GrowthAction.Evolve && evolutionFromDrag ? Modal.None : Modal.Detail;
            evolutionFromDrag = false;
            combinationFromList = false;
        }

        void Summon()
        {
            string id = run.Summon();
            if (id == null) { Notice("지금은 소환할 수 없습니다."); return; }
            Save();
            benchPage = Mathf.Max(0, (run.Roster.Members.Count - run.DeployedIds.Count - 1) / 5);
            CatalogMember member = run.Roster.Find(id);
            Notice("새 동료: " + database.Get(member.FormId).Name + "  ·  대기석에 합류했습니다.");
        }

        void StartBattle(bool resume)
        {
            try
            {
                CatalogBattle battle = resume ? run.ResumeBattle() : run.BeginBattle();
                if (battle == null) { Notice("전투를 시작할 수 없습니다."); return; }
                Save();
                modal = Modal.None;
                CatalogCombatLab lab = GetComponent<CatalogCombatLab>();
                if (lab == null) lab = gameObject.AddComponent<CatalogCombatLab>();
                enabled = false;
                lab.OpenExpeditionBattle(battle, run.Wave, ExpeditionRun.TotalWaves, run.EncounterName,
                    victory => { run.ResolveBattle(victory); Save(); enabled = true; },
                    () => { home = true; Save(); enabled = true; });
            }
            catch (Exception exception) { enabled = true; Notice("전투를 열지 못했습니다: " + exception.Message); }
        }

        void OpenLab()
        {
            CatalogCombatLab lab = GetComponent<CatalogCombatLab>();
            if (lab == null) lab = gameObject.AddComponent<CatalogCombatLab>();
            enabled = false;
            lab.OpenStandalone(() => enabled = true);
        }

        bool IsDeployed(string id)
        {
            foreach (string active in run.DeployedIds) if (active == id) return true;
            return false;
        }

        string MemberPositionLabel(string id)
        {
            for (int i = 0; i < run.DeployedIds.Count; i++)
                if (run.DeployedIds[i] == id) return "출전 " + (i + 1);
            int benchIndex = 0;
            foreach (CatalogMember member in run.Roster.Members)
            {
                if (IsDeployed(member.Id)) continue;
                benchIndex++;
                if (member.Id == id) return "대기 " + benchIndex;
            }
            return "편성 밖";
        }

        static bool ReadyToEvolve(CatalogMember member)
        {
            return member != null && (member.FormId.StartsWith("B-", StringComparison.Ordinal) && member.Growth >= 2 ||
                member.FormId.StartsWith("E-", StringComparison.Ordinal) && member.Growth >= 6);
        }

        void DrawGrowthPreview()
        {
            if (hoverRecipientId == null) return;
            CatalogMember recipient = run.Roster.Find(hoverRecipientId);
            if (recipient == null) return;
            Rect target = MemberVisualRect(hoverRecipientId);
            if (target.width > 0)
            {
                Fill(new Rect(target.x - 4, target.y - 4, target.width + 8, target.height + 8),
                    hoverArmed ? new Color(.98f, .73f, .31f, .36f) : new Color(.93f, .86f, .49f, .22f));
            }
            string resultForm = recipient.FormId;
            bool growth = run.CanTransfer(pointerId, hoverRecipientId);
            CatalogMember donor = run.Roster.Find(pointerId);
            bool quickSwap = !IsDeployed(pointerId) && IsDeployed(hoverRecipientId) && run.DeployedIds.Count >= 5;
            string action = growth ? "성장 +1" :
                quickSwap ? "출전 교체" : donor != null && donor.MaterialLocked ? "보호된 재료" : "성장 불가";
            Fill(new Rect(735, 323, 493, 165), new Color(.035f, .11f, .085f, .96f));
            if (art != null) art.Draw(resultForm, new Rect(746, 334, 106, 112), 0);
            GUI.Label(new Rect(864, 337, 350, 37), action + "  →  " + database.Get(resultForm).Name, body);
            if (growth)
            {
                string detail = "진화 진행  " + recipient.Growth + " → " +
                    (recipient.Growth + 1) + " · 재료 동료 소모";
                GUI.Label(new Rect(864, 377, 342, 50), quickSwap && !hoverArmed ?
                    "빠르게 놓기: 출전 교체\n잠시 겹치기: " + action : detail, small);
                float progress = hoverArmed ? 1f : Mathf.Clamp01((Time.unscaledTime - hoverStartTime) / GrowthHoverSeconds);
                Fill(new Rect(864, 444, 336, 12), PanelLight);
                Fill(new Rect(864, 444, 336 * progress, 12), Gold);
                GUI.Label(new Rect(864, 462, 340, 24), hoverArmed ? "놓으면 " + action + " 실행" : "겹친 채 잠시 유지", small);
            }
            else GUI.Label(new Rect(864, 409, 340, 45), quickSwap ? "빠르게 놓으면 출전 교체" :
                "이 대상에게 성장시킬 수 없습니다", small);
        }

        Rect MemberVisualRect(string id)
        {
            CatalogMember member = run.Roster.Find(id);
            if (member == null) return new Rect();
            if (IsDeployed(id))
            {
                Vector2 foot = ArenaPoint(member);
                return new Rect(foot.x - 54, foot.y - 113, 108, 143);
            }
            int index = 0;
            foreach (CatalogMember candidate in run.Roster.Members)
            {
                if (IsDeployed(candidate.Id)) continue;
                if (candidate.Id == id && index / 5 == benchPage) return BenchRect(index % 5);
                index++;
            }
            return new Rect();
        }

        static string TraitName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "없음";
            for (int i = 0; i < TraitIds.Length; i++) if (TraitIds[i] == id) return TraitNames[i];
            return "알 수 없음";
        }

        static Vector2 ArenaPoint(CatalogMember member)
        {
            // Fit the full saved placement range (-2.5..2.2) without folding rows together.
            return new Vector2(Mathf.Clamp(560f + member.X * 75f, 95f, 655f),
                Mathf.Clamp(GroundY - member.Y * GroundYScale, 270f, 505f));
        }

        void Update()
        {
            if (home || run == null || run.Phase != ExpeditionPhase.Preparation ||
                modal != Modal.None || viewport.width <= 0) { CancelPointer(); return; }
            bool down = false, held = false, up = false, canceled = false;
            Vector2 screen;
            if (Input.touchCount > 0)
            {
                if (Input.touchCount > 1) { CancelPointer(); return; }
                Touch touch = Input.GetTouch(0);
                if (pointerId != null && (!pointerTouch || touch.fingerId != pointerFingerId))
                { CancelPointer(); return; }
                screen = touch.position;
                down = pointerId == null && touch.phase == TouchPhase.Began;
                canceled = touch.phase == TouchPhase.Canceled;
                up = touch.phase == TouchPhase.Ended;
                held = !up;
            }
            else
            {
                if (pointerTouch) { CancelPointer(); return; }
                screen = Input.mousePosition;
                down = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
                up = Input.GetMouseButtonUp(0);
            }
            if (canceled) { CancelPointer(); return; }
            Vector2 point = new Vector2((screen.x - viewport.x) * Width / viewport.width,
                (Screen.height - screen.y - viewport.y) * Height / viewport.height);
            ProcessPointer(point, down, held, up, Time.unscaledTime);
            if (down && Input.touchCount > 0 && pointerId != null)
            { pointerTouch = true; pointerFingerId = Input.GetTouch(0).fingerId; }
        }

        void ProcessPointer(Vector2 point, bool down, bool held, bool up, float now)
        {
            if (down)
            {
                pointerId = MemberAt(point);
                pointerStart = point;
                CatalogMember grabbed = pointerId == null ? null : run.Roster.Find(pointerId);
                pointerGrabOffset = grabbed != null && IsDeployed(pointerId) ?
                    ArenaPoint(grabbed) - point : Vector2.zero;
                pointerDragging = false;
                ResetHover();
            }
            if (held && pointerId != null && (point - pointerStart).sqrMagnitude > 144f)
                pointerDragging = true;
            pointerCurrent = point + pointerGrabOffset;
            if (pointerDragging && (held || up)) UpdateHover(point, now);
            if (!up || pointerId == null) return;
            string id = pointerId;
            bool wasDragging = pointerDragging;
            string recipientId = hoverRecipientId;
            bool armed = hoverArmed;
            CancelPointer();
            if (!wasDragging)
            { selectedId = id; modal = Modal.Detail; return; }
            if (armed && recipientId != null)
            {
                bool applied = run.Transfer(id, recipientId, false);
                if (applied)
                {
                    Save();
                    growthPulseId = recipientId;
                    growthPulseUntil = Time.unscaledTime + .6f;
                    CatalogMember grown = run.Roster.Find(recipientId);
                    Notice("성장 +1 · 되돌리기로 취소할 수 있습니다.");
                    if (ReadyToEvolve(grown)) PrepareEvolution(grown, true);
                }
                else Notice("현재 조건에서는 성장시킬 수 없습니다.");
                return;
            }
            if (recipientId != null && !IsDeployed(recipientId))
            {
                Notice("이 대상에게 성장시키려면 재료 보호와 성장 조건을 확인해 주세요.");
                return;
            }
            if (new Rect(32, 143, 687, 363).Contains(point))
            {
                if (!IsDeployed(id))
                {
                    if (run.DeployedIds.Count >= 5)
                    {
                        string replace = ActiveAt(point, id);
                        if (replace == null || !run.SwapDeployment(replace, id))
                        { Notice("교체할 출전 동료 위에 놓아 주세요."); return; }
                    }
                    else if (!run.SetDeployed(id, true))
                    { Notice("출전은 다섯 명까지 가능합니다."); return; }
                }
                if (run.Move(id, Mathf.Clamp((pointerCurrent.x - 560f) / 75f, -6.2f, -.2f),
                    Mathf.Clamp((GroundY - pointerCurrent.y) / GroundYScale, -2.5f, 2.2f))) Save();
                else Save();
            }
            else if (point.y >= 550 && IsDeployed(id))
            {
                if (run.SetDeployed(id, false)) Save();
                else Notice("대기석이 가득 찼거나 마지막 출전 동료입니다.");
            }
        }

        void UpdateHover(Vector2 point, float now)
        {
            string recipientId = MemberAt(point, pointerId);
            CatalogMember recipient = recipientId == null ? null : run.Roster.Find(recipientId);
            bool canGrow = recipient != null && run.CanTransfer(pointerId, recipientId);
            if (recipientId != hoverRecipientId || canGrow && hoverStartTime < 0f)
            {
                hoverRecipientId = recipientId;
                hoverStartTime = canGrow ? now : -1f;
                hoverArmed = false;
            }
            if (!canGrow) { hoverArmed = false; return; }
            hoverArmed = now - hoverStartTime >= GrowthHoverSeconds;
        }

        void ResetHover()
        {
            hoverRecipientId = null;
            hoverStartTime = -1f;
            hoverArmed = false;
        }

        void CancelPointer()
        {
            pointerId = null;
            pointerDragging = false;
            pointerTouch = false;
            ResetHover();
        }

        string MemberAt(Vector2 point, string except = null)
        {
            string active = ActiveAt(point, except);
            if (active != null) return active;
            int index = 0;
            foreach (CatalogMember candidate in run.Roster.Members)
            {
                if (IsDeployed(candidate.Id)) continue;
                if (candidate.Id != except && index / 5 == benchPage && BenchRect(index % 5).Contains(point))
                    return candidate.Id;
                index++;
            }
            return null;
        }

        string ActiveAt(Vector2 point, string except)
        {
            string selected = null;
            float foreground = float.NegativeInfinity;
            foreach (string id in run.DeployedIds)
            {
                if (id == except) continue;
                CatalogMember member = run.Roster.Find(id);
                if (member == null) continue;
                Vector2 foot = ArenaPoint(member);
                if (new Rect(foot.x - 54, foot.y - 113, 108, 143).Contains(point) && foot.y >= foreground)
                { selected = id; foreground = foot.y; }
            }
            return selected;
        }

        static void Fill(Rect rect, Color color)
        {
            Color before = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = before;
        }
    }
}
