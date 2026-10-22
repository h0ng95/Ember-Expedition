using System;
using System.Collections.Generic;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Main v0.11 route: home, preparation and the six encounter results.
    public sealed class ExpeditionScreen : MonoBehaviour
    {
        const string SaveKey = "ember.expedition.run.v011";
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
        static readonly string[][] HybridFamilies = {
            new[] { "FI", "KN" }, new[] { "KN", "PR" }, new[] { "AR", "IC" },
            new[] { "FI", "IC" }, new[] { "BA", "PR" }, new[] { "AR", "BA" }
        };
        static readonly string[] TraitIds = { "T-KN", "T-AR", "T-FI", "T-PR", "T-IC", "T-BA",
            "T-H01", "T-H02", "T-H03", "T-H04", "T-H05", "T-H06" };
        static readonly string[] TraitNames = { "버팀", "집중", "잔불", "첫 빛", "한기", "서약",
            "재가르기", "온기", "서리 추적", "열균열", "한마음", "전리품 보급" };

        enum Modal { None, Detail, ChooseDonor, ChoosePartner, Confirm, NewRun, BattleWarning }
        enum GrowthAction { None, Transfer, Evolve, Combine }

        CatalogDatabase database;
        ExpeditionRun run;
        CatalogArtView art;
        Texture2D battlefield;
        Camera backgroundCamera;
        GUIStyle title, heading, body, small, centered, button, primary, hint;
        Font font;
        Rect viewport;
        readonly List<CatalogMember> bench = new List<CatalogMember>();
        readonly List<CatalogMember> fieldMembers = new List<CatalogMember>(5);
        bool home = true, savePresent, badSave, inheritTrait, pointerDragging;
        string error, selectedId, pendingOtherId, pendingFormId, toast, pointerId;
        float toastUntil, pointerStartTime;
        Vector2 pointerStart, pointerCurrent, pointerGrabOffset;
        int benchPage, choosePage;
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
        void OnDisable() { if (art != null) { art.Dispose(); art = null; } }
        void OnDestroy() { if (font != null) Destroy(font); }
        void OnApplicationPause(bool paused) { if (paused) Save(); }

        void LoadSave()
        {
            savePresent = PlayerPrefs.HasKey(SaveKey);
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
            GUI.Label(new Rect(220, 265, 840, 70), "동료를 소환하고 배치해 여섯 번의 전투를 돌파하세요.", body);
            if (GUI.Button(new Rect(230, 385, 385, 76), "새 원정", primary))
            {
                if (savePresent) modal = Modal.NewRun; else NewRun();
            }
            GUI.enabled = modal == Modal.None && run != null;
            if (GUI.Button(new Rect(665, 385, 385, 76), "이어하기", button))
            { home = false; modal = Modal.None; }
            GUI.enabled = modal == Modal.None;
            if (error != null) GUI.Label(new Rect(220, 475, 840, 75), error, hint);
            if (GUI.Button(new Rect(1020, 650, 225, 48), "개발용 전투 시험실", button)) OpenLab();
        }

        void DrawHeader(string phase)
        {
            Fill(new Rect(0, 0, Width, 80), Panel);
            if (GUI.Button(new Rect(18, 15, 90, 50), "거점", button)) { home = true; modal = Modal.None; Save(); }
            GUI.Label(new Rect(130, 10, 545, 58), "잔불 원정대", title);
            GUI.Label(new Rect(705, 18, 320, 43), "전투 " + run.Wave + " / " + ExpeditionRun.TotalWaves +
                "     등불 " + run.Lantern, body);
            Fill(new Rect(1070, 17, 188, 43), PanelLight);
            GUI.Label(new Rect(1080, 21, 168, 33), phase, centered);
        }

        void DrawPreparation()
        {
            DrawHeader("전투 준비");
            Rect field = new Rect(17, 93, 1246, 449);
            if (battlefield != null) GUI.DrawTexture(field, battlefield, ScaleMode.ScaleAndCrop);
            else Fill(field, new Color(.075f, .15f, .12f));
            Fill(field, new Color(.015f, .045f, .03f, .23f));
            Fill(new Rect(32, 143, 687, 363), new Color(.04f, .12f, .075f, .26f));
            Fill(new Rect(724, 94, 523, 430), new Color(.025f, .075f, .06f, .86f));
            Fill(new Rect(48, 185, 615, 1), new Color(.57f, .62f, .41f, .32f));
            Fill(new Rect(48, 350, 615, 1), new Color(.57f, .62f, .41f, .32f));
            GUI.Label(new Rect(46, 100, 630, 34), "출전 배치  " + run.DeployedIds.Count + " / 5", heading);
            GUI.Label(new Rect(735, 101, 505, 37), run.EncounterName, heading);
            GUI.Label(new Rect(735, 146, 490, 35), run.EncounterDescription, hint);
            GUI.Label(new Rect(735, 193, 490, 70), "동료를 끌어 출전 위치를 바꿉니다.\n대기 동료를 전장에 놓으면 출전합니다.", body);
            GUI.Label(new Rect(735, 273, 480, 33), "전투 전 확인", hint);
            GUI.Label(new Rect(735, 313, 480, 150),
                "전투가 시작되면 남은 소환 기회는 사라집니다.\n동료를 누르면 전승, 진화, 결합을 준비할 수 있습니다.\n성장에는 다른 동료가 재료로 쓰일 수 있습니다.", body);
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
            if (pointerDragging && pointerId != null)
            {
                CatalogMember carried = run.Roster.Find(pointerId);
                if (carried != null && art != null)
                {
                    Fill(new Rect(pointerCurrent.x - 52, pointerCurrent.y - 103, 104, 111),
                        new Color(.88f, .73f, .43f, .25f));
                    art.Draw(carried.FormId, new Rect(pointerCurrent.x - 48, pointerCurrent.y - 99, 96, 99),
                        (int)(Time.unscaledTime * 5f) % 4);
                }
            }
            DrawDock();
        }

        void DrawDock()
        {
            Fill(new Rect(0, 550, Width, 170), Panel);
            GUI.Label(new Rect(23, 557, 145, 31), "대기석", heading);
            bench.Clear();
            foreach (CatalogMember member in run.Roster.Members)
                if (!IsDeployed(member.Id)) bench.Add(member);
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
            GUI.enabled = modal == Modal.None && run.SummonsRemaining > 0;
            if (GUI.Button(new Rect(944, 574, 310, 58), "동료 소환  ·  남음 " + run.SummonsRemaining, button)) Summon();
            GUI.enabled = modal == Modal.None && run.DeployedIds.Count > 0;
            if (GUI.Button(new Rect(944, 641, 310, 58), "전투 시작  ›", primary))
            {
                if (run.SummonsRemaining > 0) modal = Modal.BattleWarning;
                else StartBattle(false);
            }
            GUI.enabled = modal == Modal.None;
            if (GUI.Button(new Rect(1075, 483, 164, 44), "되돌리기", button))
            {
                if (run.Undo()) { selectedId = null; Save(); Notice("직전 준비 행동을 되돌렸습니다."); }
                else Notice("되돌릴 준비 행동이 없습니다.");
            }
        }

        static Rect BenchRect(int index) { return new Rect(150 + index * 155, 573, 145, 127); }

        void DrawUnit(CatalogMember member, Rect sprite, Rect name)
        {
            Fill(new Rect(sprite.center.x - 28, sprite.yMax - 10, 56, 7), new Color(.02f, .06f, .04f, .7f));
            if (art != null) art.Draw(member.FormId, sprite, (int)(Time.unscaledTime * 5f) % 4);
            string label = database.Get(member.FormId).Name;
            GUI.Label(name, label, centered);
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
                run.Phase == ExpeditionPhase.Result && (run.Lantern <= 0 ||
                    run.Wave == ExpeditionRun.TotalWaves && !run.LastVictory) ?
                "이 전투로 원정이 끝났습니다." : victory ? "다음 길을 준비하세요." : "등불을 들고 다시 출발할 수 있습니다.";
            GUI.Label(new Rect(225, 275, 830, 92),
                "승리 " + run.Victories + " / " + ExpeditionRun.TotalWaves + "     등불 " + run.Lantern +
                "\n" + outcome, body);
            if (run.Phase == ExpeditionPhase.Result)
            {
                if (GUI.Button(new Rect(260, 425, 760, 75), "계속하기", primary))
                { if (run.Continue()) { Save(); selectedId = null; } }
            }
            else if (GUI.Button(new Rect(260, 425, 760, 75), "거점으로", primary)) home = true;
        }

        void DrawModal()
        {
            Fill(new Rect(0, 0, Width, Height), new Color(0f, .015f, .01f, .82f));
            if (modal == Modal.Detail) DrawDetail();
            else if (modal == Modal.ChooseDonor || modal == Modal.ChoosePartner) DrawChooseMember();
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
                else Notice("출전은 다섯 명까지 가능합니다.");
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
            GUI.Label(new Rect(277, 568, 725, 49), "전승과 결합에 쓰인 동료는 떠납니다. 실행 전 결과를 확인합니다.", small);
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
                string result = transfer ? null : HybridResult(selected.FormId, other.FormId);
                if (!transfer && result == null) continue;
                candidates.Add(other);
            }
            int pages = Mathf.Max(1, (candidates.Count + 11) / 12);
            choosePage = Mathf.Clamp(choosePage, 0, pages - 1);
            int shown = 0;
            for (int index = choosePage * 12; index < candidates.Count && shown < 12; index++)
            {
                CatalogMember other = candidates[index];
                string result = transfer ? null : HybridResult(selected.FormId, other.FormId);
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

        void PrepareEvolution(CatalogMember member)
        {
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
            Fill(new Rect(335, 155, 610, 385), Panel);
            if (modal == Modal.NewRun)
            {
                GUI.Label(new Rect(375, 188, 530, 54), "새 원정을 시작할까요?", heading);
                GUI.Label(new Rect(375, 255, 530, 115),
                    "현재 저장된 원정을 새 출발로 바꿉니다.\n진행과 동료 편성은 되돌릴 수 없습니다.", body);
                ConfirmationButtons("새 원정 시작", NewRun, () => modal = Modal.None);
                return;
            }
            if (modal == Modal.BattleWarning)
            {
                GUI.Label(new Rect(375, 188, 530, 54), "소환 기회가 남았습니다", heading);
                GUI.Label(new Rect(375, 255, 530, 115),
                    "남은 " + run.SummonsRemaining + "회의 소환 기회는 전투를 시작하면 사라집니다.", body);
                ConfirmationButtons("전투 시작", () => StartBattle(false), () => modal = Modal.None);
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
            ConfirmationButtons("실행", ExecuteGrowth, () => modal = Modal.Detail);
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
            modal = Modal.Detail;
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

        static string HybridResult(string firstForm, string secondForm)
        {
            if (string.IsNullOrEmpty(firstForm) || string.IsNullOrEmpty(secondForm) ||
                !firstForm.StartsWith("E-", StringComparison.Ordinal) ||
                !secondForm.StartsWith("E-", StringComparison.Ordinal)) return null;
            string first = CatalogRoster.FamilyCode(firstForm), second = CatalogRoster.FamilyCode(secondForm);
            for (int i = 0; i < HybridFamilies.Length; i++)
                if ((HybridFamilies[i][0] == first && HybridFamilies[i][1] == second) ||
                    (HybridFamilies[i][1] == first && HybridFamilies[i][0] == second))
                    return "H-" + (i + 1).ToString("00");
            return null;
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
                modal != Modal.None || viewport.width <= 0) return;
            bool down, held, up;
            Vector2 screen;
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                screen = touch.position;
                down = touch.phase == TouchPhase.Began;
                up = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                held = !up;
            }
            else
            {
                screen = Input.mousePosition;
                down = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
                up = Input.GetMouseButtonUp(0);
            }
            Vector2 point = new Vector2((screen.x - viewport.x) * Width / viewport.width,
                (Screen.height - screen.y - viewport.y) * Height / viewport.height);
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Canceled)
            { pointerId = null; pointerDragging = false; return; }
            if (down)
            {
                pointerId = MemberAt(point);
                pointerStart = point;
                CatalogMember grabbed = pointerId == null ? null : run.Roster.Find(pointerId);
                pointerGrabOffset = grabbed != null && IsDeployed(pointerId) ?
                    ArenaPoint(grabbed) - point : Vector2.zero;
                pointerDragging = false;
                pointerStartTime = Time.unscaledTime;
            }
            if (held && pointerId != null && (point - pointerStart).sqrMagnitude > 144f)
                pointerDragging = true;
            pointerCurrent = point + pointerGrabOffset;
            if (!up || pointerId == null) return;
            string id = pointerId;
            bool wasDragging = pointerDragging;
            pointerId = null;
            pointerDragging = false;
            if (!wasDragging && Time.unscaledTime - pointerStartTime < .7f)
            { selectedId = id; modal = Modal.Detail; return; }
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
            { if (run.SetDeployed(id, false)) Save(); }
        }

        string MemberAt(Vector2 point)
        {
            string active = ActiveAt(point, null);
            if (active != null) return active;
            for (int i = 0; i < 5; i++)
            {
                int index = benchPage * 5 + i;
                if (index < bench.Count && BenchRect(i).Contains(point)) return bench[index].Id;
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
