using System;
using System.Collections.Generic;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Self-contained catalog test surface. The stage prototype is suspended while this is open.
    public sealed class CatalogCombatLab : MonoBehaviour
    {
        const float Width = 1280f, Height = 720f, Tick = 1f / 60f;
        const float SettlementDuration = .55f;
        const string ArtRoot = "Art/Units/v010/";
        static readonly string[] FormIds = {
            "B-KN", "B-AR", "B-FI", "B-PR", "B-IC", "B-BA",
            "E-KN-G", "E-KN-C", "E-AR-P", "E-AR-T", "E-FI-B", "E-FI-E",
            "E-PR-G", "E-PR-H", "E-IC-F", "E-IC-S", "E-BA-G", "E-BA-A",
            "F-KN", "F-AR", "F-FI", "F-PR", "F-IC", "F-BA",
            "H-01", "H-02", "H-03", "H-04", "H-05", "H-06"
        };
        static readonly string[] English = {
            "Shield Knight", "Scout Archer", "Fire Mage", "Lantern Priest", "Frost Mage", "Oath Rider",
            "Guardian Knight", "Counter Knight", "Piercing Archer", "Tracking Archer", "Blast Mage", "Ember Mage",
            "Guardian Priest", "Healing Priest", "Freeze Mage", "Frost Skirmisher", "Guardian Rider", "Vanguard Rider",
            "Beacon Shieldmaster", "Twilight Marksman", "Beacon Archmage", "Dawn Guide", "Frost Warden", "Oath Bannerlord",
            "Ember Swordguard", "Lantern Paladin", "Frost Tracker", "Melt Mage", "Hymn Rider", "Expedition Captain"
        };
        static readonly string[] Traits = { "", "T-KN", "T-AR", "T-FI", "T-PR", "T-IC", "T-BA",
            "T-H01", "T-H02", "T-H03", "T-H04", "T-H05", "T-H06" };
        static readonly string[] TraitNames = { "None", "Shield", "Scout", "Flame", "Lantern", "Frost", "Oath",
            "Ember Guard", "Warmth", "Frost Track", "Melt", "Harmony", "Spoils" };
        static readonly string[] CategoryNames = { "BASE 6", "BRANCH", "FINAL 6", "FUSION 6" };
        static readonly string[][] Presets = {
            new[] { "B-KN", "B-AR", "B-PR" },
            new[] { "B-KN", "B-BA", "B-AR", "B-FI", "B-IC" },
            new[] { "E-KN-G", "E-BA-A", "E-AR-P", "E-FI-B", "E-PR-H" },
            new[] { "H-01", "H-02", "H-03", "H-04", "H-06" },
            new string[0]
        };
        static readonly string[] PresetNames = { "BASIC 3", "BASIC 5", "BRANCH 5", "FUSION 5", "BOSS (BASIC)" };
        static readonly string[][] FusionFamilies = {
            new[] { "KN", "FI" }, new[] { "KN", "PR" }, new[] { "AR", "IC" },
            new[] { "FI", "IC" }, new[] { "PR", "BA" }, new[] { "AR", "BA" }
        };
        static readonly Color Ink = new Color(.06f, .11f, .12f);
        static readonly Color Panel = new Color(.11f, .18f, .19f, .97f);
        static readonly Color Gold = new Color(.99f, .78f, .41f);

        sealed class Slot
        {
            public string Id, FormId, BranchId, TraitId;
            public Vector2 Position;
        }
        sealed class View
        {
            public CatalogBattleUnit Unit;
            public Vector2 LastPosition;
            public float StateTime, HitTime, WindupAge, ReleaseAge, ReleaseRemaining;
            public string ReleaseAbilityId;
            public int State;
            public bool NewHit, NewRelease, WasWinding;
        }
        struct DrawnCombatant
        {
            public View View;
            public Vector2 Point;
            public float Height;
            public bool RegionalArtLoaded;
        }
        sealed class FloatText
        {
            public string Text;
            public Vector2 Position;
            public Color Color;
            public float Age;
        }

        [Serializable] sealed class ArtManifest { public ArtEntry[] entries; }
        [Serializable] sealed class ArtEntry
        {
            public string id, resourcePath;
            public int width, height;
            public ArtFrame[] frames;
        }
        [Serializable] sealed class ArtFrame
        {
            public float x, y, width, height, pivotX, pivotY;
        }

        readonly List<Slot> party = new List<Slot>();
        readonly List<View> combatViews = new List<View>();
        readonly List<FloatText> floating = new List<FloatText>();
        readonly Dictionary<string, Texture2D> sheets = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, RenderTexture> clippedSheets = new Dictionary<string, RenderTexture>();
        readonly Dictionary<string, ArtEntry> artEntries = new Dictionary<string, ArtEntry>();
        readonly HashSet<string> missingSheets = new HashSet<string>();
        readonly HashSet<string> neededSheets = new HashSet<string>();
        CatalogBodyScale bodyScale;
        ForestEnemyArt forestEnemyArt;
        BattlePrototype parent;
        Camera parentCamera;
        CatalogDatabase database;
        CatalogBattle battle;
        CatalogBattleEffects battleEffects;
        Material clippedSpriteMaterial;
        Texture2D bossConceptSheet;
        Texture2D expeditionBackground, raiderConcept;
        Action standaloneClose, expeditionExit;
        Action<bool> expeditionResult;
        bool expeditionMode;
        bool settling, settlementVictory;
        float settlementAge;
        int expeditionWave, expeditionTotal;
        string expeditionEncounter;
        string error, lastAction;
        string latestSkill;
        float latestSkillUntil, bossNoticeUntil;
        int selected, category, trait, branch, fusionPair, preset, galleryState, screen, dragged = -1;
        float clock, accumulator, speed = 1f, galleryTime;
        bool paused, galleryAuto;
        GUIStyle title, heading, label, small, button, tiny, centered;
        Font uiFont;
        Rect viewport;

        public void Open(BattlePrototype owner, Camera camera)
        {
            expeditionMode = false;
            standaloneClose = expeditionExit = null;
            expeditionResult = null;
            parent = owner;
            parentCamera = camera;
            if (clippedSpriteMaterial == null)
            {
                Shader clipped = Shader.Find("Expedition/CatalogSpriteClip");
                if (clipped != null && clipped.isSupported) clippedSpriteMaterial = new Material(clipped);
            }
            var json = Resources.Load<TextAsset>("Content/unit-catalog-v010");
            try
            {
                if (forestEnemyArt != null) forestEnemyArt.Release();
                forestEnemyArt = new ForestEnemyArt();
                bodyScale = new CatalogBodyScale();
                database = json == null ? null : CatalogDatabase.FromJson(json.text);
                error = database == null ? "Missing Resources/Content/unit-catalog-v010.json" : null;
                artEntries.Clear();
                var manifestAsset = Resources.Load<TextAsset>("Art/Units/v010/art-manifest");
                if (manifestAsset != null)
                {
                    ArtManifest manifest = JsonUtility.FromJson<ArtManifest>(manifestAsset.text);
                    if (manifest != null && manifest.entries != null)
                        foreach (ArtEntry entry in manifest.entries)
                            if (entry != null && !string.IsNullOrEmpty(entry.id)) artEntries[entry.id] = entry;
                }
            }
            catch (Exception exception) { error = "Catalog data error: " + exception.Message; }
            selected = category = trait = branch = fusionPair = preset = screen = 0;
            galleryAuto = false;
            lastAction = null;
            party.Clear();
            ClearBattle();
            enabled = true;
        }

        public void OpenStandalone(Action onClose)
        {
            Open(null, null);
            standaloneClose = onClose;
        }

        // The real expedition owns progression and saving; the lab only plays this battle.
        public void OpenExpeditionBattle(CatalogBattle runBattle, int wave, int total,
            string encounter, Action<bool> onResult, Action onExit)
        {
            if (runBattle == null || onResult == null || onExit == null)
                throw new ArgumentNullException("Expedition battle callbacks and battle are required.");
            Open(null, null);
            expeditionMode = true;
            expeditionWave = wave;
            expeditionTotal = total;
            expeditionEncounter = encounter;
            expeditionResult = onResult;
            expeditionExit = onExit;
            expeditionBackground = Resources.Load<Texture2D>(wave <= 5 ?
                "Art/forest-battlefield" : wave <= 10 ?
                "Art/ruined-outpost-battlefield" : wave <= 15 ?
                "Art/Environments/v024/furnace-battlefield" : wave <= 20 ?
                "Art/Environments/v029/frost-battlefield" :
                "Art/Environments/v030/garden-battlefield");
            battle = runBattle;
            battleEffects = new CatalogBattleEffects(gameObject);
            foreach (CatalogBattleUnit unit in battle.Units)
                combatViews.Add(new View { Unit = unit, LastPosition = new Vector2(unit.X, unit.Y) });
            battle.Event += OnBattleEvent;
            speed = 1f;
            screen = 1;
        }

        void OnDisable()
        {
            ClearBattle(); ReleaseSheets();
            if (forestEnemyArt != null) forestEnemyArt.Release();
            forestEnemyArt = null;
            bodyScale = null;
            if (clippedSpriteMaterial != null)
            {
                if (Application.isPlaying) Destroy(clippedSpriteMaterial);
                else DestroyImmediate(clippedSpriteMaterial);
            }
            clippedSpriteMaterial = null;
        }

        void OnApplicationPause(bool applicationPaused)
        {
            if (applicationPaused) SetPaused(true);
        }

        void SetPaused(bool value)
        {
            paused = value;
            if (battleEffects != null) battleEffects.SetPaused(value);
        }

        void Close()
        {
            Action callback = expeditionMode ? expeditionExit : standaloneClose;
            enabled = false;
            if (parentCamera != null) parentCamera.enabled = true;
            if (parent != null) parent.enabled = true;
            if (callback != null) callback();
        }

        void ClearBattle()
        {
            if (battleEffects != null) { battleEffects.Dispose(); battleEffects = null; }
            if (battle != null) battle.Event -= OnBattleEvent;
            battle = null;
            combatViews.Clear();
            floating.Clear();
            accumulator = clock = 0;
            settling = settlementVictory = false;
            settlementAge = 0;
            paused = false;
            latestSkill = null;
            bossNoticeUntil = 0;
        }

        void StartBattle()
        {
            if (database == null || party.Count == 0) return;
            ClearBattle();
            battle = new CatalogBattle(database);
            for (int i = 0; i < party.Count; i++)
            {
                Slot slot = party[i];
                battle.AddUnit(slot.Id, CatalogTeam.Ally, slot.FormId, slot.BranchId, slot.TraitId,
                    slot.Position.x, slot.Position.y);
            }
            string[] enemies = Presets[preset];
            for (int i = 0; i < enemies.Length; i++)
                battle.AddUnit("enemy" + i, CatalogTeam.Enemy, enemies[i], null, null,
                    2.5f + .65f * (i % 2), -2f + (i / 2) * 1.4f);
            if (preset == 4)
                battle.AddEnemy("enemyBoss", 1150f, 46f, 36f, 1.55f, 2f, 1.15f, true, 3.1f, -.3f);
            foreach (CatalogBattleUnit unit in battle.Units)
                combatViews.Add(new View { Unit = unit, LastPosition = new Vector2(unit.X, unit.Y) });
            battle.Event += OnBattleEvent;
            screen = 1;
            TrimSheets();
        }

        void OnBattleEvent(CatalogBattleEvent e)
        {
            if (battleEffects != null) battleEffects.Observe(e, battle);
            View source = combatViews.Find(v => v.Unit.Id == e.SourceId);
            View target = combatViews.Find(v => v.Unit.Id == e.TargetId);
            if ((e.Kind == CatalogEventKind.SkillUsed || e.Kind == CatalogEventKind.BasicHit ||
                e.Kind == CatalogEventKind.ChargeHit || e.Kind == CatalogEventKind.BossImpact) && source != null)
            {
                source.ReleaseRemaining = .18f;
                source.ReleaseAge = 0;
                source.ReleaseAbilityId = e.AbilityId;
                source.NewRelease = true;
                if (e.Kind == CatalogEventKind.SkillUsed && clock >= bossNoticeUntil)
                {
                    latestSkill = expeditionMode ? UnitName(source.Unit) + " · 기술 발동" :
                        source.Unit.FormId + "  /  " + e.AbilityId;
                    latestSkillUntil = clock + 2f;
                }
            }
            if (e.Kind == CatalogEventKind.ChargeTelegraph && source != null &&
                (source.Unit.IsBoss || clock >= bossNoticeUntil))
            {
                latestSkill = source.Unit.IsBoss ? "우두머리 돌진 · 표시된 경로를 공격합니다" :
                    CastleEnemyArt.SpeciesFor(source.Unit) == CastleEnemyArt.Species[2] ?
                    "갑주 석상이 방패를 듭니다 · 돌진 예고" : "야수가 몸을 낮춥니다 · 돌진 예고";
                latestSkillUntil = clock + 1.1f;
                if (source.Unit.IsBoss) bossNoticeUntil = latestSkillUntil;
            }
            if (e.Kind == CatalogEventKind.BossTelegraph && source != null)
            {
                latestSkill = e.AbilityId == "FURNACE_EMBER" ?
                    "후속 불씨파 · 작은 원 안을 다시 공격합니다" :
                    e.AbilityId == "FURNACE_BLAST" ?
                    "용광로 폭발 · 표시된 범위에 화상을 남깁니다" :
                    e.AbilityId == "FROST_NOVA" ?
                    "서리 폭발 · 먼 동료 위치에 빙결 원형 공격" :
                    e.AbilityId == "GARDEN_BRIARS" ?
                    "가시뿌리 · 먼 동료 최대 셋 발밑을 차례로 공격" :
                    e.AbilityId == "GOLEM_AFTERSHOCK" ?
                    "후속 충격파 · 작은 원 안을 다시 공격합니다" :
                    "골렘 내려찍기 · 표시된 범위를 공격합니다";
                latestSkillUntil = clock + 1.4f;
                bossNoticeUntil = latestSkillUntil;
            }
            if (e.Kind == CatalogEventKind.BossEnraged && source != null)
            {
                latestSkill = UnitName(source.Unit) + " 분노 · " +
                    (source.Unit.EnemyRole == CatalogEnemyRole.Beast ? "돌진 간격 단축" :
                    source.Unit.EnemyRole == CatalogEnemyRole.Furnace ? "폭발 뒤 불씨파 추가" :
                    source.Unit.EnemyRole == CatalogEnemyRole.FrostWarden ? "서리 원형 공격 간격 단축" :
                    source.Unit.EnemyRole == CatalogEnemyRole.GardenLord ? "독가시 공격 간격 단축" :
                    "내려찍기 뒤 충격파 추가");
                latestSkillUntil = clock + 3f;
                bossNoticeUntil = latestSkillUntil;
            }
            if (e.Kind == CatalogEventKind.Damage && target != null)
            {
                // Bosses keep their own attack/locomotion pose when hit. HP,
                // impact effects and damage numbers still communicate damage.
                target.HitTime = target.Unit.IsBoss ? 0f : .24f;
                target.NewHit = !target.Unit.IsBoss;
                if (floating.Count < 24) floating.Add(new FloatText {
                    Text = "−" + e.Amount.ToString("0"), Position = new Vector2(target.Unit.X, target.Unit.Y),
                    Color = target.Unit.Team == CatalogTeam.Ally ? new Color(1f, .53f, .47f) : Gold });
            }
            if ((e.Kind == CatalogEventKind.Heal || e.Kind == CatalogEventKind.Shield) && target != null && floating.Count < 24)
                floating.Add(new FloatText { Text = (e.Kind == CatalogEventKind.Heal ? "+" : "◇") + e.Amount.ToString("0"),
                    Position = new Vector2(target.Unit.X, target.Unit.Y), Color = e.Kind == CatalogEventKind.Heal ?
                    new Color(.55f, 1f, .64f) : new Color(.5f, .84f, 1f) });
            if (e.Kind == CatalogEventKind.Shield && e.AbilityId == "MINE_GUARD" && source != null)
            {
                source.ReleaseRemaining = .18f;
                source.ReleaseAge = 0f;
                source.ReleaseAbilityId = e.AbilityId;
                source.NewRelease = true;
                if (clock >= bossNoticeUntil)
                {
                    latestSkill = UnitName(source.Unit) + " · 주변 적에게 보호막";
                    latestSkillUntil = clock + 1.8f;
                }
            }
            if (e.Kind == CatalogEventKind.Freeze && target != null)
            {
                target.HitTime = 0f;
                target.NewHit = false;
                target.ReleaseRemaining = 0f;
                target.NewRelease = false;
                if (floating.Count < 24)
                    floating.Add(new FloatText { Text = "빙결", Position = new Vector2(target.Unit.X, target.Unit.Y),
                        Color = new Color(.59f, .88f, 1f) });
            }
            if (e.Kind == CatalogEventKind.Poison && target != null && floating.Count < 24)
                floating.Add(new FloatText { Text = "중독", Position = new Vector2(target.Unit.X, target.Unit.Y),
                    Color = new Color(.73f, 1f, .42f) });
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            galleryTime += dt;
            if (screen == 0 && viewport.width > 0) HandlePlacementInput();
            if (screen == 2 && galleryAuto)
            {
                float phase = galleryTime % 4.85f;
                galleryState = phase < 1.6f ? 0 : phase < 2.7f ? 1 : phase < 3.35f ? 2 :
                    phase < 3.75f ? 3 : 4;
            }
            if (screen != 1 || battle == null || paused) return;
            float gameDt = dt * speed;
            if (battleEffects != null)
            {
                bool impactHold = battleEffects.HitStopRemaining > 0;
                battleEffects.Tick(dt, impactHold ? 0 : settling ? dt : gameDt, battle);
                if (impactHold) return;
            }
            if (!battle.Ended && !settling && (!expeditionMode || clock < 75f))
            {
                accumulator += gameDt;
                int steps = 0;
                while (accumulator >= Tick && steps++ < 12 && !battle.Ended && (!expeditionMode || clock < 75f))
                {
                    battle.Step(Tick); accumulator -= Tick; clock += Tick;
                    if (battleEffects != null && battleEffects.HitStopRemaining > 0)
                    { accumulator = 0; break; }
                }
            }
            if (expeditionMode && !settling && (battle.Ended || clock >= 75f))
            {
                settling = true;
                settlementVictory = battle.Ended && battle.Winner == CatalogTeam.Ally;
                settlementAge = 0;
                accumulator = 0;
            }
            float visualDt = settling ? dt : gameDt;
            foreach (View view in combatViews)
            {
                float actorDt = view.Unit.FrozenRemaining > 0f ? 0f : visualDt;
                if (view.Unit.FrozenRemaining > 0f) view.HitTime = 0f;
                if (!view.NewHit) view.HitTime = Mathf.Max(0, view.HitTime - Mathf.Min(actorDt, .08f));
                view.NewHit = false;
                if (!view.NewRelease)
                {
                    float releaseDt = Mathf.Min(actorDt, .08f);
                    view.ReleaseRemaining = Mathf.Max(0, view.ReleaseRemaining - releaseDt);
                    view.ReleaseAge += releaseDt;
                }
                view.NewRelease = false;
                Vector2 position = new Vector2(view.Unit.X, view.Unit.Y);
                bool winding = view.Unit.AttackPhase != CatalogAttackPhase.Ready;
                if (winding) view.WindupAge = view.WasWinding ? Mathf.Min(.25f, view.WindupAge + actorDt) : 0;
                else view.WindupAge = 0;
                view.WasWinding = winding;
                int state = !view.Unit.IsAlive ? 4 : view.Unit.FrozenRemaining > 0f ? 0 : view.HitTime > 0 ? 3 :
                    view.ReleaseRemaining > 0 || winding ? 2 :
                    (position - view.LastPosition).sqrMagnitude > .00004f ? 1 : 0;
                if (state != view.State) { view.State = state; view.StateTime = 0; }
                else view.StateTime += actorDt;
                view.LastPosition = position;
            }
            for (int i = floating.Count - 1; i >= 0; i--)
            { floating[i].Age += dt; if (floating[i].Age > .75f) floating.RemoveAt(i); }
            AdvanceSettlement(dt);
        }

        void AdvanceSettlement(float realDt)
        {
            if (!settling || paused || battleEffects != null && battleEffects.HitStopRemaining > 0) return;
            settlementAge += Mathf.Max(0, realDt);
            if (settlementAge < SettlementDuration) return;
            settling = false;
            Action<bool> callback = expeditionResult;
            bool victory = settlementVictory;
            expeditionResult = null;
            enabled = false;
            if (callback != null) callback(victory);
        }

        void EnsureStyles()
        {
            if (title != null) return;
            uiFont = Font.CreateDynamicFontFromOSFont(new[] {
                "Noto Sans CJK KR", "Noto Sans KR", "Malgun Gothic", "Apple SD Gothic Neo", "Arial"
            }, 18);
            title = new GUIStyle(GUI.skin.label) { fontSize = 29, fontStyle = FontStyle.Bold, normal = { textColor = Gold } };
            heading = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = Color.white } };
            small = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(.8f, .9f, .88f) } };
            tiny = new GUIStyle(small) { fontSize = 12 };
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            if (uiFont != null)
                foreach (GUIStyle style in new[] { title, heading, label, small, tiny, centered, button }) style.font = uiFont;
        }

        void OnGUI()
        {
            Rect safe = Screen.safeArea;
            float scale = Mathf.Min(safe.width / Width, safe.height / Height);
            viewport = new Rect(safe.x + (safe.width - Width * scale) * .5f,
                Screen.height - safe.yMax + (safe.height - Height * scale) * .5f,
                Width * scale, Height * scale);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture);
            EnsureStyles();
            GUI.matrix = Matrix4x4.TRS(new Vector3(viewport.x, viewport.y), Quaternion.identity,
                new Vector3(scale, scale, 1));
            if (expeditionMode)
            {
                DrawExpeditionBattle();
                GUI.matrix = Matrix4x4.identity;
                return;
            }
            Fill(new Rect(0, 0, Width, Height), Ink);
            Fill(new Rect(0, 0, Width, 66), new Color(.035f, .085f, .09f));
            if (GUI.Button(new Rect(15, 11, 100, 43), standaloneClose != null ? "← HOME" : "← MAP", button)) { Close(); GUI.matrix = Matrix4x4.identity; return; }
            GUI.Label(new Rect(132, 12, 720, 44), "EMBER EXPEDITION  /  CATALOG COMBAT LAB", title);
            if (GUI.Button(new Rect(925, 10, 160, 45), screen == 2 ? "TEAM BUILDER" : "ANIMATION QA", button))
            { screen = screen == 2 ? 0 : 2; TrimSheets(); }
            if (GUI.Button(new Rect(1095, 10, 168, 45), "RESET BATTLE", button))
            { ClearBattle(); screen = 0; TrimSheets(); }
            if (error != null || database == null) GUI.Label(new Rect(30, 100, 1200, 80), error ?? "Catalog data unavailable", heading);
            else if (screen == 2) DrawGallery();
            else { DrawRoster(); DrawArena(); }
            GUI.matrix = Matrix4x4.identity;
        }

        void DrawRoster()
        {
            Fill(new Rect(14, 78, 375, 624), Panel);
            for (int i = 0; i < 4; i++)
                if (GUI.Button(new Rect(20 + i * 92, 87, 90, 38), CategoryNames[i], button))
                { category = i; selected = CategoryStart(i); branch = fusionPair = 0; TrimSheets(); }
            int start = CategoryStart(category), count = category == 1 ? 12 : 6;
            int columns = 2;
            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                CatalogForm form = database.Get(FormIds[index]);
                Rect rect = new Rect(24 + (i % columns) * 180, 136 + (i / columns) * 68, 174, 62);
                if (index == selected) Fill(rect, new Color(.51f, .37f, .18f));
                if (GUI.Button(rect, form.Name + "\n" + English[index], button))
                { selected = index; branch = fusionPair = 0; TrimSheets(); }
            }
            if (category != 1)
            {
                Fill(new Rect(25, 570, 352, 118), new Color(.06f, .13f, .14f));
                GUI.Label(new Rect(34, 578, 330, 35), "30 playable forms · same core data", small);
                GUI.Label(new Rect(34, 610, 330, 65), "Select a form, choose inherited trait and branch, then add up to five allies.", small);
            }
            Fill(new Rect(401, 78, 866, 244), Panel);
            CatalogForm chosen = database.Get(FormIds[selected]);
            GUI.Label(new Rect(420, 89, 550, 36), chosen.Name + "  /  " + English[selected], heading);
            GUI.Label(new Rect(420, 127, 550, 26), chosen.Id + "  ·  " + chosen.Tier + "  ·  " + chosen.Family, small);
            GUI.Label(new Rect(420, 162, 465, 66), "HP " + chosen.Hp.ToString("0") + "   ATK " + chosen.Power.ToString("0") +
                "   DEF " + chosen.Defense.ToString("0") + "\nRNG " + chosen.Range.ToString("0.0") +
                "   INT " + chosen.AttackInterval.ToString("0.00") + "s   MOVE " + chosen.MoveSpeed.ToString("0.00"), label);
            DrawSheet(chosen.Id, 0, (int)(galleryTime * 5) % 4, new Rect(910, 98, 104, 132), false);
            if (chosen.Id.StartsWith("F-", StringComparison.Ordinal))
            {
                string[] branchIds = BranchesFor(chosen.Id);
                if (GUI.Button(new Rect(420, 246, 240, 52), "BRANCH: " + branchIds[branch], button)) branch = 1 - branch;
            }
            else if (chosen.Id.StartsWith("H-", StringComparison.Ordinal))
            {
                string[] source = FusionFamilies[selected - 24];
                string left = BranchesFor("F-" + source[0])[fusionPair / 2];
                string right = BranchesFor("F-" + source[1])[fusionPair % 2];
                if (GUI.Button(new Rect(420, 246, 240, 52), left + " + " + right, button)) fusionPair = (fusionPair + 1) % 4;
            }
            if (GUI.Button(new Rect(675, 246, 257, 52), "TRAIT: " + TraitNames[trait], button)) trait = (trait + 1) % Traits.Length;
            GUI.enabled = party.Count < 5 && screen == 0;
            string addLabel = chosen.Id.StartsWith("E-", StringComparison.Ordinal) ||
                chosen.Id.StartsWith("F-", StringComparison.Ordinal) ? "EVOLVE + ADD" :
                chosen.Id.StartsWith("H-", StringComparison.Ordinal) ? "FUSE + ADD" : "+ ADD TO TEAM";
            if (GUI.Button(new Rect(946, 246, 303, 52), addLabel, button)) AddSelected();
            GUI.enabled = true;
            Fill(new Rect(401, 332, 866, 117), Panel);
            GUI.Label(new Rect(414, 337, 190, 30), "TEAM  " + party.Count + "/5", heading);
            if (lastAction != null) GUI.Label(new Rect(605, 342, 640, 27), lastAction, small);
            for (int i = 0; i < party.Count; i++)
            {
                Slot slot = party[i];
                Rect rect = new Rect(416 + i * 166, 370, 160, 68);
                GUI.enabled = screen == 0;
                if (GUI.Button(rect, database.Get(slot.FormId).Name + "\n" + slot.FormId, button))
                { party.RemoveAt(i); TrimSheets(); }
                GUI.enabled = true;
            }
        }

        void DrawExpeditionBattle()
        {
            Fill(new Rect(0, 0, Width, Height), Ink);
            if (expeditionBackground != null)
                GUI.DrawTexture(new Rect(0, 68, Width, 652), expeditionBackground, ScaleMode.ScaleAndCrop);
            Fill(new Rect(0, 68, Width, 652), new Color(.025f, .07f, .07f, .28f));
            Fill(new Rect(0, 0, Width, 76), new Color(.035f, .085f, .09f));
            GUI.Label(new Rect(26, 17, 690, 40), expeditionEncounter + "   " + expeditionWave + "/" + expeditionTotal, title);
            GUI.Label(new Rect(745, 24, 145, 30), clock.ToString("0.0") + " / 75초", label);
            if (GUI.Button(new Rect(904, 14, 125, 49), speed == 1f ? "1×" : "2×", button)) speed = speed == 1f ? 2f : 1f;
            if (GUI.Button(new Rect(1043, 14, 211, 49), paused ? "계속하기" : "일시정지", button)) SetPaused(!paused);
            if (battle == null) return;

            Rect arena = new Rect(24, 92, 1232, 560);
            if (battleEffects != null) battleEffects.DrawGround(battle, p => ExpeditionPoint(p, arena));
            // Bosses remain behind living allies and escorts. Other actors use foot depth.
            var sorted = new List<View>(combatViews);
            sorted.Sort((a, b) => BattleVisibility.Compare(a.Unit.IsAlive, a.Unit.Y, a.Unit.Team, a.Unit.Id,
                b.Unit.IsAlive, b.Unit.Y, b.Unit.Team, b.Unit.Id, a.Unit.IsBoss, b.Unit.IsBoss));
            var drawn = new List<DrawnCombatant>(sorted.Count);
            foreach (View view in sorted)
            {
                CatalogBattleUnit unit = view.Unit;
                Vector2 point = ExpeditionPoint(new Vector2(unit.X, unit.Y), arena);
                Color previous = GUI.color;
                if (!unit.IsAlive) GUI.color = new Color(1, 1, 1, Mathf.Max(.24f, 1f - view.StateTime));
                else if (unit.FrozenRemaining > 0f) GUI.color = new Color(.68f, .87f, 1f, 1f);
                else if (view.HitTime > .13f) GUI.color = new Color(1f, .62f, .48f, 1f);
                if (unit.IsAlive && view.HitTime > 0 && unit.FrozenRemaining <= 0f)
                    point.x += (unit.Team == CatalogTeam.Ally ? -1 : 1) *
                        Mathf.Sin(view.HitTime / .24f * Mathf.PI) * 6;
                bool regionalArtLoaded = false;
                float drawnHeight = 144f;
                Rect bounds;
                if (unit.Team == CatalogTeam.Enemy)
                    drawnHeight = DrawExpeditionEnemy(view, point, out regionalArtLoaded, out bounds);
                else bounds = DrawSheet(unit.FormId, view.State, BattleAnimationFrame(view),
                    new Rect(point.x - 60, point.y - 144, 120, 144), ShouldFlip(view));
                GUI.color = previous;
                drawn.Add(new DrawnCombatant { View = view, Point = point,
                    Height = drawnHeight, RegionalArtLoaded = regionalArtLoaded });
            }
            if (battleEffects != null) battleEffects.Draw(battle, p => ExpeditionPoint(p, arena));
            // HUD is painted after every silhouette.
            foreach (DrawnCombatant painted in drawn)
            {
                View view = painted.View;
                CatalogBattleUnit unit = view.Unit;
                Vector2 point = painted.Point;
                if (!unit.IsAlive) continue;
                float width = unit.IsBoss ? 174f : 74f;
                float barHeight = painted.RegionalArtLoaded ? painted.Height + (unit.IsBoss ? 23f : 12f) :
                    unit.Team == CatalogTeam.Ally ? AllyHudHeight(unit.FormId) :
                    unit.IsBoss ? 267f : unit.Team == CatalogTeam.Enemy &&
                    unit.Id.IndexOf("elite", StringComparison.Ordinal) >= 0 ? 176f : 134f;
                float barY = Mathf.Max(arena.y + 10, point.y - barHeight);
                float barX = Mathf.Clamp(point.x - width * .5f, arena.x + 3, arena.xMax - width - 3);
                Fill(new Rect(barX - 1, barY - 1, width + 2, 8), new Color(.02f, .035f, .035f, .9f));
                Fill(new Rect(barX, barY, width * Mathf.Clamp01(unit.Hp / unit.MaxHp), 6),
                    unit.Team == CatalogTeam.Ally ? new Color(.45f, .94f, .55f) : new Color(1f, .45f, .39f));
                if (unit.Shield > 0) Fill(new Rect(barX, barY + 9,
                    width * Mathf.Clamp01(unit.Shield / unit.MaxHp), 3), new Color(.5f, .84f, 1f));
                float statusY = barY + 16f;
                if (unit.FrozenRemaining > 0f)
                {
                    DrawStatus(new Rect(barX, statusY, width, 20), "빙결 ", unit.FrozenRemaining,
                        new Color(.035f, .18f, .27f, .87f), new Color(.68f, .91f, 1f));
                    statusY += 21f;
                }
                if (unit.BurnRemaining > 0f)
                {
                    DrawStatus(new Rect(barX, statusY, width, 20), "화상 ", unit.BurnRemaining,
                        new Color(.32f, .095f, .035f, .87f), new Color(1f, .72f, .43f));
                    statusY += 21f;
                }
                if (unit.PoisonRemaining > 0f)
                    DrawStatus(new Rect(barX, statusY, width, 20), "중독 ", unit.PoisonRemaining,
                        new Color(.12f, .16f, .075f, .87f), new Color(.78f, 1f, .48f));
                if (unit.IsBoss) GUI.Label(new Rect(barX - 30, barY - 33, 260, 31), UnitName(unit), heading);
                else if (paused) GUI.Label(new Rect(point.x - 78, point.y + 5, 156, 24), UnitName(unit), centered);
                if (unit.IsBoss && unit.BossEnraged)
                    GUI.Label(new Rect(barX, barY + 13, width, 26), "분노", centered);
            }
            foreach (FloatText text in floating)
            {
                Vector2 point = ExpeditionPoint(text.Position, arena);
                Color previous = GUI.color;
                GUI.color = new Color(text.Color.r, text.Color.g, text.Color.b, 1f - text.Age / .75f);
                float textY = Mathf.Clamp(point.y - 156f - text.Age * 42f, arena.y + 24, arena.yMax - 30);
                GUI.Label(new Rect(point.x - 40, textY, 80, 28), text.Text, centered);
                GUI.color = previous;
            }
            Fill(new Rect(0, 664, Width, 56), new Color(.035f, .085f, .09f, .92f));
            GUI.Label(new Rect(28, 677, 870, 30), latestSkill != null && latestSkillUntil > clock ?
                latestSkill : "전투가 끝나면 다음 준비에서 동료들이 회복합니다.", label);
            int alive = 0, enemies = 0;
            foreach (CatalogBattleUnit unit in battle.Units)
                if (unit.IsAlive) { if (unit.Team == CatalogTeam.Ally) alive++; else enemies++; }
            GUI.Label(new Rect(1010, 677, 250, 30), "아군 " + alive + "  ·  적 " + enemies, heading);
            if (paused)
            {
                Fill(new Rect(0, 76, Width, 588), new Color(.015f, .04f, .04f, .72f));
                Fill(new Rect(402, 220, 476, 276), Panel);
                GUI.Label(new Rect(440, 245, 400, 45), "잠시 쉬어가기", title);
                GUI.Label(new Rect(440, 302, 400, 50), "거점에 나가도 원정은 저장됩니다.\n이어하기는 이 전투의 시작부터 재개합니다.", label);
                if (GUI.Button(new Rect(440, 384, 184, 62), "계속하기", button)) SetPaused(false);
                if (GUI.Button(new Rect(642, 384, 198, 62), "거점으로", button)) Close();
            }
        }

        void DrawStatus(Rect rect, string name, float remaining, Color background, Color foreground)
        {
            Fill(rect, background);
            Color prior = GUI.color;
            GUI.color = foreground;
            GUI.Label(rect, name + remaining.ToString("0.0"), centered);
            GUI.color = prior;
        }

        string UnitName(CatalogBattleUnit unit)
        {
            if (unit.Team == CatalogTeam.Ally) return database.Get(unit.FormId).Name;
            if (unit.Id == "garden-boss") return "가시의 군주";
            if (unit.Id == "garden-keeper") return "정예 황혼정원사";
            if (unit.Id.StartsWith("garden-guard-", StringComparison.Ordinal)) return "가시 근위병";
            if (unit.Id.StartsWith("garden-spitter-", StringComparison.Ordinal)) return "독침 사냥꾼";
            if (unit.Id == "frost-boss") return "서리의 파수꾼";
            if (unit.Id == "frost-cantor") return "찬송 감시자";
            if (unit.Id.StartsWith("frost-guard-", StringComparison.Ordinal)) return "서리 근위병";
            if (unit.Id.StartsWith("frost-acolyte-", StringComparison.Ordinal)) return "서리 시종";
            if (unit.Id == "furnace-boss") return "용광로 수호자";
            if (unit.Id == "mine-elite") return "철쇄 감독관";
            if (unit.Id.StartsWith("mine-worker-", StringComparison.Ordinal)) return "잿재 채굴병";
            if (unit.Id.StartsWith("mine-slinger-", StringComparison.Ordinal)) return "불씨 투척병";
            if (unit.IsBoss) return unit.EnemyRole == CatalogEnemyRole.Beast ? "잿갈기 우두머리" : "성문 골렘";
            if (unit.Id.StartsWith("stone-elite", StringComparison.Ordinal)) return "갑주 석상";
            if (unit.Id.StartsWith("stone-", StringComparison.Ordinal)) return "돌편 보병";
            if (unit.Id.StartsWith("spine-", StringComparison.Ordinal)) return "가시등 짐승";
            if (unit.Id.StartsWith("crossbow-", StringComparison.Ordinal)) return "석궁 수비병";
            if (unit.Id.IndexOf("elite", StringComparison.Ordinal) >= 0) return "돌격 뿔짐승";
            return unit.EnemyRole == CatalogEnemyRole.Raider ||
                unit.Id.IndexOf("raider", StringComparison.Ordinal) >= 0 ? "초소 약탈자" : "뿔 야수";
        }

        static Vector2 ExpeditionPoint(Vector2 position, Rect arena)
        {
            return new Vector2(Mathf.Clamp(arena.center.x + position.x * 83f, arena.x + 75, arena.xMax - 75),
                Mathf.Clamp(arena.y + 405f - position.y * 48f, arena.y + 285, arena.yMax - 35));
        }

        float AllyHudHeight(string formId)
        {
            ArtEntry entry;
            if (bodyScale == null || !artEntries.TryGetValue(formId, out entry) ||
                entry == null || entry.frames == null || entry.frames.Length == 0 ||
                entry.frames[0] == null) return 134f;
            return bodyScale.IdleHudHeight(formId, entry.width, entry.height,
                entry.frames[0].height, 120f, 144f, 12f, 134f);
        }

        float DrawExpeditionEnemy(View view, Vector2 foot, out bool regionalArtLoaded, out Rect bounds)
        {
            regionalArtLoaded = false;
            bounds = default(Rect);
            string gardenSpecies = GardenEnemyArt.SpeciesFor(view.Unit);
            string frostSpecies = gardenSpecies == null ? FrostEnemyArt.SpeciesFor(view.Unit) : null;
            string mineSpecies = gardenSpecies == null && frostSpecies == null ? MineEnemyArt.SpeciesFor(view.Unit) : null;
            string castleSpecies = gardenSpecies == null && frostSpecies == null && mineSpecies == null ?
                CastleEnemyArt.SpeciesFor(view.Unit) : null;
            string species = gardenSpecies ?? frostSpecies ?? mineSpecies ?? castleSpecies ?? ForestEnemyArt.SpeciesFor(view.Unit);
            if (species != null && forestEnemyArt != null)
            {
                int frame = gardenSpecies != null ? GardenEnemyArt.FrameIndex(species,
                    view.State, view.StateTime, view.WindupAge, view.ReleaseRemaining, view.ReleaseAge,
                    view.Unit.AttackWindupRemaining, view.Unit.SlamWarningRemaining,
                    view.ReleaseAbilityId) :
                    frostSpecies != null ? FrostEnemyArt.FrameIndex(species,
                    view.State, view.StateTime, view.WindupAge, view.ReleaseRemaining, view.ReleaseAge,
                    view.Unit.AttackWindupRemaining, view.Unit.SlamWarningRemaining,
                    view.ReleaseAbilityId) :
                    mineSpecies != null ? MineEnemyArt.FrameIndex(species,
                    view.State, view.StateTime, view.WindupAge, view.ReleaseRemaining, view.ReleaseAge,
                    view.Unit.AttackWindupRemaining, view.Unit.SlamWarningRemaining,
                    view.Unit.SlamIsAftershock, view.ReleaseAbilityId) :
                    castleSpecies != null ? CastleEnemyArt.FrameIndex(species,
                    view.State, view.StateTime, view.WindupAge, view.ReleaseRemaining, view.ReleaseAge,
                    view.Unit.ChargeWarningRemaining, view.Unit.IsCharging,
                    view.Unit.AttackWindupRemaining, view.Unit.SlamWarningRemaining,
                    view.Unit.SlamIsAftershock, view.ReleaseAbilityId) :
                    ForestEnemyArt.FrameIndex(view.State, view.StateTime, view.WindupAge,
                        view.ReleaseRemaining, view.ReleaseAge, view.Unit.ChargeWarningRemaining,
                        view.Unit.IsCharging, view.Unit.AttackWindupRemaining,
                        species == ForestEnemyArt.Species[2] || species == ForestEnemyArt.Species[3]);
                Color beforeForest = GUI.color;
                if (view.Unit.BossEnraged) GUI.color *= new Color(1f, .72f, .62f);
                bool drewForest = forestEnemyArt.Draw(species, frame, foot, out bounds);
                GUI.color = beforeForest;
                if (drewForest)
                {
                    regionalArtLoaded = true;
                    return ForestEnemyArt.SilhouetteHeight(species);
                }
            }
            if (bossConceptSheet == null) bossConceptSheet = Resources.Load<Texture2D>("Art/expedition-sprites-v2");
            bool isRaider = view.Unit.EnemyRole == CatalogEnemyRole.Raider ||
                view.Unit.Id.IndexOf("raider", StringComparison.Ordinal) >= 0;
            bool stone = view.Unit.Id.StartsWith("stone-", StringComparison.Ordinal);
            bool spine = view.Unit.Id.StartsWith("spine-", StringComparison.Ordinal);
            bool elite = view.Unit.Id.IndexOf("elite", StringComparison.Ordinal) >= 0;
            Texture2D texture = bossConceptSheet;
            Rect crop;
            float width, height;
            bool flip = false;
            if (view.Unit.IsBoss && view.Unit.EnemyRole == CatalogEnemyRole.Beast)
            { crop = new Rect(892, 124, 300, 353); width = 216; height = 254; }
            else if (view.Unit.IsBoss)
            { crop = new Rect(63, 113, 333, 395); width = 214; height = 254; flip = true; }
            else if (stone)
            { crop = new Rect(63, 113, 333, 395); width = elite ? 138 : 96; height = elite ? 164 : 114; flip = true; }
            else if (isRaider && !spine)
            {
                if (raiderConcept == null) raiderConcept = Resources.Load<Texture2D>("Art/outpost-raider-archer");
                texture = raiderConcept;
                crop = new Rect(260, 182, 722, 904); width = 103; height = 129;
            }
            else { crop = new Rect(892, 124, 300, 353); width = elite ? 138 : 96; height = elite ? 162 : 113; }
            bounds = new Rect(foot.x - width * .5f, foot.y - height, width, height);
            if (texture == null) return height;
            // Keep the prior concept silhouette only when a regional sheet is unavailable.
            Color before = GUI.color;
            if (spine) GUI.color *= new Color(.7f, .88f, 1f);
            else if (elite) GUI.color *= new Color(1f, .8f, .62f);
            if (view.Unit.BossEnraged) GUI.color *= new Color(1f, .72f, .62f);
            // Existing concept sprites already face left, except the golem. Motion is temporary.
            float move = view.State == 1 ? Mathf.Sin(view.StateTime * 13f) * 3f : 0;
            float lunge = view.ReleaseRemaining > 0 ? Mathf.Sin(view.ReleaseAge / .18f * Mathf.PI) * 9f : 0;
            Rect destination = new Rect(foot.x - width / 2 - lunge, foot.y - height + move, width, height);
            if (view.Unit.SlamWarningRemaining > 0)
                destination.y -= 8 + Mathf.Sin(view.Unit.SlamWarningRemaining * 13) * 3;
            else if (view.Unit.IsBoss && view.Unit.ChargeWarningRemaining > 0)
            { destination.y += 10; destination.height -= 10; }
            if (!view.Unit.IsAlive) { destination.y += height * .72f; destination.height *= .28f; }
            bounds = destination;
            // These legacy textures retain their original dimensions (max import size 2048).
            float sourceWidth = texture.width;
            float sourceHeight = texture.height;
            Rect uv = new Rect(crop.x / sourceWidth, crop.y / sourceHeight, crop.width / sourceWidth, crop.height / sourceHeight);
            if (flip) { uv.x += uv.width; uv.width = -uv.width; }
            GUI.DrawTextureWithTexCoords(destination, texture, uv, true);
            GUI.color = before;
            return height;
        }

        void DrawArena()
        {
            Rect arena = new Rect(401, 459, 866, 190);
            Fill(arena, new Color(.065f, .14f, .13f));
            Fill(new Rect(401, 556, 866, 2), new Color(.41f, .48f, .32f));
            Fill(new Rect(401, 459, 520, 190), new Color(.3f, .75f, .51f, .035f));
            GUI.Label(new Rect(411, 464, 500, 25), screen == 0 ? "Drag allies to set their starting positions" :
                "Battle time " + clock.ToString("0.0") + "s", small);
            if (screen == 1 && latestSkill != null && latestSkillUntil > clock)
                GUI.Label(new Rect(786, 464, 447, 25), "SKILL  " + latestSkill, small);
            if (screen == 0)
            {
                for (int i = 0; i < party.Count; i++)
                {
                    Slot slot = party[i];
                    Vector2 point = ArenaPoint(slot.Position, arena);
                    DrawSheet(slot.FormId, 0, (int)(galleryTime * 5) % 4,
                        new Rect(point.x - 45, point.y - 106, 90, 106), false);
                    GUI.Label(new Rect(point.x - 56, point.y - 7, 112, 24), slot.FormId, centered);
                }
            }
            else if (battle != null)
            {
                foreach (View view in combatViews)
                {
                    CatalogBattleUnit unit = view.Unit;
                    Vector2 point = ArenaPoint(new Vector2(unit.X, unit.Y), arena);
                    int frame = BattleAnimationFrame(view);
                    if (unit.IsBoss) DrawBossConcept(point);
                    else DrawSheet(unit.FormId, view.State, frame, new Rect(point.x - 45, point.y - 106, 90, 106),
                        ShouldFlip(view));
                    float hp = unit.MaxHp <= 0 ? 0 : Mathf.Clamp01(unit.Hp / unit.MaxHp);
                    float barWidth = unit.IsBoss ? 120f : 70f;
                    float barY = point.y - (unit.IsBoss ? 150f : 110f);
                    Fill(new Rect(point.x - barWidth * .5f, barY, barWidth, 5), Color.black);
                    Fill(new Rect(point.x - barWidth * .5f, barY, barWidth * hp, 5), unit.Team == CatalogTeam.Ally ?
                        new Color(.45f, .94f, .55f) : new Color(1f, .45f, .39f));
                    if (unit.Shield > 0) Fill(new Rect(point.x - barWidth * .5f, barY + 7f,
                        barWidth * Mathf.Clamp01(unit.Shield / unit.MaxHp), 3), Color.cyan);
                    GUI.Label(new Rect(point.x - (unit.IsBoss ? 82f : 44f), point.y - 7,
                        unit.IsBoss ? 164f : 88f, 22), unit.IsBoss ? "GOLEM / TEMP ART" : unit.FormId ?? "ENEMY", tiny);
                }
                foreach (FloatText text in floating)
                {
                    Vector2 point = ArenaPoint(text.Position, arena);
                    Color before = GUI.color;
                    GUI.color = new Color(text.Color.r, text.Color.g, text.Color.b, 1 - text.Age / .75f);
                    GUI.Label(new Rect(point.x - 38, point.y - 120 - text.Age * 25, 80, 26), text.Text, centered);
                    GUI.color = before;
                }
                if (battle.Ended)
                {
                    int allies = 0, enemies = 0;
                    foreach (CatalogBattleUnit unit in battle.Units)
                        if (unit.IsAlive) { if (unit.Team == CatalogTeam.Ally) allies++; else enemies++; }
                    Fill(new Rect(667, 490, 344, 125), new Color(.02f, .08f, .09f, .89f));
                    GUI.Label(new Rect(694, 512, 300, 38), battle.Winner == CatalogTeam.Ally ? "VICTORY" : "DEFEAT", title);
                    GUI.Label(new Rect(694, 552, 300, 50), "Time " + clock.ToString("0.0") + "s · Allies " + allies +
                        " · Enemies " + enemies + "\nReset to adjust the team", small);
                }
            }
            Fill(new Rect(401, 660, 866, 46), Panel);
            GUI.enabled = screen == 0;
            if (GUI.Button(new Rect(412, 665, 210, 37), PresetNames[preset], button)) preset = (preset + 1) % Presets.Length;
            GUI.enabled = true;
            if (GUI.Button(new Rect(633, 665, 110, 37), speed == 1 ? "1×" : "2×", button)) speed = speed == 1 ? 2 : 1;
            GUI.enabled = screen == 1 && battle != null && !battle.Ended;
            if (GUI.Button(new Rect(752, 665, 130, 37), paused ? "RESUME" : "PAUSE", button)) paused = !paused;
            GUI.enabled = party.Count > 0;
            if (GUI.Button(new Rect(891, 665, 363, 37), screen == 1 ? "RETRY SAME TEAM" : "FIGHT", button)) StartBattle();
            GUI.enabled = true;
        }

        void HandlePlacementInput()
        {
            bool down, held, up;
            Vector2 pointerScreen;
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                pointerScreen = touch.position;
                down = touch.phase == TouchPhase.Began;
                up = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                held = !up;
            }
            else
            {
                pointerScreen = Input.mousePosition;
                down = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
                up = Input.GetMouseButtonUp(0);
            }
            Vector2 pointer = new Vector2((pointerScreen.x - viewport.x) * Width / viewport.width,
                (Screen.height - pointerScreen.y - viewport.y) * Height / viewport.height);
            Rect arena = new Rect(401, 459, 866, 190);
            if (down && arena.Contains(pointer))
            {
                for (int i = party.Count - 1; i >= 0; i--)
                {
                    Vector2 point = ArenaPoint(party[i].Position, arena);
                    if (new Rect(point.x - 50, point.y - 112, 100, 115).Contains(pointer))
                    { dragged = i; break; }
                }
            }
            else if (held && dragged >= 0 && dragged < party.Count)
            {
                party[dragged].Position = new Vector2(Mathf.Clamp((pointer.x - arena.center.x) / 65f, -5.9f, -.8f),
                    Mathf.Clamp((arena.center.y + 45f - pointer.y) / 23f, -2.5f, 2f));
            }
            if (up) dragged = -1;
        }

        void DrawGallery()
        {
            Fill(new Rect(17, 78, 1247, 626), Panel);
            GUI.Label(new Rect(36, 87, 950, 34), "ANIMATION QA  /  all 30 forms", heading);
            GUI.Label(new Rect(37, 124, 1150, 26), "Actual Resources sheet · 6 × 4 cells · missing art is shown explicitly", small);
            for (int i = 0; i < FormIds.Length; i++)
            {
                int column = i % 6, row = i / 6;
                Rect rect = new Rect(34 + column * 203, 169 + row * 67, 194, 60);
                if (i == selected) Fill(rect, new Color(.5f, .37f, .17f));
                if (GUI.Button(rect, database.Get(FormIds[i]).Name + "\n" + English[i], button))
                { selected = i; category = i < 6 ? 0 : i < 18 ? 1 : i < 24 ? 2 : 3; TrimSheets(); }
            }
            CatalogForm form = database.Get(FormIds[selected]);
            Fill(new Rect(37, 520, 580, 170), new Color(.06f, .13f, .14f));
            GUI.Label(new Rect(53, 529, 520, 32), form.Name + "  /  " + English[selected] + "  /  " + form.Id, heading);
            string[] states = { "IDLE", "WALK", "ATTACK", "HIT", "DEATH" };
            for (int i = 0; i < states.Length; i++)
                if (GUI.Button(new Rect(54 + i * 108, 573, 103, 43), states[i], button))
                { galleryAuto = false; galleryState = i; galleryTime = 0; }
            if (GUI.Button(new Rect(54, 627, 103, 35), galleryAuto ? "AUTO ✓" : "AUTO", button))
            { galleryAuto = !galleryAuto; galleryTime = 0; }
            float stateTime = GalleryStateTime();
            GUI.Label(new Rect(165, 632, 433, 37), "State: " + states[galleryState] + "  ·  frame " +
                AnimationFrame(galleryState, stateTime) + "  ·  " +
                (sheets.ContainsKey(form.Id) ? artEntries.ContainsKey(form.Id) ? "SOURCE ART + CROP DATA" :
                "SOURCE ART / GRID FALLBACK" : "MISSING ART"), label);
            Fill(new Rect(628, 518, 615, 174), new Color(.035f, .095f, .1f));
            DrawSheet(form.Id, galleryState, AnimationFrame(galleryState, stateTime), new Rect(787, 528, 155, 155), false);
            DrawSheet(form.Id, galleryState, AnimationFrame(galleryState, stateTime), new Rect(1005, 548, 115, 130), true);
            GUI.Label(new Rect(784, 673, 168, 25), "ALLY / RIGHT", small);
            GUI.Label(new Rect(999, 673, 180, 25), "ENEMY / LEFT", small);
        }

        void AddSelected()
        {
            CatalogForm form = database.Get(FormIds[selected]);
            int i = party.Count;
            string branchId = null, traitId = trait == 0 ? null : Traits[trait];
            if (form.Id.StartsWith("E-", StringComparison.Ordinal) ||
                form.Id.StartsWith("F-", StringComparison.Ordinal) || form.Id.StartsWith("H-", StringComparison.Ordinal))
            {
                CatalogMember result = BuildViaRoster(form.Id);
                if (result == null) { lastAction = "Roster progression failed; check the selected path."; return; }
                branchId = result.BranchId;
                traitId = result.InheritedTraitId;
                lastAction = form.Id.StartsWith("E-", StringComparison.Ordinal) ? "Roster: 2 transfers → branch" :
                    form.Id.StartsWith("F-", StringComparison.Ordinal) ?
                    "Roster: 2 + 4 transfers → branch → final" : "Roster: two branches → fusion";
                lastAction += " · inherited " + (traitId ?? "none / innate duplicate");
            }
            else
            {
                if (form.InnateTrait == traitId) traitId = null;
                lastAction = "Added " + form.Name + " · inherited " + (traitId ?? "none / innate duplicate");
            }
            party.Add(new Slot { Id = "ally" + i + "_" + Guid.NewGuid().ToString("N").Substring(0, 4),
                FormId = form.Id, BranchId = branchId, TraitId = traitId,
                Position = new Vector2(-5.3f + .8f * (i % 2), -2f + 1.1f * (i / 2)) });
            TrimSheets();
        }

        CatalogMember BuildViaRoster(string resultId)
        {
            var roster = new CatalogRoster(database);
            if (resultId.StartsWith("E-", StringComparison.Ordinal))
            {
                string family = resultId.Substring(2, 2);
                if (!BuildBranch(roster, "anchor", family, resultId)) return null;
                return ApplyInherited(roster, "anchor") ? roster.Find("anchor") : null;
            }
            if (resultId.StartsWith("F-", StringComparison.Ordinal))
            {
                string family = resultId.Substring(2);
                if (!BuildBranch(roster, "anchor", family, BranchesFor(resultId)[branch])) return null;
                for (int i = 0; i < 4; i++)
                {
                    string id = "final-donor-" + i;
                    if (!roster.Recruit(id, "B-AR", 0, 0) || !roster.Transfer(id, "anchor", false)) return null;
                }
                return roster.Evolve("anchor", resultId) && ApplyInherited(roster, "anchor") ? roster.Find("anchor") : null;
            }
            int fusion = FormIndex(resultId) - 24;
            if (fusion < 0 || fusion >= FusionFamilies.Length) return null;
            string[] families = FusionFamilies[fusion];
            if (!BuildBranch(roster, "anchor", families[0], BranchesFor("F-" + families[0])[fusionPair / 2]) ||
                !BuildBranch(roster, "partner", families[1], BranchesFor("F-" + families[1])[fusionPair % 2]))
                return null;
            bool needsTrait = trait > 0 && database.Get(resultId).InnateTrait != Traits[trait];
            bool applyAfterFusion = needsTrait && database.Get(roster.Find("anchor").FormId).InnateTrait == Traits[trait];
            if (needsTrait && !applyAfterFusion && !ApplyInherited(roster, "anchor")) return null;
            if (!roster.Combine("anchor", "partner", resultId)) return null;
            if (applyAfterFusion && !ApplyInherited(roster, "anchor")) return null;
            return roster.Find("anchor");
        }

        bool BuildBranch(CatalogRoster roster, string id, string family, string branchId)
        {
            if (!roster.Recruit(id, "B-" + family, 0, 0)) return false;
            for (int i = 0; i < 2; i++)
            {
                string donor = id + "-donor-" + i;
                if (!roster.Recruit(donor, "B-AR", 0, 0) || !roster.Transfer(donor, id, false)) return false;
            }
            return roster.Evolve(id, branchId);
        }

        bool ApplyInherited(CatalogRoster roster, string recipientId)
        {
            if (trait == 0) return true;
            CatalogMember recipient = roster.Find(recipientId);
            if (recipient == null) return false;
            string desired = Traits[trait];
            if (database.Get(recipient.FormId).InnateTrait == desired) return true;
            const string donorId = "trait-donor";
            if (trait <= 6)
            {
                if (!roster.Recruit(donorId, "B-" + desired.Substring(2), 0, 0)) return false;
            }
            else
            {
                int hybrid = trait - 7;
                string[] families = FusionFamilies[hybrid];
                if (!BuildBranch(roster, donorId, families[0], BranchesFor("F-" + families[0])[0]) ||
                    !BuildBranch(roster, "trait-partner", families[1], BranchesFor("F-" + families[1])[0]) ||
                    !roster.Combine(donorId, "trait-partner", "H-" + (hybrid + 1).ToString("00"))) return false;
            }
            return roster.Transfer(donorId, recipientId, true);
        }

        static string[] BranchesFor(string finalId)
        {
            string family = finalId.Substring(2);
            if (family == "KN") return new[] { "E-KN-G", "E-KN-C" };
            if (family == "AR") return new[] { "E-AR-P", "E-AR-T" };
            if (family == "FI") return new[] { "E-FI-B", "E-FI-E" };
            if (family == "PR") return new[] { "E-PR-G", "E-PR-H" };
            if (family == "IC") return new[] { "E-IC-F", "E-IC-S" };
            return new[] { "E-BA-G", "E-BA-A" };
        }

        static int CategoryStart(int value) => value == 0 ? 0 : value == 1 ? 6 : value == 2 ? 18 : 24;
        int FormIndex(string id)
        {
            for (int i = 0; i < FormIds.Length; i++) if (FormIds[i] == id) return i;
            return 0;
        }
        static int AnimationFrame(int state, float time)
        {
            if (state == 0) return (int)(time * 5) % 4;
            if (state == 1) return (int)(time * 8) % 6;
            if (state == 2) return Mathf.Min(5, (int)(time * 24) % 6);
            if (state == 3) return 4 + (int)(time * 12) % 2;
            return Mathf.Min(3, (int)(time * 7));
        }

        static int BattleAnimationFrame(View view)
        {
            if (view.State != 2) return AnimationFrame(view.State, view.StateTime);
            if (view.ReleaseRemaining > 0)
                return 3 + Mathf.Min(2, (int)(view.ReleaseAge / .06f));
            return Mathf.Min(2, (int)(view.WindupAge / .25f * 3f));
        }

        float GalleryStateTime()
        {
            if (!galleryAuto) return galleryTime;
            float phase = galleryTime % 4.85f;
            return galleryState == 0 ? phase : galleryState == 1 ? phase - 1.6f :
                galleryState == 2 ? phase - 2.7f : galleryState == 3 ? phase - 3.35f : phase - 3.75f;
        }

        bool ShouldFlip(View view)
        {
            CatalogBattleUnit unit = view.Unit, nearest = null;
            if (unit.Team == CatalogTeam.Enemy) return true;
            float distance = float.PositiveInfinity;
            foreach (CatalogBattleUnit other in battle.Units)
            {
                if (!other.IsAlive || other.Team == unit.Team) continue;
                float current = (new Vector2(other.X - unit.X, other.Y - unit.Y)).sqrMagnitude;
                if (current < distance) { nearest = other; distance = current; }
            }
            return nearest == null ? unit.Team == CatalogTeam.Enemy : nearest.X < unit.X;
        }

        Rect DrawSheet(string id, int state, int frame, Rect rect, bool flip)
        {
            if (string.IsNullOrEmpty(id)) return rect;
            ArtEntry entry;
            artEntries.TryGetValue(id, out entry);
            if (!sheets.TryGetValue(id, out Texture2D texture) && !missingSheets.Contains(id))
            {
                string path = entry != null && !string.IsNullOrEmpty(entry.resourcePath) ? entry.resourcePath : ArtRoot + id;
                if (path.StartsWith("Assets/Resources/", StringComparison.Ordinal)) path = path.Substring("Assets/Resources/".Length);
                if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 4);
                texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                {
                    texture.filterMode = FilterMode.Point;
                    sheets[id] = texture;
                    if (clippedSpriteMaterial != null && SystemInfo.supportsRenderTextures &&
                        SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
                    {
                        RenderTexture clipped = null;
                        RenderTexture previousTarget = RenderTexture.active;
                        try
                        {
                            clipped = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                            clipped.filterMode = FilterMode.Point;
                            if (clipped.Create())
                            {
                                Graphics.Blit(texture, clipped, clippedSpriteMaterial);
                                clippedSheets[id] = clipped;
                            }
                            else { clipped.Release(); Destroy(clipped); }
                        }
                        catch (Exception)
                        {
                            if (clipped != null) { clipped.Release(); Destroy(clipped); }
                        }
                        finally { RenderTexture.active = previousTarget; }
                    }
                }
                else missingSheets.Add(id);
            }
            if (texture == null)
            {
                Fill(rect, new Color(.42f, .13f, .14f));
                GUI.Label(new Rect(rect.x, rect.center.y - 24, rect.width, 48), "MISSING ART\n" + id, centered);
                return rect;
            }
            int index = state == 0 ? Mathf.Clamp(frame, 0, 3) : state == 1 ? 6 + Mathf.Clamp(frame, 0, 5) :
                state == 2 ? 12 + Mathf.Clamp(frame, 0, 5) : state == 3 ? Mathf.Clamp(frame, 4, 5) :
                18 + Mathf.Clamp(frame, 0, 3);
            Rect uv;
            float pivotX = .5f, pivotY = 0f;
            float cropWidth, cropHeight;
            if (entry != null && entry.frames != null && entry.frames.Length > index &&
                entry.frames[index] != null && entry.width > 0 && entry.height > 0 &&
                entry.frames[index].width > 0 && entry.frames[index].height > 0)
            {
                ArtFrame cell = entry.frames[index];
                uv = new Rect(cell.x / entry.width, cell.y / entry.height,
                    cell.width / entry.width, cell.height / entry.height);
                pivotX = Mathf.Clamp01(cell.pivotX);
                pivotY = Mathf.Clamp01(cell.pivotY);
                cropWidth = cell.width;
                cropHeight = cell.height;
            }
            else
            {
                int row = index / 6, column = index % 6;
                uv = new Rect(column / 6f, 1f - (row + 1) / 4f, 1f / 6f, 1f / 4f);
                cropWidth = texture.width / 6f;
                cropHeight = texture.height / 4f;
            }
            if (flip) { uv.x += uv.width; uv.width = -uv.width; pivotX = 1f - pivotX; }
            // The destination rectangle's bottom center is the ground anchor.
            float cellWidth = entry != null && entry.width > 0 ? entry.width / 6f : texture.width / 6f;
            float cellHeight = entry != null && entry.height > 0 ? entry.height / 4f : texture.height / 4f;
            float scale = Mathf.Min(rect.width / cellWidth, rect.height / cellHeight);
            float multiplier = bodyScale == null || entry == null ? 1f :
                bodyScale.Multiplier(id, entry.width, entry.height);
            Rect fitted = CatalogBodyScale.Fit(rect, pivotX, pivotY,
                cropWidth, cropHeight, scale, multiplier);
            Texture displayed = clippedSheets.TryGetValue(id, out RenderTexture clippedSheet) ? clippedSheet : (Texture)texture;
            GUI.DrawTextureWithTexCoords(fitted, displayed, uv, true);
            return fitted;
        }

        void DrawBossConcept(Vector2 foot)
        {
            if (bossConceptSheet == null) bossConceptSheet = Resources.Load<Texture2D>("Art/expedition-sprites-v2");
            Rect destination = new Rect(foot.x - 60f, foot.y - 140f, 120f, 140f);
            if (bossConceptSheet == null)
            {
                Fill(destination, new Color(.4f, .14f, .14f));
                GUI.Label(destination, "MISSING BOSS ART", centered);
                return;
            }
            Rect uv = new Rect((63f + 333f) / bossConceptSheet.width, 113f / bossConceptSheet.height,
                -333f / bossConceptSheet.width, 395f / bossConceptSheet.height);
            GUI.DrawTextureWithTexCoords(destination, bossConceptSheet, uv, true);
        }

        void TrimSheets()
        {
            neededSheets.Clear();
            if (database != null && selected < FormIds.Length) neededSheets.Add(FormIds[selected]);
            if (screen != 2)
            {
                foreach (Slot slot in party) neededSheets.Add(slot.FormId);
                if (screen == 1) foreach (string id in Presets[preset]) neededSheets.Add(id);
            }
            var remove = new List<string>();
            foreach (var pair in sheets) if (!neededSheets.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (string id in remove)
            {
                ReleaseClippedSheet(id);
                Resources.UnloadAsset(sheets[id]);
                sheets.Remove(id);
            }
        }

        void ReleaseSheets()
        {
            foreach (string id in new List<string>(clippedSheets.Keys)) ReleaseClippedSheet(id);
            foreach (Texture2D texture in sheets.Values) if (texture != null) Resources.UnloadAsset(texture);
            sheets.Clear();
            missingSheets.Clear();
        }

        void ReleaseClippedSheet(string id)
        {
            if (!clippedSheets.TryGetValue(id, out RenderTexture texture)) return;
            clippedSheets.Remove(id);
            if (texture != null) { texture.Release(); Destroy(texture); }
        }

        static Vector2 ArenaPoint(Vector2 position, Rect arena) => new Vector2(
            arena.center.x + position.x * 65f, arena.center.y - position.y * 23f + 45f);
        static void Fill(Rect rect, Color color)
        {
            Color before = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = before;
        }
    }
}
