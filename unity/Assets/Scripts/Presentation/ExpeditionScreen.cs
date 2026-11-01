using System;
using System.Collections.Generic;
using System.IO;
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
        const float GroundY = PreparationLayout.OriginY, GroundYScale = PreparationLayout.ScaleY;
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
        static readonly CatalogLocalizedStrings TraitNames = new CatalogLocalizedStrings("text.0580", "text.0581", "text.0582", "text.0583", "text.0584", "text.0585",
            "text.0586", "text.0587", "text.0588", "text.0589", "text.0590", "text.0591");
        static readonly string[] LegacyBranches = { "tactical", "ember", "frost" };
        static readonly CatalogLocalizedStrings LegacyBranchNames = new CatalogLocalizedStrings("text.0585", "text.0592", "text.0593");
        static readonly string[] TacticBranches = { "defense", "precision", "arcana" };
        static readonly CatalogLocalizedStrings TacticBranchNames = new CatalogLocalizedStrings("text.0594", "text.0595", "text.0596");
        static readonly string[] FormationIds = { "standard", "tactical", "ember", "frost" };
        static readonly CatalogLocalizedStrings FormationNames = new CatalogLocalizedStrings("text.0597", "text.0585", "text.0592", "text.0593");
        static readonly CatalogLocalizedStrings FormationUnits = new CatalogLocalizedStrings("text.0598", "text.0599",
            "text.0600", "text.0601");

        enum Modal { None, Detail, ChooseDonor, ChoosePartner, CombinationOptions, Confirm, NewRun,
            Journal, Progress, Tactics, Effects, BattleReport, Playtest, GuideMenu, Settings, Records, Encounter }
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
        Vector2 pointerStart, pointerCurrent, pointerGrabOffset, journalScroll, journalBodyScroll;
        int journalDragPane;
        float journalDragStartY, journalDragStartScroll;
        bool journalDragging;
        int benchPage, choosePage, combinationPage, pointerFingerId;
        Modal modal;
        GrowthAction pendingAction;
        int presentationLanguageRevision = -1;

        void Awake()
        {
            GamePreferences.Load();
            playtest = GetComponent<PlaytestRecorder>();
            if (playtest == null) playtest = gameObject.AddComponent<PlaytestRecorder>();
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            backgroundCamera = GetComponent<Camera>();
            if (backgroundCamera == null) backgroundCamera = gameObject.AddComponent<Camera>();
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = Background;
            backgroundCamera.cullingMask = 0;
            backgroundCamera.depth = -10;
        }

        void Start() { BeginStartup(); }
        void OnEnable() { if (startupReady && art == null) art = new CatalogArtView(); }
        void OnDisable() { CancelPointer(); if (art != null) { art.Dispose(); art = null; } }
        void OnDestroy()
        {
            if (ending != null) ending.Dispose();
            ReleasePreparationPreview();
            if (battleReport != null) battleReport.Dispose();
            if (font != null) Destroy(font);
            ExpeditionUiTheme.Release();
            ExpeditionResearchBoard.Release();
            LocalizedGui.ClearCache();
        }
        void OnApplicationPause(bool paused) { if (paused) { CancelPointer(); GamePreferences.TrySave(); Save(); } }
        void OnApplicationFocus(bool focused) { if (!focused) CancelPointer(); }

        void LoadSave()
        {
            savePresent = loadedAccount != null ? loadedAccount.Run != null : PlayerPrefs.HasKey(SaveKey);
            if (storageBlocked) return;
            if (loadedAccount != null && loadedAccount.Run == null) { savePresent = false; return; }
            legacySave = !savePresent && PlayerPrefs.HasKey(LegacySaveKey);
            if (!savePresent) return;
            try
            {
                string json = loadedAccount != null ? JsonUtility.ToJson(loadedAccount.Run) : PlayerPrefs.GetString(SaveKey, "");
                if (string.IsNullOrEmpty(json)) throw new InvalidOperationException(CatalogText.Get("text.0602"));
                ExpeditionRunSave state = JsonUtility.FromJson<ExpeditionRunSave>(json);
                if (state == null) throw new InvalidOperationException(CatalogText.Get("text.0603"));
                run = ExpeditionRun.Restore(database, state);
                playtest.Begin(run, null);
                ObserveJournal();
                ObserveProgress();
            }
            catch (Exception exception)
            {
                badSave = true;
                run = null;
                error = CatalogText.Get("text.0604") +
                    exception.Message + ")";
            }
        }

        void Save(string diagnosticAction = "checkpoint", string detail = null, float battleSeconds = -1)
        {
            if (!startupReady || run == null || badSave) return;
            try
            {
                savingAccount = true;
                ObserveLessonAction(diagnosticAction);
                ObserveJournal();
                if (run.Wave == 30 && run.LastVictory && endingActors.Count > 0 && journal != null)
                    journalDirty |= journal.RecordEndingParty(endingActors);
                ObserveProgress();
                PersistAccount();
                playtest.Record(diagnosticAction, run, detail, battleSeconds);
            }
            catch (Exception exception) { Notice(CatalogText.Get("text.0605") + exception.Message); }
            finally { savingAccount = false; }
        }

        void LoadJournal()
        {
            InitializeAccountStorage();
            journal = ExpeditionJournal.New();
            if (storageBlocked) return;
            try
            {
                if (loadedAccount != null) { journal = ExpeditionJournal.Restore(loadedAccount.Journal); return; }
                if (!PlayerPrefs.HasKey(JournalSaveKey)) return;
                string json = PlayerPrefs.GetString(JournalSaveKey, "");
                var state = JsonUtility.FromJson<ExpeditionJournalSave>(json);
                if (state == null || state.Version != 1 || state.UnlockedIds == null) throw new InvalidDataException("Invalid story save.");
                journal = ExpeditionJournal.Restore(state);
            }
            catch (Exception) { storageBlocked = badProgress = true; error = CatalogText.Get("text.0606"); }
        }

        void ObserveJournal()
        {
            if (run == null || journal == null) return;
            try
            {
                if (journal.Observe(run)) journalDirty = true;
            }
            catch (Exception exception) { Notice(CatalogText.Get("text.0607") + exception.Message); }
        }

        void LoadProgress()
        {
            progress = ExpeditionProgress.New();
            if (storageBlocked) return;
            if (loadedAccount != null) { progress = ExpeditionProgress.Restore(loadedAccount.Progress); return; }
            if (!PlayerPrefs.HasKey(ProgressSaveKey)) return;
            try
            {
                string json = PlayerPrefs.GetString(ProgressSaveKey, "");
                if (string.IsNullOrEmpty(json)) throw new InvalidOperationException(CatalogText.Get("text.0608"));
                progress = ExpeditionProgress.Restore(JsonUtility.FromJson<ExpeditionProgressSave>(json));
            }
            catch (Exception)
            {
                badProgress = true;
                storageBlocked = true;
                error = CatalogText.Get("text.0609");
                progress = ExpeditionProgress.New();
            }
        }

        void ObserveProgress()
        {
            if (run == null || progress == null || badProgress) return;
            try
            {
                if (progress.Observe(run)) progressDirty = true;
            }
            catch (Exception exception) { Notice(CatalogText.Get("text.0610") + exception.Message); }
        }

        void WriteProgress()
        {
            if (badProgress || progress == null || !progressDirty) return;
            PersistAccount();
        }

        void NewRun()
        {
            if (!startupReady) return;
            if (storageBlocked) { Notice(CatalogText.Get("text.0611")); return; }
            ExpeditionRun previousRun = run;
            bool showOpening = journal != null && !journal.IsUnlocked("forest");
            ExpeditionHistorySave previousHistory = recordHistory.Capture();
            try
            {
                ObserveProgress();
                uint seed = unchecked((uint)DateTime.UtcNow.Ticks);
                ExpeditionRun nextRun = ExpeditionRun.New(database, seed,
                    ExpeditionDifficulty.Normal, startFormationId: selectedFormationId);
                recordHistory.Observe(run, true);
                if (run != null && run.Phase != ExpeditionPhase.Completed && run.Phase != ExpeditionPhase.Defeated)
                    playtest.Record("run_replaced", run, "Player chose a new expedition; not a battle defeat.");
                run = nextRun;
                ObserveJournal();
                ObserveProgress();
                PersistAccount();
                playtest.Begin(run, seed);
                badSave = false;
                legacySave = false;
                error = null;
                home = false;
                modal = Modal.None;
                selectedId = null;
                benchPage = 0;
                tutorialReplayWave = 0;
                lessonReplay = ExpeditionLesson.None;
                tutorialRangeAcknowledged = false;
                Save();
                if (showOpening) OpenJournal("intro");
            }
            catch (Exception exception)
            {
                run = previousRun;
                recordHistory = ExpeditionHistory.Restore(database, previousHistory);
                Notice(CatalogText.Get("text.0612") + exception.Message);
            }
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
            if (presentationLanguageRevision != CatalogText.Revision)
            {
                presentationLanguageRevision = CatalogText.Revision;
                infoKey = researchDetailId = null;
                infoScroll = researchDetailScroll = journalBodyScroll = Vector2.zero;
                toast = null;
            }
            Rect safe = Screen.safeArea;
            float scale = Mathf.Max(.01f, Mathf.Min(safe.width / Width, safe.height / Height));
            viewport = new Rect(safe.x + (safe.width - Width * scale) * .5f,
                Screen.height - safe.yMax + (safe.height - Height * scale) * .5f,
                Width * scale, Height * scale);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture);
            EnsureStyles();
            GUI.matrix = Matrix4x4.TRS(new Vector3(viewport.x, viewport.y), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            if (!startupReady)
            {
                DrawStartup();
                GUI.matrix = Matrix4x4.identity;
                return;
            }
            if (ending != null && ending.Active)
            { ending.Draw(); GUI.matrix = Matrix4x4.identity; return; }
            if (!GrowthRevealActive) { FilterTutorialGuiEvent(); FilterLessonGuiEvent(); }
            Fill(new Rect(0, 0, Width, Height), Background);
            GUI.enabled = modal == Modal.None && !GrowthRevealActive;
            if (home || run == null) DrawHome();
            else if (run.Phase == ExpeditionPhase.Preparation) DrawPreparation();
            else if (run.Phase == ExpeditionPhase.Battle) DrawBattleResume();
            else DrawResult();
            GUI.enabled = !GrowthRevealActive;
            if (modal != Modal.None && !GrowthRevealActive) DrawModal();
            GUI.enabled = true;
            if (toast != null && Time.unscaledTime < toastUntil)
            {
                ExpeditionUiTheme.PanelBox(new Rect(340, 660, 600, 48), true);
                LocalizedGui.Label(new Rect(350, 667, 580, 34), toast, centered);
            }
            if (GrowthRevealActive) DrawGrowthReveal();
            else { DrawTutorialSpotlight(); DrawLessonSpotlight(); }
            GUI.matrix = Matrix4x4.identity;
        }

        void DrawHome()
        {
            ExpeditionUiTheme.DrawBackdrop(new Rect(0, 90, Width, 630), battlefield);
            ExpeditionUiTheme.DrawHeaderPlate(new Rect(0, 0, Width, 90));
            ExpeditionUiTheme.DrawEmber(new Vector2(53, 46), 30);
            LocalizedGui.Label(new Rect(84, 6, 875, 60), CatalogText.Get("text.0613"), title);
            LocalizedGui.Label(new Rect(87, 62, 500, 21), "I G N I S :  A S H E N  O A T H", small);
            if (LocalizedGui.Button(new Rect(851, 20, 128, 49), CatalogText.Get("text.0363"), button)) modal = Modal.Settings;
            if (LocalizedGui.Button(new Rect(995, 20, 260, 49),
                CatalogText.Get("text.0614") + (journal == null ? 1 : journal.UnlockedCount) + "/" +
                ExpeditionStory.Entries.Count, button))
                OpenJournal(null);
            bool hasNotice = error != null || legacySave || run != null && run.UsesPreviousBalance;
            Rect panel = new Rect(155, hasNotice ? 144 : 176, 970, hasNotice ? 432 : 368);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            ExpeditionUiTheme.Window(panel);
            bool replay = journal != null && journal.IsUnlocked("palace-clear");
            LocalizedGui.Label(new Rect(content.x, content.y, content.width, 24), "I G N I S", hint);
            LocalizedGui.Label(new Rect(content.x, content.y + 32, content.width - 448, 44),
                replay ? CatalogText.Get("text.0615") : CatalogText.Get("text.0616"), heading);
            if (LocalizedGui.Button(new Rect(content.xMax - 426, content.y + 16, 205, 48),
                CatalogText.Get("text.0497"), button))
            { recordsPage = 0; selectedRecord = null; modal = Modal.Records; }
            if (LocalizedGui.Button(new Rect(content.xMax - 205, content.y + 16, 205, 48), CatalogText.Get("text.0617"), button))
                modal = Modal.Progress;
            ExpeditionUiTheme.Rule(new Rect(content.x, content.y + 88, content.width, 2));
            LocalizedGui.Label(new Rect(content.x, content.y + 104, content.width, 64),
                replay ? ExpeditionStory.ReplayBrief : ExpeditionStory.HomeBrief, dialogBody);
            if (run != null)
                LocalizedGui.Label(new Rect(content.x, content.y + 176, content.width, 33),
                    CatalogText.Get("text.0618") + DifficultyLabel(run.Difficulty) + CatalogText.Get("text.0619") + run.Wave +
                    " / " + run.ContentWaves, hint);
            if (hasNotice)
                LocalizedGui.Label(new Rect(content.x, footer.y - 88, content.width, 56),
                    error ?? CatalogText.Get("text.0620"), hint);
            ExpeditionUiTheme.FooterRule(panel);
            GUI.enabled = modal == Modal.None && !storageBlocked;
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2), CatalogText.Get("text.0621"), primary))
            { selectedFormationId = "standard"; modal = Modal.NewRun; }
            GUI.enabled = modal == Modal.None && run != null;
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), CatalogText.Get("text.0622"), button))
            { home = false; modal = Modal.None; playtest.Record("continue_run", run); }
            GUI.enabled = modal == Modal.None;
            if (LocalizedGui.Button(new Rect(1020, 650, 225, 48), CatalogText.Get("text.0623"), button)) OpenLab();
            DrawPlaytestButton();
            if (badProgress)
            {
                ExpeditionUiTheme.PanelBox(new Rect(155, 590, 970, 48));
                LocalizedGui.Label(new Rect(187, 598, 900, 36),
                    CatalogText.Get("text.0624"), small);
            }
        }

        static string DifficultyLabel(ExpeditionDifficulty difficulty)
        {
            return difficulty == ExpeditionDifficulty.Hard ? CatalogText.Get("text.0625") : CatalogText.Get("text.0626");
        }

        void DrawHeader(string phase)
        {
            if (run.UsesPreviousBalance) phase = run.UsesLegacyRules ? CatalogText.Get("text.0627") :
                CatalogBalance.Version(run.RulesVersion).Replace("normal-0", "v0.") + CatalogText.Get("text.0628");
            ExpeditionUiTheme.DrawHeaderPlate(new Rect(0, 0, Width, 80));
            if (LocalizedGui.Button(new Rect(18, 15, 90, 50), CatalogText.Get("text.0629"), button)) { home = true; modal = Modal.None; Save("return_home"); }
            ExpeditionUiTheme.DrawEmber(new Vector2(146, 40), 23);
            LocalizedGui.Label(new Rect(168, 10, 312, 58), CatalogText.Get("text.0630"), title);
            LocalizedGui.Label(new Rect(505, 5, 365, 34), DifficultyLabel(run.Difficulty) + CatalogText.Get("text.0522") +
                run.Wave + " / " + run.ContentWaves, body);
            LocalizedGui.Label(new Rect(505, 41, 365, 28), CatalogText.Get("text.0631") + run.Lantern + " / 3", small);
            LocalizedGui.Label(new Rect(880, 6, 170, 38), run.Gold + " G", heading);
            if (!run.HasEnded)
                LocalizedGui.Label(new Rect(880, 44, 170, 28), run.UsesLegacyRules ? CatalogText.Get("text.0632") : CatalogText.Get("text.0633") + run.Score.ToString("N0") + CatalogText.Get("text.0508"), small);
            if (run.Phase == ExpeditionPhase.Preparation)
            {
                if (LocalizedGui.Button(PreparationLayout.Research, CatalogText.Get("text.0634") + run.TacticIds.Count + "/" + CatalogResearch.MaxNodes, button))
                { selectedTacticNodeId = "bulwark"; modal = Modal.Tactics; }
            }
            else { ExpeditionUiTheme.PanelBox(new Rect(1070,17,188,43)); LocalizedGui.Label(new Rect(1080,21,168,33),phase,centered); }
        }

        void DrawPreparation() { DrawPreparationBoard(); }

        string PreparationAdvice()
        {
            switch (run.Wave)
            {
                case 1: return CatalogText.Get("text.0635");
                case 2: return CatalogText.Get("text.0636");
                case 3: return CatalogText.Get("text.0637");
                case 4: return CatalogText.Get("text.0638");
                case 5: return CatalogText.Get("text.0639");
                case 6: return CatalogText.Get("text.0640");
                case 7: return CatalogText.Get("text.0641");
                case 8: return CatalogText.Get("text.0642");
                case 9: return CatalogText.Get("text.0643");
                case 10: return CatalogText.Get("text.0644");
                case 11: return CatalogText.Get("text.0645");
                case 12: return CatalogText.Get("text.0646");
                case 13: return CatalogText.Get("text.0647");
                case 14: return CatalogText.Get("text.0648");
                case 15: return CatalogText.Get("text.0649");
                case 16: return CatalogText.Get("text.0650");
                case 17: return CatalogText.Get("text.0651");
                case 18: return CatalogText.Get("text.0652");
                case 19: return CatalogText.Get("text.0653");
                case 20: return run.RulesVersion >= 3 ?
                    CatalogText.Get("text.0654") :
                    CatalogText.Get("text.0655");
                case 21: return CatalogText.Get("text.0656");
                case 22: return CatalogText.Get("text.0657");
                case 23: return CatalogText.Get("text.0658");
                case 24: return CatalogText.Get("text.0659");
                case 25: return CatalogText.Get("text.0660");
                case 26: return CatalogText.Get("text.0661");
                case 27: return CatalogText.Get("text.0662");
                case 28: return CatalogText.Get("text.0663");
                case 29: return CatalogText.Get("text.0664");
                case 30: return CatalogText.Get("text.0665");
                default: return CatalogText.Get("text.0666");
            }
        }

        static Rect BenchRect(int index) { return PreparationLayout.Bench(index); }

        void DrawUnit(CatalogMember member, Rect sprite, Rect name)
        {
            if (Time.unscaledTime < growthPulseUntil && member.Id == growthPulseId)
                Fill(new Rect(sprite.x - 5, sprite.y - 5, sprite.width + 10, sprite.height + 10),
                    new Color(.95f, .74f, .30f, .36f));
            Fill(new Rect(sprite.center.x - 28, sprite.yMax - 10, 56, 7), new Color(.02f, .025f, .035f, .75f));
            if (art != null) art.Draw(member.FormId, sprite, (int)(Time.unscaledTime * 5f) % 4);
            string label = database.Get(member.FormId).Name;
            if (name.width > 0) LocalizedGui.Label(name, label, centered);
            if (member.MaterialLocked)
            {
                Fill(new Rect(sprite.x, sprite.y + 2, 54, 24), new Color(.13f, .14f, .17f, .94f));
                LocalizedGui.Label(new Rect(sprite.x, sprite.y, 54, 25), CatalogText.Get("text.0397"), small);
            }
        }

        void DrawBattleResume()
        {
            DrawHeader(CatalogText.Get("text.0667"));
            Rect panel = new Rect(185, 170, 910, 380);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            LocalizedGui.Label(new Rect(content.x, 218, content.width, 62), run.EncounterName, heading);
            LocalizedGui.Label(new Rect(content.x, 285, content.width, 92),
                CatalogText.Get("text.0668"), body);
            ExpeditionUiTheme.FooterRule(panel);
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterRect(panel), CatalogText.Get("text.0669"), primary)) StartBattle(true);
        }

        void DrawResult()
        {
            if (run.Phase == ExpeditionPhase.Completed || run.Phase == ExpeditionPhase.Defeated ||
                run.Phase == ExpeditionPhase.Result && (run.Lantern == 0 || run.LastVictory && run.Wave == ExpeditionRun.TotalWaves))
            { DrawExpeditionSummary(); return; }
            bool victory = run.Phase == ExpeditionPhase.Completed ||
                (run.Phase == ExpeditionPhase.Result && run.LastVictory);
            StoryEntry clearStory = run.LastVictory ? ExpeditionStory.BossClear(run.Wave) : null;
            DrawHeader(run.Phase == ExpeditionPhase.Result ? CatalogText.Get("text.0670") : CatalogText.Get("text.0671"));
            bool failureEvidence = !victory && HasFailureEvidence;
            Rect panel = failureEvidence ? new Rect(175, 112, 930, 520) : new Rect(175, 140, 930, 430);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            LocalizedGui.Label(failureEvidence ? new Rect(content.x, 140, content.width, 60) : clearStory == null ? new Rect(content.x, 180, content.width, 72) :
                new Rect(content.x, 164, content.width, 60),
                run.Phase == ExpeditionPhase.Completed ? CatalogText.Get("text.0672") :
                run.Phase == ExpeditionPhase.Defeated ? CatalogText.Get("text.0673") : victory ? CatalogText.Get("text.0004") : CatalogText.Get("text.0496"), title);
            string outcome = run.Phase == ExpeditionPhase.Completed ? CatalogText.Get("text.0674") :
                run.Phase == ExpeditionPhase.Defeated ? CatalogText.Get("text.0675") :
                run.Phase == ExpeditionPhase.Result && run.Lantern <= 0 ?
                CatalogText.Get("text.0676") : victory ?
                (run.Wave == ExpeditionRun.TotalWaves ? ExpeditionStory.CompletionBrief :
                CatalogText.Get("text.0677")) :
                CatalogText.Get("text.0678");
            LocalizedGui.Label(failureEvidence ? new Rect(content.x, 214, content.width, 82) : clearStory == null ? new Rect(content.x, 275, content.width, 92) :
                new Rect(content.x, 232, content.width, 68),
                CatalogText.Get("text.0679") + run.Victories + " / " + ExpeditionRun.TotalWaves + CatalogText.Get("text.0680") + run.Lantern +
                "\n" + outcome, body);
            if (clearStory != null)
                LocalizedGui.Label(new Rect(content.x, 306, content.width, 60), clearStory.Excerpt, body);
            if (failureEvidence) DrawFailureEvidence(new Rect(content.x, 302, content.width, 124));
            string reward = run.LastVictory && run.LastReward > 0 ?
                "+" + run.LastReward + " G   ·   " + (run.Gold - run.LastReward) + " → " + run.Gold + " G" :
                run.LastVictory && run.Wave == ExpeditionRun.TotalWaves ?
                ExpeditionRun.TotalWaves + CatalogText.Get("text.0681") :
                CatalogText.Get("text.0682") + run.Gold + " G";
            float rewardY = failureEvidence ? 444 : clearStory == null ? 370 : 376;
            LocalizedGui.Label(new Rect(content.x, rewardY,
                HasBattleReport ? content.width - 210 : content.width, 43), reward, heading);
            if (HasBattleReport && LocalizedGui.Button(new Rect(content.xMax - 195, rewardY, 195, 48), CatalogText.Get("text.0683"), button))
                modal = Modal.BattleReport;
            ExpeditionUiTheme.FooterRule(panel);
            if (run.Phase == ExpeditionPhase.Result)
            {
                Rect footer = ExpeditionUiTheme.FooterRect(panel);
                if (clearStory != null && LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2),
                    CatalogText.Get("text.0684"), button)) OpenJournal(clearStory.Id);
                string next = run.Lantern <= 0 || run.LastVictory && run.Wave == ExpeditionRun.TotalWaves ?
                    CatalogText.Get("text.0671") : run.LastVictory ? CatalogText.Get("text.0685") : CatalogText.Get("text.0686");
                if (LocalizedGui.Button(clearStory == null ? footer : ExpeditionUiTheme.FooterButton(footer, 1, 2), next, primary))
                    ContinueResultPreparation();
            }
            else if (LocalizedGui.Button(ExpeditionUiTheme.FooterRect(panel), CatalogText.Get("text.0365"), primary)) home = true;
        }

        void DrawModal()
        {
            if (modal == Modal.Settings) { if (GameSettingsPanel.Draw(font)) modal = Modal.None; return; }
            Fill(new Rect(0, 0, Width, Height), new Color(.005f, .008f, .015f, .84f));
            if (modal == Modal.Records) DrawRecords();
            else if (modal == Modal.Detail) DrawDetail();
            else if (modal == Modal.Journal) DrawJournal();
            else if (modal == Modal.Progress) DrawProgress();
            else if (modal == Modal.Tactics) DrawTactics();
            else if (modal == Modal.Effects) DrawMemberEffects();
            else if (modal == Modal.BattleReport) DrawBattleReport();
            else if (modal == Modal.Playtest) DrawPlaytest();
            else if (modal == Modal.GuideMenu) DrawGuideMenu();
            else if (modal == Modal.ChooseDonor || modal == Modal.ChoosePartner) DrawChooseMember();
            else if (modal == Modal.CombinationOptions) DrawCombinationOptions();
            else if (modal == Modal.Encounter) DrawEncounterBrief();
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
            journalBodyScroll = Vector2.zero;
            journalDragPane = 0;
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
            LocalizedGui.Label(new Rect(content.x, 98, 700, 51),
                CatalogText.Get("text.0687") + (journal == null ? 1 : journal.UnlockedCount) + "/" +
                ExpeditionStory.Entries.Count, heading);
            LocalizedGui.Label(new Rect(content.x + 2, 137, 680, 21), "F I E L D   J O U R N A L", small);
            if (LocalizedGui.Button(new Rect(content.xMax - 77, 98, 77, 50), CatalogText.Get("text.0400"), button))
            { modal = Modal.None; return; }
            ExpeditionUiTheme.PanelBox(new Rect(content.x, 163, 320, 459));
            Rect journalViewport = new Rect(content.x + 10, 170, 300, 443);
            DragJournalPane(1, journalViewport, ExpeditionStory.Entries.Count * 52 + 4, ref journalScroll);
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
                if (LocalizedGui.Button(row, unlocked ? entry.Title : CatalogText.Get("text.0688") + (i + 1).ToString("00"), button))
                { selectedStoryId = entry.Id; journalBodyScroll = Vector2.zero; }
            }
            GUI.EndScrollView();
            ExpeditionUiTheme.ParchmentBox(new Rect(496, 163, 624, 459));
            StoryEntry selectedStory = ExpeditionStory.Get(selectedStoryId) ?? ExpeditionStory.Get("intro");
            bool selectedUnlocked = journal != null && journal.IsUnlocked(selectedStory.Id);
            LocalizedGui.Label(new Rect(528, 190, 560, 53), selectedUnlocked ? selectedStory.Title : CatalogText.Get("text.0689"), journalTitle);
            LocalizedGui.Label(new Rect(529, 243, 559, 34), selectedUnlocked ? CatalogText.Get("text.0690") : CatalogText.Get("text.0691"), journalStatus);
            if (selectedUnlocked && selectedStory.Id == "palace-clear" &&
                LocalizedGui.Button(new Rect(856, 242, 232, 38), CatalogText.Get("text.0692"), button)) { OpenEnding(true); return; }
            ExpeditionUiTheme.Rule(new Rect(528, 285, 560, 2));
            string text = selectedUnlocked ? selectedStory.Body : StoryUnlockHint(selectedStory.Id);
            Rect readingArea = new Rect(529, 306, 559, 284);
            float textWidth = readingArea.width - 24;
            float textHeight = Mathf.Max(readingArea.height - 4, journalBody.CalcHeight(new GUIContent(text), textWidth) + 8);
            DragJournalPane(2, readingArea, textHeight, ref journalBodyScroll);
            journalBodyScroll = GUI.BeginScrollView(readingArea, journalBodyScroll,
                new Rect(0, 0, textWidth, textHeight), false, textHeight > readingArea.height);
            LocalizedGui.Label(new Rect(0, 0, textWidth, textHeight), text, journalBody);
            GUI.EndScrollView();
        }

        void DragJournalPane(int pane, Rect area, float contentHeight, ref Vector2 scroll)
        {
            Event current = Event.current;
            // IMGUI scrollbars work on Android, but dragging the page itself needs a touch/mouse gesture.
            Rect page = new Rect(area.x, area.y, area.width - 24, area.height);
            if (current.type == EventType.MouseDown && current.button == 0 && page.Contains(current.mousePosition))
            {
                journalDragPane = pane;
                journalDragStartY = current.mousePosition.y;
                journalDragStartScroll = scroll.y;
                journalDragging = false;
            }
            if (journalDragPane != pane) return;
            if (current.type == EventType.MouseDrag &&
                (journalDragging || Mathf.Abs(current.mousePosition.y - journalDragStartY) > 8))
            {
                journalDragging = true;
                scroll.y = Mathf.Clamp(journalDragStartScroll + journalDragStartY - current.mousePosition.y,
                    0, Mathf.Max(0, contentHeight - area.height));
                GUIUtility.hotControl = 0; // A swipe over a chapter button must not select it on release.
                current.Use();
            }
            else if (current.type == EventType.MouseUp)
            {
                journalDragPane = 0;
                if (journalDragging) { GUIUtility.hotControl = 0; current.Use(); }
            }
        }

        void DrawProgress()
        {
            Rect panel = new Rect(250, 170, 780, 380);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            ExpeditionUiTheme.Window(panel);
            LocalizedGui.Label(new Rect(content.x, 202, content.width, 48), CatalogText.Get("text.0617"), heading);
            LocalizedGui.Label(new Rect(content.x, 248, content.width, 32),
                CatalogText.Get("text.0693") + ExpeditionRun.TotalWaves + CatalogText.Get("text.0694"), hint);
            ExpeditionUiTheme.Rule(new Rect(content.x, 285, content.width, 2));
            LocalizedGui.Label(new Rect(content.x, 300, content.width, 58), ProgressLine(), body);
            ExpeditionUiTheme.Rule(new Rect(content.x, 365, content.width, 2));
            LocalizedGui.Label(new Rect(content.x, 378, content.width, 28),
                CatalogText.Get("text.0695") +
                (badProgress ? CatalogText.Get("text.0696") : progress != null && progress.HardUnlocked ? CatalogText.Get("text.0697") : CatalogText.Get("text.0698")) +
                CatalogText.Get("text.0699"), small);
            if (badProgress)
                LocalizedGui.Label(new Rect(content.x, 407, content.width, 24),
                    CatalogText.Get("text.0700"), small);
            ExpeditionUiTheme.FooterRule(panel);
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterRect(panel), CatalogText.Get("text.0400"), button)) modal = Modal.None;
        }

        string ProgressLine()
        {
            if (badProgress) return CatalogText.Get("text.0701");
            int best = progress == null ? 0 : progress.BestWave;
            int completed = progress == null ? 0 : progress.CompletedWaves;
            return CatalogText.Get("text.0702") + best + CatalogText.Get("text.0703") +
                CatalogText.Get("text.0704") + completed + " / " + ExpeditionRun.ReleaseWaves + CatalogText.Get("text.0703");
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
                case "forest": return CatalogText.Get("text.0705");
                case "forest-clear": return CatalogText.Get("text.0706");
                case "castle": return CatalogText.Get("text.0707");
                case "castle-clear": return CatalogText.Get("text.0708");
                case "mine": return CatalogText.Get("text.0709");
                case "mine-clear": return CatalogText.Get("text.0710");
                case "frost": return CatalogText.Get("text.0711");
                case "frost-clear": return CatalogText.Get("text.0712");
                case "garden": return CatalogText.Get("text.0713");
                case "garden-clear": return CatalogText.Get("text.0714");
                case "palace": return CatalogText.Get("text.0715");
                case "palace-clear": return CatalogText.Get("text.0716");
                default: return CatalogText.Get("text.0717");
            }
        }

        void DrawChooseMember()
        {
            if (modal == Modal.ChoosePartner) { DrawPartnerChoices(); return; }
            bool transfer = modal == Modal.ChooseDonor;
            Rect panel = new Rect(215, 75, 850, 585);
            Rect content = ExpeditionUiTheme.ContentRect(panel);
            Rect footer = ExpeditionUiTheme.FooterRect(panel);
            ExpeditionUiTheme.Window(panel);
            LocalizedGui.Label(new Rect(content.x, 107, content.width - 80, 52),
                transfer ? CatalogText.Get("text.0718") : CatalogText.Get("text.0719"), heading);
            if (LocalizedGui.Button(new Rect(content.xMax - 59, 107, 59, 50), "×", button))
            { modal = Modal.Detail; return; }
            if (transfer)
            {
                if (LocalizedGui.Button(new Rect(content.x, 166, content.width, 48),
                    (inheritTrait ? "✓  " : "○  ") + CatalogText.Get("text.0720"), button))
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
                string label = (IsDeployed(other.Id) ? CatalogText.Get("text.0721") : CatalogText.Get("text.0722")) + database.Get(other.FormId).Name;
                if (result != null) label += "  →  " + database.Get(result).Name;
                else label += "  ·  " + TraitName(database.Get(other.FormId).InnateTrait);
                if (LocalizedGui.Button(slot, label, button))
                {
                    pendingAction = transfer ? GrowthAction.Transfer : GrowthAction.Combine;
                    pendingOtherId = other.Id;
                    pendingFormId = result;
                    combinationFromList = false;
                    modal = Modal.Confirm;
                }
                shown++;
            }
            if (shown == 0) LocalizedGui.Label(new Rect(content.x, 248, content.width, 95),
                transfer ? CatalogText.Get("text.0723") : CatalogText.Get("text.0724"), body);
            if (pages > 1)
            {
                ExpeditionUiTheme.FooterRule(panel);
                if (LocalizedGui.Button(new Rect(footer.x, footer.y, 112, 64), CatalogText.Get("text.0510"), button)) choosePage--;
                LocalizedGui.Label(new Rect(footer.center.x - 60, footer.y + 17, 120, 30),
                    (choosePage + 1) + " / " + pages, centered);
                if (LocalizedGui.Button(new Rect(footer.xMax - 112, footer.y, 112, 64), CatalogText.Get("text.0330"), button)) choosePage++;
            }
        }

        void OpenCombinationOptions()
        {
            if (run == null || run.Phase != ExpeditionPhase.Preparation) return;
            List<CatalogCombinationOption> options = run.GetCombinationOptions();
            if (options.Count == 0) { Notice(CatalogText.Get("text.0725")); return; }
            combinationPage = 0;
            modal = Modal.CombinationOptions;
        }

        void ChooseCombinationOption(int index)
        {
            growthPreviewKey = null;
            List<CatalogCombinationOption> options = run.GetCombinationOptions();
            if (index < 0 || index >= options.Count)
            { Notice(CatalogText.Get("text.0726")); return; }
            CatalogCombinationOption option = options[index];
            if (!run.CanCombine(option.RecipientId, option.DonorId, option.ResultId))
            { Notice(CatalogText.Get("text.0727")); return; }
            selectedId = option.RecipientId;
            pendingOtherId = option.DonorId;
            pendingFormId = option.ResultId;
            pendingAction = GrowthAction.Combine;
            combinationFromList = true;
            modal = Modal.Confirm;
        }

        void PrepareEvolution(CatalogMember member, bool fromDrag = false)
        {
            growthPreviewKey = null;
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
            if (modal == Modal.Confirm && pendingAction == GrowthAction.Evolve)
            { DrawEvolutionConfirmation(); return; }
            if (modal == Modal.Confirm && pendingAction == GrowthAction.Combine)
            { DrawCombinationConfirmation(); return; }
            if (modal == Modal.NewRun)
            {
                Rect panel = new Rect(205, 70, 870, 580);
                Rect content = ExpeditionUiTheme.ContentRect(panel);
                ExpeditionUiTheme.Window(panel);
                LocalizedGui.Label(new Rect(content.x, panel.y + 32, content.width, 48),
                    CatalogText.Get("text.0621"), heading);
                string warning = savePresent ?
                    CatalogText.Get("text.0728") :
                    legacySave ? CatalogText.Get("text.0729") :
                    CatalogText.Get("text.0730");
                LocalizedGui.Label(new Rect(content.x, 152, content.width, 50),
                    CatalogText.Get("text.0693") + ExpeditionRun.TotalWaves + CatalogText.Get("text.0731") + warning, small);
                Rect normal = new Rect(content.x, 216, content.width, 62);
                ExpeditionUiTheme.PanelBox(normal, true);
                LocalizedGui.Label(new Rect(normal.x + 20, normal.y + 6, normal.width - 40, 28),
                    CatalogText.Get("text.0732"), body);
                LocalizedGui.Label(new Rect(normal.x + 20, normal.y + 34, normal.width - 40, 23),
                    CatalogText.Get("text.0733"), small);
                Rect locked = new Rect(content.x, 288, content.width, 62);
                bool wasEnabled = GUI.enabled;
                GUI.enabled = false;
                LocalizedGui.Button(locked, "", button);
                GUI.enabled = wasEnabled;
                LocalizedGui.Label(new Rect(locked.x + 20, locked.y + 6, locked.width - 40, 28),
                    CatalogText.Get("text.0734"), body);
                LocalizedGui.Label(new Rect(locked.x + 20, locked.y + 34, locked.width - 40, 23),
                    progress != null && progress.HardUnlocked && !badProgress ?
                    CatalogText.Get("text.0735") :
                    CatalogText.Get("text.0736"), small);
                LocalizedGui.Label(new Rect(content.x, 364, content.width, 31), CatalogText.Get("text.0737"), hint);
                IReadOnlyList<string> research = badProgress || progress == null ?
                    Array.Empty<string>() : progress.ResearchIds;
                for (int i = 0; i < FormationIds.Length; i++)
                {
                    float cardWidth = (content.width - 3 * 12f) / 4f;
                    Rect card = new Rect(content.x + i * (cardWidth + 12f), 402, cardWidth, 116);
                    bool unlocked = ExpeditionRun.IsStartingFormation(FormationIds[i]);
                    if (selectedFormationId == FormationIds[i])
                        Fill(new Rect(card.x - 3, card.y - 3, card.width + 6, card.height + 6),
                            new Color(Gold.r, Gold.g, Gold.b, .8f));
                    GUI.enabled = wasEnabled && unlocked;
                    if (LocalizedGui.Button(card, GUIContent.none, button)) selectedFormationId = FormationIds[i];
                    GUI.enabled = wasEnabled;
                    LocalizedGui.Label(new Rect(card.x + 13, card.y + 9, card.width - 26, 31),
                        FormationNames[i], centered);
                    LocalizedGui.Label(new Rect(card.x + 12, card.y + 39, card.width - 24, 48),
                        FormationUnits[i], small);
                    LocalizedGui.Label(new Rect(card.x + 12, card.y + 90, card.width - 24, 22),
                        unlocked ? selectedFormationId == FormationIds[i] ? CatalogText.Get("text.0738") : CatalogText.Get("text.0739") :
                        badProgress ? CatalogText.Get("text.0740") : CatalogText.Get("text.0741"), small);
                }
                ExpeditionUiTheme.FooterRule(panel);
                ConfirmationButtons(ExpeditionUiTheme.FooterRect(panel),
                    CatalogText.Get("text.0742"), NewRun, () => modal = Modal.None);
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
            string headingText = pendingAction == GrowthAction.Transfer ? CatalogText.Get("text.0743") :
                pendingAction == GrowthAction.Combine ? CatalogText.Get("text.0744") : CatalogText.Get("text.0745");
            LocalizedGui.Label(new Rect(growthContent.x, growthPanel.y + 32, growthContent.width, 48),
                headingText, heading);
            string selectedName = database.Get(selected.FormId).Name;
            string resultName = pendingAction == GrowthAction.Transfer ? selectedName :
                pendingFormId == null ? "" : database.Get(pendingFormId).Name;
            string message = pendingAction == GrowthAction.Evolve ? selectedName + " → " + resultName :
                other == null ? CatalogText.Get("text.0746") :
                selectedName + " + " + database.Get(other.FormId).Name + " → " + resultName +
                "\n" + database.Get(other.FormId).Name + CatalogText.Get("text.0747");
            if (pendingAction == GrowthAction.Transfer)
                message += CatalogText.Get("text.0748") + (inheritTrait ? CatalogText.Get("text.0749") : "");
            LocalizedGui.Label(new Rect(growthContent.x, growthPanel.y + 100, growthContent.width,
                chooseBranch ? 86 : 110), message, dialogBody);
            if (chooseBranch)
            {
                string family = CatalogRoster.FamilyCode(selected.FormId);
                int familyIndex = Array.IndexOf(Families, family);
                if (familyIndex >= 0)
                {
                    Rect branchRow = new Rect(growthContent.x, growthPanel.y + 200,
                        growthContent.width, 56);
                    if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(branchRow, 0, 2),
                        database.Get(Branches[familyIndex][0]).Name, button))
                        pendingFormId = Branches[familyIndex][0];
                    if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(branchRow, 1, 2),
                        database.Get(Branches[familyIndex][1]).Name, button))
                        pendingFormId = Branches[familyIndex][1];
                    LocalizedGui.Label(new Rect(growthContent.x, growthPanel.y + 270,
                        growthContent.width, 30), CatalogText.Get("text.0750") + database.Get(pendingFormId).Name, small);
                }
            }
            ExpeditionUiTheme.FooterRule(growthPanel);
            ConfirmationButtons(ExpeditionUiTheme.FooterRect(growthPanel), CatalogText.Get("text.0751"), ExecuteGrowth,
                () => modal = pendingAction == GrowthAction.Evolve && evolutionFromDrag ? Modal.None : Modal.Detail);
        }

        sealed class HybridGuidance
        {
            public readonly string Skill;
            public readonly string Tactic;

            public HybridGuidance(string skill, string role, string placement)
            {
                Skill = skill;
                Tactic = CatalogText.Get("text.0752") + role + CatalogText.Get("text.0753") + placement;
            }
        }

        static string HybridSkillText(CatalogForm form, HybridGuidance guidance)
        { return string.IsNullOrEmpty(form.Description) ? guidance.Skill : form.Description; }

        static readonly HybridGuidance H01Guide = new HybridGuidance(CatalogText.Get("text.0754"), CatalogText.Get("text.0755"), CatalogText.Get("text.0756"));
        static readonly HybridGuidance H02Guide = new HybridGuidance(CatalogText.Get("text.0757"), CatalogText.Get("text.0758"), CatalogText.Get("text.0759"));
        static readonly HybridGuidance H03Guide = new HybridGuidance(CatalogText.Get("text.0760"), CatalogText.Get("text.0761"), CatalogText.Get("text.0762"));
        static readonly HybridGuidance H04Guide = new HybridGuidance(CatalogText.Get("text.0763"), CatalogText.Get("text.0764"), CatalogText.Get("text.0765"));
        static readonly HybridGuidance H05Guide = new HybridGuidance(CatalogText.Get("text.0766"), CatalogText.Get("text.0767"), CatalogText.Get("text.0768"));
        static readonly HybridGuidance H06Guide = new HybridGuidance(CatalogText.Get("text.0769"), CatalogText.Get("text.0770"), CatalogText.Get("text.0771"));

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
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 0, 2), CatalogText.Get("text.0772"), button)) no();
            if (LocalizedGui.Button(ExpeditionUiTheme.FooterButton(footer, 1, 2), confirm, primary)) yes();
        }

        void ExecuteGrowth()
        {
            if (GrowthRevealActive) return;
            string beforeForm = run.Roster.Find(selectedId)?.FormId;
            string donorForm = run.Roster.Find(pendingOtherId)?.FormId;
            bool applied = pendingAction == GrowthAction.Transfer ?
                run.Transfer(pendingOtherId, selectedId, inheritTrait) :
                pendingAction == GrowthAction.Evolve ? run.Evolve(selectedId, pendingFormId) :
                pendingAction == GrowthAction.Combine && run.Combine(selectedId, pendingOtherId, pendingFormId);
            if (!applied) { Notice(CatalogText.Get("text.0773")); modal = Modal.Detail; return; }
            Save(pendingAction.ToString().ToLowerInvariant(), selectedId + ":" + pendingOtherId + ":" + pendingFormId);
            Notice(CatalogText.Get("text.0774"));
            if (pendingAction == GrowthAction.Transfer)
            {
                CatalogMember grown = run.Roster.Find(selectedId);
                if (ReadyToEvolve(grown)) { PrepareEvolution(grown); return; }
            }
            modal = pendingAction == GrowthAction.Combine && combinationFromList ||
                pendingAction == GrowthAction.Evolve && evolutionFromDrag ? Modal.None : Modal.Detail;
            evolutionFromDrag = false;
            combinationFromList = false;
            if (pendingAction == GrowthAction.Evolve || pendingAction == GrowthAction.Combine)
                BeginGrowthReveal(beforeForm, donorForm, pendingFormId, pendingAction == GrowthAction.Combine);
        }

        void Summon()
        {
            string id = run.Summon();
            if (id == null) { Notice(CatalogText.Get("text.0775")); return; }
            Save("summon", id);
            benchPage = Mathf.Max(0, (run.Roster.Members.Count - run.DeployedIds.Count - 1) / 5);
            CatalogMember member = run.Roster.Find(id);
            Notice(CatalogText.Get("text.0776") + database.Get(member.FormId).Name + CatalogText.Get("text.0777"));
        }

        void StartBattle(bool resume)
        {
            try
            {
                CatalogBattle battle = resume ? run.ResumeBattle() : run.BeginBattle();
                if (battle == null) { Notice(CatalogText.Get("text.0778")); return; }
                FinishTutorialWave(run.Wave);
                BeginReport(battle);
                Save(resume ? "battle_restart" : "battle_start", resume ? "Restart from saved pre-battle checkpoint." : null);
                modal = Modal.None;
                CatalogCombatLab lab = GetComponent<CatalogCombatLab>();
                if (lab == null) lab = gameObject.AddComponent<CatalogCombatLab>();
                enabled = false;
                lab.OpenExpeditionBattle(battle, run.Wave, ExpeditionRun.TotalWaves, run.EncounterName,
                    victory => { QueueEnding(battle, victory); run.ResolveBattle(victory); CompleteReport();
                        Save(victory ? "battle_win" : "battle_loss", battleSeconds: battle.Time); },
                    () => { home = true; Save("battle_exit", "Return home; no inferred defeat.", battle.Time); enabled = true; },
                    FinishBattlePresentation);
                lab.ConfigureExpeditionSpeed(run.BattleSpeed, value => { if (run.SetBattleSpeed(value)) Save("battle_speed", value.ToString()); });
            }
            catch (Exception exception) { enabled = true; Notice(CatalogText.Get("text.0779") + exception.Message); }
        }

        void OpenLab()
        {
            playtest.Record("open_developer_lab", run, "Lab battles are excluded from expedition results.");
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
                if (run.DeployedIds[i] == id) return CatalogText.Get("text.0213") + (i + 1);
            int benchIndex = 0;
            foreach (CatalogMember member in run.Roster.Members)
            {
                if (IsDeployed(member.Id)) continue;
                benchIndex++;
                if (member.Id == id) return CatalogText.Get("text.0478") + benchIndex;
            }
            return CatalogText.Get("text.0780");
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
            string action = growth ? CatalogText.Get("text.0781") :
                quickSwap ? CatalogText.Get("text.0782") : donor != null && donor.MaterialLocked ? CatalogText.Get("text.0783") : CatalogText.Get("text.0784");
            ExpeditionUiTheme.PanelBox(new Rect(908,240,344,300),true);
            if(art!=null)art.Draw(resultForm,new Rect(1020,252,120,126),0);
            LocalizedGui.Label(new Rect(924,386,312,40),action,heading);
            LocalizedGui.Label(new Rect(924,430,312,48),growth?CatalogText.Get("text.0785")+recipient.Growth+" → "+(recipient.Growth+1)+CatalogText.Get("text.0786"):quickSwap?CatalogText.Get("text.0787"):CatalogText.Get("text.0788"),small);
            if(growth){float p=hoverArmed?1:Mathf.Clamp01((Time.unscaledTime-hoverStartTime)/GrowthHoverSeconds);Fill(new Rect(924,486,312,8),PanelLight);Fill(new Rect(924,486,312*p,8),Gold);LocalizedGui.Label(new Rect(924,501,312,28),hoverArmed?CatalogText.Get("text.0789"):CatalogText.Get("text.0790"),small);}
        }

        Rect MemberVisualRect(string id)
        {
            CatalogMember member = run.Roster.Find(id);
            if (member == null) return new Rect();
            if (IsDeployed(id))
            {
                Vector2 foot = ArenaPoint(member);
                return PreparationLayout.Hit(foot);
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
            if (string.IsNullOrEmpty(id)) return CatalogText.Get("text.0172");
            for (int i = 0; i < TraitIds.Length; i++) if (TraitIds[i] == id) return TraitNames[i];
            return CatalogText.Get("text.0791");
        }

        static Vector2 ArenaPoint(CatalogMember member) { return PreparationLayout.Point(member.X, member.Y); }

        void Update()
        {
            if (ending != null)
            {
                if (ending.Active) { ending.Tick(Time.unscaledDeltaTime); CancelPointer(); return; }
                ending.Dispose(); ending = null;
            }
            PresentationPerformanceProbe.Mark(!startupReady ? "startup" : home ? "home" :
                run != null && run.Phase != ExpeditionPhase.Preparation ? "result" : modal == Modal.None ? "preparation" : "dialog");
            if (!startupReady || home || run == null || run.Phase != ExpeditionPhase.Preparation ||
                modal != Modal.None || GrowthRevealActive || viewport.width <= 0) { CancelPointer(); return; }
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
            if (!TutorialPointerAllowed(point, down, up) || !LessonPointerAllowed(point, down, up))
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
                    Save("transfer", recipientId + ":" + id);
                    growthPulseId = recipientId;
                    growthPulseUntil = Time.unscaledTime + .6f;
                    CatalogMember grown = run.Roster.Find(recipientId);
                    Notice(CatalogText.Get("text.0792"));
                    if (ReadyToEvolve(grown)) PrepareEvolution(grown, true);
                }
                else Notice(CatalogText.Get("text.0793"));
                return;
            }
            if (recipientId != null && !IsDeployed(recipientId))
            {
                Notice(CatalogText.Get("text.0794"));
                return;
            }
            if (PreparationLayout.Board.Contains(point))
            {
                if (!IsDeployed(id))
                {
                    if (run.DeployedIds.Count >= 5)
                    {
                        string replace = ActiveAt(point, id);
                        if (replace == null || !run.SwapDeployment(replace, id))
                        { Notice(CatalogText.Get("text.0795")); return; }
                    }
                    else if (!run.SetDeployed(id, true))
                    { Notice(CatalogText.Get("text.0796")); return; }
                }
                if (run.Move(id, PreparationLayout.World(pointerCurrent).x, PreparationLayout.World(pointerCurrent).y)) Save("position", id);
                else Save("deployment", id);
                SelectPreparation(id);
            }
            else if (point.y >= 576 && IsDeployed(id))
            {
                if (run.SetDeployed(id, false)) Save("bench", id);
                else Notice(CatalogText.Get("text.0797"));
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
            float nearest = float.PositiveInfinity;
            foreach (string id in run.DeployedIds)
            {
                if (id == except) continue;
                CatalogMember member = run.Roster.Find(id);
                if (member == null) continue;
                Vector2 foot = ArenaPoint(member);
                if (!PreparationLayout.Hit(foot).Contains(point)) continue;
                // A large rear sprite remains selectable where bodies overlap.
                float distance = (point - (foot + new Vector2(0,-74))).sqrMagnitude;
                if (distance <= nearest) { selected=id; nearest=distance; }
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
