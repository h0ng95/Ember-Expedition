using System;
using System.Collections.Generic;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Main expedition route: gold-funded recruitment and regional encounters.
    public sealed partial class ExpeditionScreen : MonoBehaviour
    {
        const string SaveKey = "ember.expedition.run.v013";
        const string LegacySaveKey = "ember.expedition.run.v011";
        const string JournalSaveKey = "ember.expedition.story.v025";
        const string ProgressSaveKey = "ember.expedition.progress.v027";
        const float Width = 1280f, Height = 720f;
        const float GroundY = 380f, GroundYScale = 50f;
        static readonly Color Background = ExpeditionUiTheme.Background;
        static readonly Color Panel = ExpeditionUiTheme.Panel;
        static readonly Color PanelLight = ExpeditionUiTheme.PanelLight;
        static readonly Color Cream = ExpeditionUiTheme.Cream;
        static readonly Color Gold = ExpeditionUiTheme.Gold;
        static readonly Color Muted = ExpeditionUiTheme.Muted;
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
        static readonly string[] LegacyBranches = { "tactical", "ember", "frost" };
        static readonly string[] LegacyBranchNames = { "서약", "화염", "서리" };
        static readonly string[] TacticBranches = { "defense", "precision", "arcana" };
        static readonly string[] TacticBranchNames = { "방어", "정밀", "술식" };
        static readonly string[] FormationIds = { "standard", "tactical", "ember", "frost" };
        static readonly string[] FormationNames = { "기본", "서약", "화염", "서리" };
        static readonly string[] FormationUnits = { "기사 · 궁수 · 사제", "기사 · 기수 · 사제",
            "기사 · 화염술사 · 사제", "기사 · 서리술사 · 사제" };

        enum Modal { None, Detail, ChooseDonor, ChoosePartner, CombinationOptions, Confirm, NewRun,
            Journal, Progress, LegacyResearch, Tactics, Effects, BattleReport }
        enum GrowthAction { None, Transfer, Evolve, Combine }

        CatalogDatabase database;
        ExpeditionRun run;
        ExpeditionJournal journal;
        ExpeditionProgress progress;
        CatalogArtView art;
        Texture2D battlefield, outpost, furnace, frost, garden, palace;
        Camera backgroundCamera;
        GUIStyle title, heading, body, small, centered, button, primary, hint, bossChecklist;
        GUIStyle journalTitle, journalBody, journalStatus, dialogBody;
        Font font;
        Rect viewport;
        readonly List<CatalogMember> bench = new List<CatalogMember>();
        readonly List<CatalogMember> fieldMembers = new List<CatalogMember>(5);
        bool home = true, savePresent, badSave, inheritTrait, pointerDragging, legacySave, showHelp;
        bool journalDirty, progressDirty;
        bool badProgress;
        bool pointerTouch, hoverArmed, evolutionFromDrag, combinationFromList;
        string error, selectedId, pendingOtherId, pendingFormId, toast, pointerId, selectedStoryId;
        string selectedLegacyNodeId = "tactical-start", selectedTacticNodeId = "bulwark";
        string selectedFormationId = "standard";
        string hoverRecipientId, growthPulseId;
        float toastUntil, hoverStartTime = -1f, growthPulseUntil;
        Vector2 pointerStart, pointerCurrent, pointerGrabOffset, journalScroll;
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
                frost = Resources.Load<Texture2D>("Art/Environments/v029/frost-battlefield");
                garden = Resources.Load<Texture2D>("Art/Environments/v030/garden-battlefield");
                palace = Resources.Load<Texture2D>("Art/Environments/v031/palace-battlefield");
                LoadJournal();
                LoadProgress();
                LoadSave();
                LoadTutorial();
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
        void OnDestroy()
        {
            ReleasePreparationPreview();
            if (battleReport != null) battleReport.Dispose();
            if (font != null) Destroy(font);
            ExpeditionUiTheme.Release();
            ExpeditionResearchBoard.Release();
        }
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
                ObserveJournal();
                ObserveProgress();
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
                ObserveJournal();
                ObserveProgress();
            }
            catch (Exception exception) { Notice("저장하지 못했습니다: " + exception.Message); }
        }

        void LoadJournal()
        {
            journal = ExpeditionJournal.New();
            try
            {
                if (!PlayerPrefs.HasKey(JournalSaveKey)) return;
                string json = PlayerPrefs.GetString(JournalSaveKey, "");
                if (!string.IsNullOrEmpty(json))
                    journal = ExpeditionJournal.Restore(JsonUtility.FromJson<ExpeditionJournalSave>(json));
            }
            catch (Exception) { journal = ExpeditionJournal.New(); }
        }

        void ObserveJournal()
        {
            if (run == null || journal == null) return;
            try
            {
                if (journal.Observe(run)) journalDirty = true;
                if (!journalDirty) return;
                PlayerPrefs.SetString(JournalSaveKey, JsonUtility.ToJson(journal.Capture()));
                PlayerPrefs.Save();
                journalDirty = false;
            }
            catch (Exception exception) { Notice("원정 기록을 저장하지 못했습니다: " + exception.Message); }
        }

        void LoadProgress()
        {
            progress = ExpeditionProgress.New();
            if (!PlayerPrefs.HasKey(ProgressSaveKey)) return;
            try
            {
                string json = PlayerPrefs.GetString(ProgressSaveKey, "");
                if (string.IsNullOrEmpty(json)) throw new InvalidOperationException("빈 성과 기록");
                progress = ExpeditionProgress.Restore(JsonUtility.FromJson<ExpeditionProgressSave>(json));
            }
            catch (Exception)
            {
                badProgress = true;
                progress = ExpeditionProgress.New();
            }
        }

        void ObserveProgress()
        {
            if (run == null || progress == null || badProgress) return;
            try
            {
                if (progress.Observe(run)) progressDirty = true;
                WriteProgress();
            }
            catch (Exception exception) { Notice("원정 성과를 저장하지 못했습니다: " + exception.Message); }
        }

        void WriteProgress()
        {
            if (badProgress || progress == null || !progressDirty) return;
            PlayerPrefs.SetString(ProgressSaveKey, JsonUtility.ToJson(progress.Capture()));
            PlayerPrefs.Save();
            progressDirty = false;
        }

        void NewRun()
        {
            try
            {
                ObserveProgress();
                run = ExpeditionRun.New(database, unchecked((uint)DateTime.UtcNow.Ticks),
                    ExpeditionDifficulty.Normal, badProgress || progress == null ?
                    Array.Empty<string>() : progress.ResearchIds,
                    badProgress ? "standard" : selectedFormationId);
                badSave = false;
                legacySave = false;
                error = null;
                home = false;
                modal = Modal.None;
                selectedId = null;
                benchPage = 0;
                tutorialReplayWave = 0;
                tutorialRangeAcknowledged = false;
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
            dialogBody = Style(19, Cream, false, TextAnchor.UpperLeft);
            small = Style(15, Muted, false, TextAnchor.MiddleLeft);
            centered = Style(17, Cream, true, TextAnchor.MiddleCenter);
            hint = Style(17, Gold, false, TextAnchor.MiddleLeft);
            bossChecklist = Style(17, Cream, false, TextAnchor.MiddleLeft);
            journalTitle = Style(26, ExpeditionUiTheme.Ink, true, TextAnchor.MiddleLeft);
            journalBody = Style(21, ExpeditionUiTheme.Ink, false, TextAnchor.UpperLeft);
            journalStatus = Style(17, ExpeditionUiTheme.FadedInk, false, TextAnchor.MiddleLeft);
            button = ExpeditionUiTheme.CreateButtonStyle(font, 18);
            primary = ExpeditionUiTheme.CreateButtonStyle(font, 18, true);
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
            FilterTutorialGuiEvent();
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
                ExpeditionUiTheme.PanelBox(new Rect(340, 660, 600, 48), true);
                GUI.Label(new Rect(350, 667, 580, 34), toast, centered);
            }
            DrawTutorialSpotlight();
            GUI.matrix = Matrix4x4.identity;
        }

        void DrawHome()
        {
            ExpeditionUiTheme.DrawBackdrop(new Rect(0, 90, Width, 630), battlefield);
            ExpeditionUiTheme.DrawHeaderPlate(new Rect(0, 0, Width, 90));
            ExpeditionUiTheme.DrawEmber(new Vector2(53, 46), 30);
            GUI.Label(new Rect(84, 6, 875, 60), "이그니스: 잿빛 서약", title);
            GUI.Label(new Rect(87, 62, 500, 21), "I G N I S :  A S H E N  O A T H", small);
            if (GUI.Button(new Rect(995, 20, 260, 49),
                "원정 기록  " + (journal == null ? 1 : journal.UnlockedCount) + "/" +
                ExpeditionStory.Entries.Count, button))
                OpenJournal(null);
            bool hasNotice = error != null || legacySave;
            Rect panel = new Rect(155, hasNotice ? 144 : 176, 970, hasNotice ? 432 : 368);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, content.y, content.width, 24), "I G N I S", hint);
            GUI.Label(new Rect(content.x, content.y + 32, content.width, 44),
                "꺼진 왕국에 다시 불을 밝히다", heading);
            if (GUI.Button(new Rect(content.xMax - 426, content.y + 16, 205, 48),
                "원정 연구", button))
            { selectedLegacyNodeId = "tactical-start"; modal = Modal.LegacyResearch; }
            if (GUI.Button(new Rect(content.xMax - 205, content.y + 16, 205, 48), "원정 성과", button))
                modal = Modal.Progress;
            ExpeditionUiTheme.Rule(new Rect(content.x, content.y + 88, content.width, 2));
            GUI.Label(new Rect(content.x, content.y + 104, content.width, 64),
                "이그니스가 흩어진 밤, 왕국의 결계가 무너지고 수호자들이 뒤틀렸습니다.\n" +
                "마지막 불씨를 등불에 담아, 빼앗긴 불씨 조각을 되찾으세요.", dialogBody);
            if (run != null)
                GUI.Label(new Rect(content.x, content.y + 176, content.width, 33),
                    "이어할 원정  ·  " + DifficultyLabel(run.Difficulty) + "  ·  전투 " + run.Wave +
                    " / " + run.ContentWaves, hint);
            if (hasNotice)
                GUI.Label(new Rect(content.x, footer.y - 88, content.width, 56),
                    error ?? "이전 6전투 원정은 보관되어 있습니다. 새 원정을 눌러 골드 원정을 시작하세요.", hint);
            ExpeditionUiTheme.FooterRule(panel);
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2), "새 원정", primary))
            { selectedFormationId = "standard"; modal = Modal.NewRun; }
            GUI.enabled = modal == Modal.None && run != null;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), "이어하기", button))
            { home = false; modal = Modal.None; }
            GUI.enabled = modal == Modal.None;
            if (GUI.Button(new Rect(1020, 650, 225, 48), "개발용 전투 시험실", button)) OpenLab();
            if (badProgress)
            {
                ExpeditionUiTheme.PanelBox(new Rect(155, 590, 970, 48));
                GUI.Label(new Rect(187, 598, 900, 36),
                    "성과 기록을 읽지 못했습니다. 원본을 보관하고 성과 갱신을 멈췄습니다.", small);
            }
        }

        static string DifficultyLabel(ExpeditionDifficulty difficulty)
        {
            return difficulty == ExpeditionDifficulty.Hard ? "고난도" : "일반";
        }

        void DrawHeader(string phase)
        {
            ExpeditionUiTheme.DrawHeaderPlate(new Rect(0, 0, Width, 80));
            if (GUI.Button(new Rect(18, 15, 90, 50), "거점", button)) { home = true; modal = Modal.None; Save(); }
            ExpeditionUiTheme.DrawEmber(new Vector2(146, 40), 23);
            GUI.Label(new Rect(168, 10, 312, 58), "잿빛 서약", title);
            GUI.Label(new Rect(505, 18, 365, 43), DifficultyLabel(run.Difficulty) + " · 전투 " +
                run.Wave + " / " + run.ContentWaves + "   등불 " + run.Lantern, body);
            GUI.Label(new Rect(880, 18, 170, 43), run.Gold + " G", heading);
            ExpeditionUiTheme.PanelBox(new Rect(1070, 17, 188, 43));
            GUI.Label(new Rect(1080, 21, 168, 33), phase, centered);
        }

        void DrawPreparation()
        {
            DrawHeader(run.IsBossWave ? "보스전 준비" : "전투 준비");
            Rect field = new Rect(17, 93, 1246, 449);
            Texture2D region = run.Wave <= 5 ? battlefield : run.Wave <= 10 ? outpost :
                run.Wave <= 15 ? furnace : run.Wave <= 20 ? frost :
                run.Wave <= 25 ? garden : palace;
            ExpeditionUiTheme.DrawBackdrop(field, region, false);
            Fill(new Rect(32, 143, 687, 363), new Color(.045f, .05f, .065f, .28f));
            ExpeditionUiTheme.PanelBox(new Rect(724, 94, 523, 430));
            ExpeditionUiTheme.Rule(new Rect(48, 185, 615, 1));
            ExpeditionUiTheme.Rule(new Rect(48, 350, 615, 1));
            GUI.Label(new Rect(46, 100, 470, 34), "출전 배치  " + run.DeployedIds.Count + " / 5", heading);
            DrawTutorialReplayButton();
            DrawPreparationRange();
            GUI.Label(new Rect(735, 101, 505, 37), run.EncounterName, heading);
            GUI.Label(new Rect(735, 146, 490, 35), run.EncounterDescription, hint);
            DrawTutorialAdvice();
            if (GUI.Button(new Rect(735, 270, 154, 48), showHelp ? "도움말 접기" : "배치 도움말", button))
                showHelp = !showHelp;
            if (GUI.Button(new Rect(905, 270, 154, 48), "지역 이야기", button))
            {
                StoryEntry regionStory = ExpeditionStory.ForWave(run.Wave);
                if (regionStory != null) OpenJournal(regionStory.Id);
            }
            if (GUI.Button(new Rect(1075, 270, 154, 48),
                "원정 전술 " + run.TacticIds.Count + "/" + CatalogResearch.MaxNodes, button))
            { selectedTacticNodeId = "bulwark"; modal = Modal.Tactics; }
            if (DrawSelectedPreparation()) { }
            else if (run.IsBossWave && !showHelp)
            {
                GUI.Label(new Rect(735, 326, 490, 28), "보스 준비 점검", hint);
                string[] checks = ExpeditionPreparationHints.ForBoss(run, database);
                for (int i = 0; i < checks.Length && i < 3; i++)
                    GUI.Label(new Rect(735, 358 + i * 30, 495, 29), checks[i], bossChecklist);
            }
            else
                GUI.Label(new Rect(735, 326, 480, 125), showHelp ?
                    "동료를 겹쳐 잠시 유지한 뒤 놓으면 성장 +1\n결합은 아래 목록에서 결과를 보고 선택\n재료 보호 중인 동료는 소모되지 않습니다\n출전 최대 5명 · 대기 최대 7명" :
                    "동료를 누르면 사거리·지원 범위 표시\n한 번 더 누르면 상세 · 끌어서 위치 이동\n결합은 가능한 조합 목록에서 선택", body);
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
                DrawPreparationMarker(member, foot);
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
                case 1: return "기사를 앞에, 원거리·지원 동료를 뒤에 두세요.\n소환한 동료는 대기석에 합류합니다.";
                case 2: return "가시등 짐승은 약해진 동료를 노립니다.\n전열 뒤에 원거리·지원 동료를 배치하세요.";
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
                case 15: return "용광로 폭발에 맞으면 화상이 3초 이어집니다.\n체력 45%부터 후속 불씨파가 추가됩니다.";
                case 16: return "서리 근위병이 전열을 막고 시종이 후열을 노립니다.\n회복 동료가 너무 멀리 떨어지지 않게 하세요.";
                case 17: return "근위병 셋이 앞줄을 압박합니다.\n후열의 공격 각도와 전열의 생존을 점검하세요.";
                case 18: return "시종 셋이 약한 동료를 함께 노립니다.\n보호와 회복을 나누어 배치하세요.";
                case 19: return "찬송 감시자는 가까운 적 둘에게 보호막을 줍니다.\n보호막에 막히기 전에 목표를 고르세요.";
                case 20: return "서리 원형 공격은 먼 동료 위치를 예고합니다.\n맞은 동료는 잠시 얼어 공격과 이동이 멈춥니다.";
                case 21: return "가시 근위병이 전열을 지킵니다.\n독침 사냥꾼의 사격을 받을 후열을 살피세요.";
                case 22: return "독침 사냥꾼은 세 번째 명중에 독을 남깁니다.\n중독된 동료는 회복 효과가 줄어듭니다.";
                case 23: return "사냥꾼의 독과 근위병의 전열 압박이 겹칩니다.\n해로운 상태가 걸린 동료의 체력을 살피세요.";
                case 24: return "황혼정원사는 가까운 호위 둘에게 보호막을 줍니다.\n전열을 지원 범위 안에 두고 장기전에 대비하세요.";
                case 25: return "먼 동료 최대 셋 발밑에 가시가 차례로 솟습니다.\n후열을 분산 배치하세요 · 중독 중 회복량 −35%.";
                case 26: return "왕궁 근위병이 전열을 지킵니다.\n뒤의 석궁병에게 약한 동료가 노출되지 않게 하세요.";
                case 27: return "왕궁 석궁병은 약해진 동료를 노립니다.\n전열과 후열의 거리를 다시 점검하세요.";
                case 28: return "근위병과 석궁병이 함께 압박합니다.\n성장한 동료의 전승과 배치를 살펴보세요.";
                case 29: return "집행관이 가까운 호위를 반복해서 보호합니다.\n전열을 지원 범위 안에 두고 석궁 사격을 버티세요.";
                case 30: return "직선 브레스 뒤 먼 동료를 노린 비행 폭격이 옵니다.\n착지 충격파는 용의 발밑에서 퍼집니다.";
                default: return "승리하면 골드를 얻습니다.\n소환한 동료는 대기석에 합류합니다.";
            }
        }

        void DrawDock()
        {
            ExpeditionUiTheme.PanelBox(new Rect(0, 548, Width, 172));
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
                ExpeditionUiTheme.PanelBox(card);
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
            Fill(new Rect(sprite.center.x - 28, sprite.yMax - 10, 56, 7), new Color(.02f, .025f, .035f, .75f));
            if (art != null) art.Draw(member.FormId, sprite, (int)(Time.unscaledTime * 5f) % 4);
            string label = database.Get(member.FormId).Name;
            GUI.Label(name, label, centered);
            if (member.MaterialLocked)
            {
                Fill(new Rect(sprite.x, sprite.y + 2, 54, 24), new Color(.13f, .14f, .17f, .94f));
                GUI.Label(new Rect(sprite.x, sprite.y, 54, 25), "보호", small);
            }
        }

        void DrawBattleResume()
        {
            DrawHeader("전투 대기");
            Rect panel = new Rect(185, 170, 910, 380);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 218, content.width, 62), run.EncounterName, heading);
            GUI.Label(new Rect(content.x, 285, content.width, 92),
                "이전 전투가 중단되었습니다. 같은 편성과 전투 시작 상태로 이어갑니다.", body);
            ExpeditionUiTheme.FooterRule(panel);
            if (GUI.Button(ExpeditionUiTheme.FooterRect(panel), "전투 재개", primary)) StartBattle(true);
        }

        void DrawResult()
        {
            bool victory = run.Phase == ExpeditionPhase.Completed ||
                (run.Phase == ExpeditionPhase.Result && run.LastVictory);
            StoryEntry clearStory = run.LastVictory ? ExpeditionStory.BossClear(run.Wave) : null;
            DrawHeader(run.Phase == ExpeditionPhase.Result ? "전투 결과" : "원정 결과");
            Rect panel = new Rect(175, 140, 930, 430);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(clearStory == null ? new Rect(content.x, 180, content.width, 72) :
                new Rect(content.x, 164, content.width, 60),
                run.Phase == ExpeditionPhase.Completed ? "원정 완료" :
                run.Phase == ExpeditionPhase.Defeated ? "원정 종료" : victory ? "승리" : "패배", title);
            string outcome = run.Phase == ExpeditionPhase.Completed ? "이번 원정의 길을 돌파했습니다." :
                run.Phase == ExpeditionPhase.Defeated ? "이번 원정은 여기서 끝납니다." :
                run.Phase == ExpeditionPhase.Result && run.Lantern <= 0 ?
                "등불을 모두 소진했습니다." : victory ?
                (run.Wave == ExpeditionRun.TotalWaves ? "이그니스의 불이 왕궁 결계에 돌아왔습니다." :
                "다음 길을 준비하세요.") :
                "등불 −1 · 편성을 바꾸고 같은 전투에 재도전하세요.";
            GUI.Label(clearStory == null ? new Rect(content.x, 275, content.width, 92) :
                new Rect(content.x, 232, content.width, 68),
                "승리 " + run.Victories + " / " + ExpeditionRun.TotalWaves + "     등불 " + run.Lantern +
                "\n" + outcome, body);
            if (clearStory != null)
                GUI.Label(new Rect(content.x, 306, content.width, 60), clearStory.Body, body);
            string reward = run.LastVictory && run.LastReward > 0 ?
                "+" + run.LastReward + " G   ·   " + (run.Gold - run.LastReward) + " → " + run.Gold + " G" :
                run.LastVictory && run.Wave == ExpeditionRun.TotalWaves ?
                ExpeditionRun.TotalWaves + "전투 돌파 · 원정 완료" :
                "획득 골드 0 G   ·   보유 " + run.Gold + " G";
            GUI.Label(new Rect(content.x, clearStory == null ? 370 : 376,
                HasBattleReport ? content.width - 210 : content.width, 43), reward, heading);
            if (HasBattleReport && GUI.Button(new Rect(content.xMax - 195, 370, 195, 48), "전투 기록", button))
                modal = Modal.BattleReport;
            ExpeditionUiTheme.FooterRule(panel);
            if (run.Phase == ExpeditionPhase.Result)
            {
                string next = run.Lantern <= 0 || run.LastVictory && run.Wave == ExpeditionRun.TotalWaves ?
                    "원정 결과" : run.LastVictory ? "다음 전투 준비  ›" : "같은 전투 재도전  ›";
                if (GUI.Button(ExpeditionUiTheme.FooterRect(panel), next, primary))
                { if (run.Continue()) { Save(); selectedId = null; } }
            }
            else if (GUI.Button(ExpeditionUiTheme.FooterRect(panel), "거점으로", primary)) home = true;
        }

        void DrawModal()
        {
            Fill(new Rect(0, 0, Width, Height), new Color(.005f, .008f, .015f, .84f));
            if (modal == Modal.Detail) DrawDetail();
            else if (modal == Modal.Journal) DrawJournal();
            else if (modal == Modal.Progress) DrawProgress();
            else if (modal == Modal.LegacyResearch) DrawLegacyResearch();
            else if (modal == Modal.Tactics) DrawTactics();
            else if (modal == Modal.Effects) DrawMemberEffects();
            else if (modal == Modal.BattleReport) DrawBattleReport();
            else if (modal == Modal.ChooseDonor || modal == Modal.ChoosePartner) DrawChooseMember();
            else if (modal == Modal.CombinationOptions) DrawCombinationOptions();
            else DrawConfirmation();
        }

        void OpenJournal(string entryId)
        {
            CancelPointer();
            if (entryId == null)
            {
                entryId = "intro";
                foreach (StoryEntry entry in ExpeditionStory.Entries)
                    if (journal != null && journal.IsUnlocked(entry.Id)) entryId = entry.Id;
            }
            selectedStoryId = entryId;
            int selectedIndex = 0;
            for (int i = 0; i < ExpeditionStory.Entries.Count; i++)
                if (ExpeditionStory.Entries[i].Id == entryId) { selectedIndex = i; break; }
            journalScroll = new Vector2(0, Mathf.Clamp((selectedIndex + 1) * 52 - 440,
                0, Mathf.Max(0, ExpeditionStory.Entries.Count * 52 + 4 - 446)));
            modal = Modal.Journal;
        }

        void DrawJournal()
        {
            Rect panel = new Rect(128, 66, 1024, 588);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 98, 700, 51),
                "원정 기록  " + (journal == null ? 1 : journal.UnlockedCount) + "/" +
                ExpeditionStory.Entries.Count, heading);
            GUI.Label(new Rect(content.x + 2, 137, 680, 21), "F I E L D   J O U R N A L", small);
            if (GUI.Button(new Rect(content.xMax - 77, 98, 77, 50), "닫기", button))
            { modal = Modal.None; return; }
            ExpeditionUiTheme.PanelBox(new Rect(content.x, 163, 320, 459));
            Rect journalViewport = new Rect(content.x + 10, 170, 300, 443);
            journalScroll = GUI.BeginScrollView(journalViewport, journalScroll,
                new Rect(0, 0, 281, ExpeditionStory.Entries.Count * 52 + 4), false,
                ExpeditionStory.Entries.Count > 8);
            for (int i = 0; i < ExpeditionStory.Entries.Count; i++)
            {
                StoryEntry entry = ExpeditionStory.Entries[i];
                bool unlocked = journal != null && journal.IsUnlocked(entry.Id);
                Rect row = new Rect(4, 4 + i * 52, 270, 48);
                if (entry.Id == selectedStoryId)
                    Fill(new Rect(row.x - 3, row.y - 3, row.width + 6, row.height + 6),
                        new Color(.69f, .51f, .26f, .65f));
                if (GUI.Button(row, (unlocked ? "열림  ·  " : "잠김  ·  ") + entry.Title, button))
                    selectedStoryId = entry.Id;
            }
            GUI.EndScrollView();
            ExpeditionUiTheme.ParchmentBox(new Rect(496, 163, 624, 459));
            StoryEntry selectedStory = ExpeditionStory.Get(selectedStoryId) ?? ExpeditionStory.Get("intro");
            bool selectedUnlocked = journal != null && journal.IsUnlocked(selectedStory.Id);
            GUI.Label(new Rect(528, 190, 560, 53), selectedStory.Title, journalTitle);
            GUI.Label(new Rect(529, 243, 559, 34), selectedUnlocked ? "원정대의 기록" : "아직 잠긴 기록", journalStatus);
            ExpeditionUiTheme.Rule(new Rect(528, 285, 560, 2));
            GUI.Label(new Rect(529, 310, 559, 272), selectedUnlocked ? selectedStory.Body :
                StoryUnlockHint(selectedStory.Id), journalBody);
        }

        void DrawProgress()
        {
            Rect panel = new Rect(250, 170, 780, 380);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 202, content.width, 48), "원정 성과", heading);
            GUI.Label(new Rect(content.x, 248, content.width, 32),
                "현재 공개 " + ExpeditionRun.TotalWaves + "전투  ·  전체 여정 목표 30전투", hint);
            ExpeditionUiTheme.Rule(new Rect(content.x, 285, content.width, 2));
            GUI.Label(new Rect(content.x, 300, content.width, 58), ProgressLine(), body);
            ExpeditionUiTheme.Rule(new Rect(content.x, 365, content.width, 2));
            GUI.Label(new Rect(content.x, 378, content.width, 28),
                "일반 30전투 완료 시 고난도 도전 자격 " +
                (badProgress ? "확인 불가" : progress != null && progress.HardUnlocked ? "획득" : "미획득") +
                " · 고난도 전투는 준비 중", small);
            if (badProgress)
                GUI.Label(new Rect(content.x, 407, content.width, 24),
                    "성과 저장 오류: 기존 기록을 보관하며 갱신하지 않습니다.", small);
            ExpeditionUiTheme.FooterRule(panel);
            if (GUI.Button(ExpeditionUiTheme.FooterRect(panel), "닫기", button)) modal = Modal.None;
        }

        string ProgressLine()
        {
            if (badProgress) return "일반  ·  성과 기록을 읽을 수 없습니다.";
            int best = progress == null ? 0 : progress.BestWave;
            int completed = progress == null ? 0 : progress.CompletedWaves;
            return "일반  ·  최고 격파 " + best + "전투" +
                "\n구간 완료 " + completed + " / " + ExpeditionRun.ReleaseWaves + "전투";
        }

        static bool HasId(IReadOnlyList<string> ids, string id)
        {
            if (ids == null) return false;
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] == id) return true;
            return false;
        }

        static CatalogLegacyResearchNode LegacyNode(string branch, int tier)
        {
            foreach (CatalogLegacyResearchNode node in CatalogLegacyResearch.Nodes)
                if (node.BranchId == branch && node.Tier == tier) return node;
            return null;
        }

        static CatalogResearchNode TacticNode(string branch, int tier)
        {
            foreach (CatalogResearchNode node in CatalogResearch.Nodes)
                if (node.BranchId == branch && node.Tier == tier) return node;
            return null;
        }

        static string StoryUnlockHint(string id)
        {
            switch (id)
            {
                case "forest": return "새 원정을 시작하면 열립니다.";
                case "forest-clear": return "숲길의 보스를 쓰러뜨리면 열립니다.";
                case "castle": return "전투 6에 도달하면 열립니다.";
                case "castle-clear": return "성문의 보스를 쓰러뜨리면 열립니다.";
                case "mine": return "전투 11에 도달하면 열립니다.";
                case "mine-clear": return "용광로 수호자를 쓰러뜨리면 열립니다.";
                case "frost": return "전투 16에 도달하면 열립니다.";
                case "frost-clear": return "서리의 파수꾼을 쓰러뜨리면 열립니다.";
                case "garden": return "전투 21에 도달하면 열립니다.";
                case "garden-clear": return "가시의 군주를 쓰러뜨리면 열립니다.";
                case "palace": return "전투 26에 도달하면 열립니다.";
                case "palace-clear": return "왕관의 수호룡을 쓰러뜨리면 열립니다.";
                default: return "원정을 진행하면 열립니다.";
            }
        }

        void DrawDetail()
        {
            CatalogMember member = run == null ? null : run.Roster.Find(selectedId);
            if (member == null) { modal = Modal.None; return; }
            Rect panel = new Rect(245, 75, 790, 570);
            ExpeditionUiTheme.Window(panel);
            CatalogForm form = database.Get(member.FormId);
            GUI.Label(new Rect(277, 99, 560, 48), form.Name, heading);
            if (GUI.Button(new Rect(943, 108, 60, 54), "×", button)) { modal = Modal.None; return; }
            if (art != null) art.Draw(member.FormId, new Rect(290, 154, 155, 175), (int)(Time.unscaledTime * 5f) % 4);
            GUI.Label(new Rect(468, 157, 535, 72),
                "체력 " + form.Hp.ToString("0") + "   공격 " + form.Power.ToString("0") +
                "   방어 " + form.Defense.ToString("0") + "\n" +
                "공격 간격 " + form.AttackInterval.ToString("0.0") + "초   사거리 " + form.Range.ToString("0.0"), body);
            int target = member.FormId.StartsWith("B-", StringComparison.Ordinal) ? 2 :
                member.FormId.StartsWith("E-", StringComparison.Ordinal) ? 6 : 0;
            HybridGuidance guidance = HybridGuide(member.FormId);
            EnsureRuneStyles();
            GUI.Label(new Rect(468, 239, 535, 88), guidance != null ? "기술: " + HybridSkillText(form, guidance) :
                target > 0 ? "진화 진행  " + member.Growth + " / " + target : "최종 형태", runeBody);
            GUI.Label(new Rect(277, 334, 726, 30), "고유 특성: " + TraitName(form.InnateTrait) +
                "    전승 특성: " + TraitName(member.InheritedTraitId), small);
            if (guidance != null)
                GUI.Label(new Rect(277, 368, 726, 49), guidance.Tactic, hint);
            else
                GUI.Label(new Rect(277, 368, 726, 40), "성장과 편성", heading);
            Rect firstActions = new Rect(277, 430, 726, 58), secondActions = new Rect(277, 504, 726, 58);
            if (GUI.Button(ExpeditionUiTheme.FooterButton(firstActions, 0, 3), IsDeployed(member.Id) ? "대기석으로" : "출전시키기", button))
            {
                if (run.SetDeployed(member.Id, !IsDeployed(member.Id))) { Save(); modal = Modal.None; }
                else Notice("출전 1~5명 · 대기석 최대 7명을 유지해야 합니다.");
            }
            if (GUI.Button(ExpeditionUiTheme.FooterButton(firstActions, 1, 3), "전승", button))
            { inheritTrait = false; choosePage = 0; modal = Modal.ChooseDonor; }
            GUI.enabled = target > 0 && member.Growth >= target;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(firstActions, 2, 3), "진화", button)) PrepareEvolution(member);
            GUI.enabled = true;
            GUI.enabled = member.FormId.StartsWith("E-", StringComparison.Ordinal);
            if (GUI.Button(ExpeditionUiTheme.FooterButton(secondActions, 1, 3), "결합", button))
            { choosePage = 0; modal = Modal.ChoosePartner; }
            GUI.enabled = true;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(secondActions, 0, 3), member.MaterialLocked ? "재료 보호 해제" : "재료 보호", button))
            {
                if (run.SetMaterialLocked(member.Id, !member.MaterialLocked)) Save();
                else Notice("재료 보호 상태를 바꾸지 못했습니다.");
            }
            if (GUI.Button(ExpeditionUiTheme.FooterButton(secondActions, 2, 3), "적용 효과", button))
                OpenMemberEffects(member);
            GUI.Label(new Rect(277, 578, 726, 36), "끌어놓기로 성장합니다. 재료 보호 중인 동료는 전승·결합에 쓰이지 않습니다.", small);
        }

        void DrawChooseMember()
        {
            bool transfer = modal == Modal.ChooseDonor;
            Rect panel = new Rect(215, 75, 850, 585);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 107, content.width - 80, 52),
                transfer ? "전승할 동료 선택" : "결합할 동료 선택", heading);
            if (GUI.Button(new Rect(content.xMax - 59, 107, 59, 50), "×", button))
            { modal = Modal.Detail; return; }
            if (transfer)
            {
                if (GUI.Button(new Rect(content.x, 166, content.width, 48),
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
            const int pageSize = 10;
            int pages = Mathf.Max(1, (candidates.Count + pageSize - 1) / pageSize);
            choosePage = Mathf.Clamp(choosePage, 0, pages - 1);
            int shown = 0;
            for (int index = choosePage * pageSize; index < candidates.Count && shown < pageSize; index++)
            {
                CatalogMember other = candidates[index];
                string result = transfer ? null : run.Roster.HybridResult(selected.FormId, other.FormId);
                int col = shown % 2, row = shown / 2;
                Rect slot = new Rect(content.x + col * 401, 224 + row * 62, 385, 54);
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
            if (shown == 0) GUI.Label(new Rect(content.x, 248, content.width, 95),
                transfer ? "전승할 다른 동료가 없습니다." : "지금 결합할 수 있는 동료가 없습니다.", body);
            if (pages > 1)
            {
                ExpeditionUiTheme.FooterRule(panel);
                if (GUI.Button(new Rect(footer.x, footer.y, 112, 64), "이전", button)) choosePage--;
                GUI.Label(new Rect(footer.center.x - 60, footer.y + 17, 120, 30),
                    (choosePage + 1) + " / " + pages, centered);
                if (GUI.Button(new Rect(footer.xMax - 112, footer.y, 112, 64), "다음", button)) choosePage++;
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
            Rect panel = new Rect(215, 75, 850, 585);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 107, content.width - 80, 52),
                "가능한 결합  " + options.Count + "쌍", heading);
            if (GUI.Button(new Rect(content.xMax - 59, 107, 59, 50), "×", button))
            { modal = Modal.None; return; }
            GUI.Label(new Rect(content.x, 160, content.width, 34),
                "결합은 선택 사항입니다. 결과와 재료를 확인한 뒤 진행하세요.", small);
            const int pageSize = 4;
            int pages = Mathf.Max(1, (options.Count + pageSize - 1) / pageSize);
            combinationPage = Mathf.Clamp(combinationPage, 0, pages - 1);
            for (int row = 0; row < pageSize; row++)
            {
                int index = combinationPage * pageSize + row;
                if (index >= options.Count) break;
                CatalogCombinationOption option = options[index];
                CatalogMember recipient = run.Roster.Find(option.RecipientId);
                CatalogMember donor = run.Roster.Find(option.DonorId);
                if (recipient == null || donor == null) continue;
                string label = database.Get(option.ResultId).Name + " 결합\n" +
                    "남는 동료 " + MemberPositionLabel(recipient.Id) + " " + database.Get(recipient.FormId).Name +
                    "  +  재료 " + MemberPositionLabel(donor.Id) + " " + database.Get(donor.FormId).Name +
                    " · 전승 " + TraitName(donor.InheritedTraitId);
                if (GUI.Button(new Rect(content.x, 204 + row * 78, content.width, 70), label, button))
                    ChooseCombinationOption(index);
            }
            if (options.Count == 0) GUI.Label(new Rect(content.x, 240, content.width, 80),
                "가능한 결합이 없습니다.", body);
            ExpeditionUiTheme.FooterRule(panel);
            if (pages > 1)
            {
                if (GUI.Button(new Rect(footer.x, footer.y, 112, 64), "이전", button)) combinationPage--;
                GUI.Label(new Rect(footer.x + 122, footer.y + 17, 120, 30),
                    (combinationPage + 1) + " / " + pages, centered);
                if (GUI.Button(new Rect(footer.x + 252, footer.y, 112, 64), "다음", button)) combinationPage++;
            }
            if (GUI.Button(new Rect(footer.xMax - 192, footer.y, 192, 64), "나중에", button))
                modal = Modal.None;
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
            if (modal == Modal.NewRun)
            {
                Rect panel = new Rect(205, 70, 870, 580);
                Rect content = ExpeditionUiTheme.ContentRect(panel);
                ExpeditionUiTheme.Window(panel);
                GUI.Label(new Rect(content.x, panel.y + 32, content.width, 48),
                    "새 원정", heading);
                string warning = savePresent ?
                    "저장된 원정을 새 출발로 교체합니다. 진행과 편성은 되돌릴 수 없습니다." :
                    legacySave ? "이전 6전투 원정은 별도로 보관됩니다." :
                    "등불과 기본 동료 3명으로 새로 출발합니다.";
                GUI.Label(new Rect(content.x, 152, content.width, 50),
                    "현재 공개 " + ExpeditionRun.TotalWaves + "전투 · 전체 목표 30전투\n" + warning, small);
                Rect normal = new Rect(content.x, 216, content.width, 62);
                ExpeditionUiTheme.PanelBox(normal, true);
                GUI.Label(new Rect(normal.x + 20, normal.y + 6, normal.width - 40, 28),
                    "NORMAL · 일반  ✓", body);
                GUI.Label(new Rect(normal.x + 20, normal.y + 34, normal.width - 40, 23),
                    "현재 플레이 가능 · 기본 전투 규칙", small);
                Rect locked = new Rect(content.x, 288, content.width, 62);
                bool wasEnabled = GUI.enabled;
                GUI.enabled = false;
                GUI.Button(locked, "", button);
                GUI.enabled = wasEnabled;
                GUI.Label(new Rect(locked.x + 20, locked.y + 6, locked.width - 40, 28),
                    "HARD · 고난도  ·  준비 중", body);
                GUI.Label(new Rect(locked.x + 20, locked.y + 34, locked.width - 40, 23),
                    progress != null && progress.HardUnlocked && !badProgress ?
                    "도전 자격 획득 · 전투는 추후 공개" :
                    "일반 30전투 완료 후 도전 자격 · 전투는 추후 공개", small);
                GUI.Label(new Rect(content.x, 364, content.width, 31), "시작 편성 선택", hint);
                IReadOnlyList<string> research = badProgress || progress == null ?
                    Array.Empty<string>() : progress.ResearchIds;
                for (int i = 0; i < FormationIds.Length; i++)
                {
                    float cardWidth = (content.width - 3 * 12f) / 4f;
                    Rect card = new Rect(content.x + i * (cardWidth + 12f), 402, cardWidth, 116);
                    bool unlocked = CatalogLegacyResearch.IsFormationAvailable(FormationIds[i], research);
                    if (selectedFormationId == FormationIds[i])
                        Fill(new Rect(card.x - 3, card.y - 3, card.width + 6, card.height + 6),
                            new Color(Gold.r, Gold.g, Gold.b, .8f));
                    GUI.enabled = wasEnabled && unlocked;
                    if (GUI.Button(card, GUIContent.none, button)) selectedFormationId = FormationIds[i];
                    GUI.enabled = wasEnabled;
                    GUI.Label(new Rect(card.x + 13, card.y + 9, card.width - 26, 31),
                        FormationNames[i], centered);
                    GUI.Label(new Rect(card.x + 12, card.y + 45, card.width - 24, 30),
                        FormationUnits[i], small);
                    GUI.Label(new Rect(card.x + 12, card.y + 82, card.width - 24, 24),
                        unlocked ? selectedFormationId == FormationIds[i] ? "선택됨" : "선택 가능" :
                        badProgress ? "기록 확인 불가" : "원정 연구 필요", small);
                }
                ExpeditionUiTheme.FooterRule(panel);
                ConfirmationButtons(ExpeditionUiTheme.FooterRect(panel),
                    "새 원정 시작", NewRun, () => modal = Modal.None);
                return;
            }
            CatalogMember selected = run.Roster.Find(selectedId);
            CatalogMember other = run.Roster.Find(pendingOtherId);
            if (selected == null) { modal = Modal.None; return; }
            bool chooseBranch = pendingAction == GrowthAction.Evolve &&
                selected.FormId.StartsWith("B-", StringComparison.Ordinal);
            Rect growthPanel = new Rect(335, chooseBranch ? 136 : 180, 610, chooseBranch ? 448 : 360);
            Rect growthContent = ExpeditionUiTheme.ContentRect(growthPanel);
            ExpeditionUiTheme.Window(growthPanel);
            string headingText = pendingAction == GrowthAction.Transfer ? "전승 확인" :
                pendingAction == GrowthAction.Combine ? "결합 확인" : "진화 확인";
            GUI.Label(new Rect(growthContent.x, growthPanel.y + 32, growthContent.width, 48),
                headingText, heading);
            string selectedName = database.Get(selected.FormId).Name;
            string resultName = pendingAction == GrowthAction.Transfer ? selectedName :
                pendingFormId == null ? "" : database.Get(pendingFormId).Name;
            string message = pendingAction == GrowthAction.Evolve ? selectedName + " → " + resultName :
                other == null ? "동료를 다시 선택해 주세요." :
                selectedName + " + " + database.Get(other.FormId).Name + " → " + resultName +
                "\n" + database.Get(other.FormId).Name + "은(는) 재료로 떠납니다.";
            if (pendingAction == GrowthAction.Transfer)
                message += "\n진화 진행이 가능하면 1 증가" + (inheritTrait ? " · 고유 특성 전승" : "");
            GUI.Label(new Rect(growthContent.x, growthPanel.y + 100, growthContent.width,
                chooseBranch ? 86 : 110), message, dialogBody);
            if (chooseBranch)
            {
                string family = CatalogRoster.FamilyCode(selected.FormId);
                int familyIndex = Array.IndexOf(Families, family);
                if (familyIndex >= 0)
                {
                    Rect branchRow = new Rect(growthContent.x, growthPanel.y + 200,
                        growthContent.width, 56);
                    if (GUI.Button(ExpeditionUiTheme.FooterButton(branchRow, 0, 2),
                        database.Get(Branches[familyIndex][0]).Name, button))
                        pendingFormId = Branches[familyIndex][0];
                    if (GUI.Button(ExpeditionUiTheme.FooterButton(branchRow, 1, 2),
                        database.Get(Branches[familyIndex][1]).Name, button))
                        pendingFormId = Branches[familyIndex][1];
                    GUI.Label(new Rect(growthContent.x, growthPanel.y + 270,
                        growthContent.width, 30), "선택: " + database.Get(pendingFormId).Name, small);
                }
            }
            ExpeditionUiTheme.FooterRule(growthPanel);
            ConfirmationButtons(ExpeditionUiTheme.FooterRect(growthPanel), "실행", ExecuteGrowth,
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
            HybridGuidance guidance = HybridGuide(pendingFormId);
            Rect panel = new Rect(205, 35, 870, 650);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            ExpeditionUiTheme.Window(panel);
            GUI.Label(new Rect(content.x, 67, content.width, 45), "결합 결과 확인", heading);
            GUI.Label(new Rect(content.x, 117, content.width, 52),
                MemberPositionLabel(recipient.Id) + " " + database.Get(recipient.FormId).Name +
                " + " + MemberPositionLabel(donor.Id) + " " + database.Get(donor.FormId).Name +
                "  →  " + result.Name, body);
            if (art != null) art.Draw(pendingFormId, new Rect(content.x + 5, 176, 170, 184),
                (int)(Time.unscaledTime * 5f) % 4);
            GUI.Label(new Rect(440, 173, content.xMax - 440, 43), result.Name, heading);
            GUI.Label(new Rect(440, 220, content.xMax - 440, 40),
                "체력 " + result.Hp.ToString("0") + "   공격 " + result.Power.ToString("0") +
                "   방어 " + result.Defense.ToString("0"), body);
            EnsureRuneStyles();
            GUI.Label(new Rect(440, 270, content.xMax - 440, 94),
                "기술: " + (guidance == null ? "고유 특성 " + pendingFormId : HybridSkillText(result, guidance)), runeBody);
            if (guidance != null)
                GUI.Label(new Rect(content.x, 373, content.width, 46), guidance.Tactic, hint);
            GUI.Label(new Rect(content.x, 429, content.width, 32),
                "결과를 유지하는 동료: " + MemberPositionLabel(recipient.Id) + " " +
                database.Get(recipient.FormId).Name +
                " · 전승 특성 " + TraitName(recipient.InheritedTraitId) + " 유지", small);
            int deployedAfter = run.DeployedIds.Count -
                (IsDeployed(donor.Id) && IsDeployed(recipient.Id) ? 1 : 0);
            GUI.Label(new Rect(content.x, 469, content.width, 65),
                "재료: " + MemberPositionLabel(donor.Id) + " " + database.Get(donor.FormId).Name +
                " · 전승 특성 " + TraitName(donor.InheritedTraitId) + "\n이 동료가 소모됩니다. 출전 " +
                run.DeployedIds.Count + "명 → " + deployedAfter + "명 · 되돌리기 가능", body);
            ExpeditionUiTheme.FooterRule(panel);
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2), "나중에", button))
                modal = combinationFromList ? Modal.CombinationOptions : Modal.Detail;
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), "결합하기", primary)) ExecuteGrowth();
        }

        sealed class HybridGuidance
        {
            public readonly string Skill;
            public readonly string Tactic;

            public HybridGuidance(string skill, string role, string placement)
            {
                Skill = skill;
                Tactic = "역할: " + role + "\n배치: " + placement;
            }
        }

        static string HybridSkillText(CatalogForm form, HybridGuidance guidance)
        { return string.IsNullOrEmpty(form.Description) ? guidance.Skill : form.Description; }

        static readonly HybridGuidance H01Guide = new HybridGuidance("전방 화염 공격 · 적중 후 자신 보호막", "전열 범위 공격·자기 보호", "적이 모이는 앞줄");
        static readonly HybridGuidance H02Guide = new HybridGuidance("근접 타격·주변 최대 2명 회복(자신 가능)", "전열 근접 회복", "전열 곁 · 먼 후열은 회복 불가");
        static readonly HybridGuidance H03Guide = new HybridGuidance("직선 관통 공격과 이동 둔화", "후열 관통·이동 둔화", "적의 진로가 겹치는 뒤쪽");
        static readonly HybridGuidance H04Guide = new HybridGuidance("범위 내 적에게 화상·둔화·피해", "후열 범위 견제", "뭉친 적을 겨냥하는 뒤쪽");
        static readonly HybridGuidance H05Guide = new HybridGuidance("자신과 부상자 2명 우선 회복·공격력 강화", "부상자 회복·공격 지원", "아군이 거리 4.5 안에 들어오는 중열");
        static readonly HybridGuidance H06Guide = new HybridGuidance("표식·5초 안 다음 기본공격 1회 +60%", "후열 단발 공격 지원", "본인 + 거리 3 내 가까운 아군 2명");

        static HybridGuidance HybridGuide(string formId)
        {
            // Match CatalogSkills' H-01..06 effects and the forms' attack ranges.
            switch (formId)
            {
                case "H-01": return H01Guide;
                case "H-02": return H02Guide;
                case "H-03": return H03Guide;
                case "H-04": return H04Guide;
                case "H-05": return H05Guide;
                case "H-06": return H06Guide;
                default: return null;
            }
        }

        void ConfirmationButtons(Rect footer, string confirm, Action yes, Action no)
        {
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2), "돌아가기", button)) no();
            if (GUI.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), confirm, primary)) yes();
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
                FinishTutorialWave(run.Wave);
                BeginReport(battle);
                Save();
                modal = Modal.None;
                CatalogCombatLab lab = GetComponent<CatalogCombatLab>();
                if (lab == null) lab = gameObject.AddComponent<CatalogCombatLab>();
                enabled = false;
                lab.OpenExpeditionBattle(battle, run.Wave, ExpeditionRun.TotalWaves, run.EncounterName,
                    victory => { run.ResolveBattle(victory); CompleteReport(); Save(); enabled = true; },
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
            ExpeditionUiTheme.PanelBox(new Rect(735, 323, 493, 165), true);
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
            if (!TutorialPointerAllowed(point, down, up))
            {
                if (up) CancelPointer();
                return;
            }
            ProcessPointer(point, down, held, up, Time.unscaledTime);
            if (down && Input.touchCount > 0 && pointerId != null)
            { pointerTouch = true; pointerFingerId = Input.GetTouch(0).fingerId; }
        }

        void ProcessPointer(Vector2 point, bool down, bool held, bool up, float now)
        {
            if (down)
            {
                PreparationSelection();
                pointerId = MemberAt(point);
                pointerStart = point;
                CatalogMember grabbed = pointerId == null ? null : run.Roster.Find(pointerId);
                pointerGrabOffset = grabbed != null && IsDeployed(pointerId) ?
                    ArenaPoint(grabbed) - point : Vector2.zero;
                pointerDragging = false;
                ResetHover();
            }
            if (held && pointerId != null && (point - pointerStart).sqrMagnitude > 144f)
            {
                if (!pointerDragging && IsDeployed(pointerId)) SelectPreparation(pointerId);
                pointerDragging = true;
            }
            pointerCurrent = point + pointerGrabOffset;
            if (pointerDragging && (held || up)) UpdateHover(point, now);
            if (!up || pointerId == null) return;
            string id = pointerId;
            bool wasDragging = pointerDragging;
            string recipientId = hoverRecipientId;
            bool armed = hoverArmed;
            CancelPointer();
            if (!wasDragging)
            {
                if (IsDeployed(id))
                {
                    if (preparationSelectionId == id) { selectedId = id; modal = Modal.Detail; }
                    else SelectPreparation(id);
                }
                else { selectedId = id; modal = Modal.Detail; }
                return;
            }
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
                SelectPreparation(id);
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
