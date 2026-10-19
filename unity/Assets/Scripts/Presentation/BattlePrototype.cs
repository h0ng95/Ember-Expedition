using System;
using System.Collections.Generic;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // A combat feel prototype: concept sprites with procedural motion, not finished animation.
    public sealed class BattlePrototype : MonoBehaviour
    {
        const float Tick = 1f / 60f;
        const float UiWidth = 1280f, UiHeight = 720f;
        static readonly int FlashId = Shader.PropertyToID("_FlashAmount");
        sealed class UnitView
        {
            public string Id;
            public Vector2 Position;
            public Vector2 StartPosition;
            public SpriteRenderer Renderer;
            public float Height, Hit, Punch, Death;
            public CombatUnit Unit;
            public Color Tint;
        }
        sealed class Effect
        {
            public SpriteRenderer Renderer;
            public Vector2 Start, End, Size;
            public Color Tint;
            public float Age, Duration;
            public bool Projectile;
            public string Source, Target;
        }
        sealed class Number
        {
            public Vector2 Position;
            public int Damage;
            public float Age;
            public bool AlliedTarget;
        }
        readonly Dictionary<string, UnitView> views = new Dictionary<string, UnitView>();
        readonly List<Effect> effects = new List<Effect>();
        readonly List<Number> numbers = new List<Number>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        MaterialPropertyBlock flashBlock;
        readonly AudioSource[] voices = new AudioSource[6];
        readonly List<Sprite> ownedSprites = new List<Sprite>();
        Camera gameCamera;
        Material spriteMaterial;
        Sprite solid;
        Texture2D solidTexture;
        CombatSimulation simulation;
        ExpeditionPreparation preparation = new ExpeditionPreparation();
        readonly StageFlow stage = new StageFlow(new System.Random(Guid.NewGuid().GetHashCode()));
        readonly StageProgress stageProgress = new StageProgress();
        StageDefinition selectedMapStage;
        bool labMode;
        Texture2D characterSheet, routeMap, mapBadge;
        Sprite golemSprite, forestEnemySprite;
        int selectedRecipient;
        UnitView dragging;
        Vector2 dragOrigin, dragOffset;
        Rect viewport;
        float accumulator, speed = 1f;
        bool running, paused, muted, suspended, lowFrameRate;
        GUIStyle titleStyle, labelStyle, smallStyle, buttonStyle, prepButtonStyle, numberStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<BattlePrototype>() != null) return;
            var root = new GameObject("Expedition Combat Prototype");
            DontDestroyOnLoad(root);
            root.AddComponent<BattlePrototype>();
        }

        void Awake()
        {
            LoadStageRecords();
            flashBlock = new MaterialPropertyBlock();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            var cameraObject = new GameObject("Combat Camera");
            cameraObject.transform.SetParent(transform);
            gameCamera = cameraObject.AddComponent<Camera>();
            gameCamera.orthographic = true;
            gameCamera.orthographicSize = 4.5f;
            gameCamera.transform.position = new Vector3(0, 0, -10);
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = new Color(.025f, .035f, .04f);
            cameraObject.AddComponent<AudioListener>();
            Shader shader = Shader.Find("Expedition/HitFlash") ?? Shader.Find("Sprites/Default");
            spriteMaterial = new Material(shader);
            solidTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            solidTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            solidTexture.filterMode = FilterMode.Point;
            solidTexture.Apply();
            solid = MakeSprite(solidTexture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 2);
            mapBadge = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            mapBadge.filterMode = FilterMode.Point;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = x - 31.5f, dy = y - 31.5f;
                    mapBadge.SetPixel(x, y, dx * dx + dy * dy <= 31f * 31f ? Color.white : Color.clear);
                }
            mapBadge.Apply();
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0;
                voices[i].volume = .25f;
            }
            foreach (string name in new[] { "melee_hit", "arrow_launch", "arrow_hit", "defeat", "ui_confirm" })
                clips[name] = Resources.Load<AudioClip>("Audio/" + name);
            var background = Resources.Load<Texture2D>("Art/forest-battlefield");
            if (background != null)
            {
                var renderer = NewRenderer("Forest", MakeSprite(background,
                    new Rect(0, 0, background.width, background.height), new Vector2(.5f, .5f), background.width / 16f), -100);
                renderer.transform.localScale = new Vector3(1, 9f / renderer.sprite.bounds.size.y, 1);
                renderer.color = new Color(.72f, .77f, .72f);
            }
            characterSheet = Resources.Load<Texture2D>("Art/expedition-sprites-v2");
            routeMap = Resources.Load<Texture2D>("Art/forest-route-map-v1");
            AddUnit("knight", new Vector2(-2.9f, -.8f), characterSheet, new Rect(493, 641, 302, 423), 1.5f, Color.white);
            AddUnit("priest", new Vector2(-4.7f, -1.8f), characterSheet, new Rect(64, 635, 340, 455), 1.5f, new Color(.9f, 1f, 1f));
            // The lantern silhouette is a clearly labelled temporary mage proxy until matching art exists.
            AddUnit("mage", new Vector2(-5.7f, -1f), characterSheet, new Rect(64, 635, 340, 455), 1.5f, new Color(1f, .68f, .42f));
            AddUnit("golem", new Vector2(4.1f, -.7f), characterSheet, new Rect(63, 113, 333, 395), 1.8f, Color.white);
            golemSprite = views["golem"].Renderer.sprite;
            if (characterSheet != null)
                forestEnemySprite = MakeSprite(characterSheet, new Rect(892, 124, 300, 353), new Vector2(.5f, 0), 353f / 1.7f);
            FitViewport();
        }

        void LoadStageRecords()
        {
            foreach (StageDefinition definition in StageCatalog.All)
            {
                int legacyScore = definition.Id == "1-1" ? PlayerPrefs.GetInt("Ember.Stage11.BestScore", 0) : 0;
                int legacyStars = definition.Id == "1-1" ? PlayerPrefs.GetInt("Ember.Stage11.BestStars", 0) : 0;
                int score = PlayerPrefs.GetInt("Ember.Stage." + definition.Id + ".BestScore", legacyScore);
                int stars = PlayerPrefs.GetInt("Ember.Stage." + definition.Id + ".BestStars", legacyStars);
                if (stars > 0) stageProgress.RecordFor(definition).RecordClear(Mathf.Clamp(score, 0, 100), Mathf.Clamp(stars, 1, 3));
            }
        }

        Sprite MakeSprite(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit)
        {
            var sprite = Sprite.Create(texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            ownedSprites.Add(sprite);
            return sprite;
        }

        SpriteRenderer NewRenderer(string name, Sprite sprite, int order)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = spriteMaterial;
            renderer.sortingOrder = order;
            return renderer;
        }

        void AddUnit(string id, Vector2 position, Texture2D sheet, Rect crop, float height, Color fallback)
        {
            Sprite sprite = sheet != null ? MakeSprite(sheet, crop, new Vector2(.5f, 0), crop.height / height) : solid;
            var renderer = NewRenderer(id, sprite, 10);
            renderer.color = fallback;
            // The sheet's golem faces right, so its initial view is mirrored toward the allies.
            renderer.flipX = id == "golem";
            if (sheet == null) renderer.transform.localScale = new Vector3(.6f, height, 1);
            renderer.transform.position = position;
            views.Add(id, new UnitView { Id = id, Position = position, StartPosition = position, Renderer = renderer, Height = height, Tint = fallback });
        }

        void FitViewport()
        {
            Rect safe = Screen.safeArea;
            float width = Mathf.Min(safe.width, safe.height * 16f / 9f), height = width * 9f / 16f;
            viewport = new Rect(safe.x + (safe.width - width) / 2, safe.y + (safe.height - height) / 2, width, height);
            gameCamera.pixelRect = viewport;
        }

        void StartBattle()
        {
            if (!labMode && !stage.StartBattle()) return;
            dragging = null;
            var units = new List<CombatUnit>();
            foreach (var view in views.Values)
            {
                view.StartPosition = view.Position;
                view.Unit = new CombatUnit(DefinitionFor(view), view.Position.x, view.Position.y);
                units.Add(view.Unit);
            }
            simulation = new CombatSimulation(units);
            simulation.Event += OnCombatEvent;
            preparation.Commit();
            running = true; paused = false; accumulator = 0;
            PlaySound("ui_confirm");
        }

        UnitDefinition DefinitionFor(UnitView view)
        {
            bool enemy = view.Id == "golem";
            UnitRole role = view.Id == "knight" ? UnitRole.ShieldKnight :
                view.Id == "priest" ? UnitRole.LanternPriest :
                view.Id == "mage" ? UnitRole.FireMage :
                enemy ? (labMode ? UnitRole.GateGolem : UnitRole.Ordinary) : RecruitRole();
            bool scout = view.Id == "recruit" && stage.Recruited == RecruitKind.ScoutArcher;
            bool ranged = role == UnitRole.LanternPriest || role == UnitRole.FireMage || scout;
            bool evolvedKnight = view.Id == "knight" && preparation.Evolution != KnightEvolution.None;
            WaveDefinition wave = enemy && !labMode ? stage.Definition.Waves[stage.BattleNumber - 1] : null;
            float hp = enemy ? (labMode ? 1200f : wave.EnemyHp) :
                role == UnitRole.ShieldKnight ? (evolvedKnight ? 350f : 320f) :
                role == UnitRole.LanternPriest ? 165f : scout ? 125f : 150f;
            float damage = enemy ? (labMode ? 46f : wave.EnemyDamage) :
                role == UnitRole.ShieldKnight ? (view.Id == "knight" && preparation.Evolution == KnightEvolution.CounterKnight ? 30f :
                    evolvedKnight ? 28f : 26f) : role == UnitRole.LanternPriest ? 9f : scout ? 20f : 23f;
            return new UnitDefinition
            {
                Id = view.Id, Team = enemy ? Team.Enemy : Team.Ally,
                AttackKind = ranged ? AttackKind.Ranged : AttackKind.Melee,
                MaxHp = hp, Damage = damage,
                Range = ranged ? (role == UnitRole.LanternPriest ? 3.6f : 5.7f) : 1.15f,
                MoveSpeed = enemy ? (labMode ? .68f : wave.EnemySpeed) : role == UnitRole.ShieldKnight ? 1.25f : 1.05f,
                Cooldown = enemy ? (labMode ? 1.6f : wave.EnemyCooldown) : role == UnitRole.ShieldKnight ? .9f :
                    role == UnitRole.LanternPriest ? 1.7f : scout ? 1.1f : 1.25f,
                Windup = ranged ? .24f : .2f, ProjectileSpeed = 12f, Radius = enemy ? (labMode ? .7f : .48f) : .32f,
                Role = role,
                NativeTrait = role == UnitRole.ShieldKnight ? NativeTrait.Resolve :
                    role == UnitRole.LanternPriest ? NativeTrait.LanternBlessing :
                    role == UnitRole.FireMage ? NativeTrait.Ember : NativeTrait.None,
                InheritedTrait = view.Id == "knight" ? preparation.InheritedTrait :
                    view.Id == "priest" ? preparation.PriestInheritedTrait :
                    view.Id == "mage" ? preparation.MageInheritedTrait : NativeTrait.None,
                Evolution = view.Id == "knight" ? preparation.Evolution : KnightEvolution.None
            };
        }

        UnitRole RecruitRole()
        {
            if (stage.Recruited == RecruitKind.ShieldKnight) return UnitRole.ShieldKnight;
            if (stage.Recruited == RecruitKind.LanternPriest) return UnitRole.LanternPriest;
            if (stage.Recruited == RecruitKind.FireMage) return UnitRole.FireMage;
            return UnitRole.Ordinary;
        }

        void ResetBattle()
        {
            if (simulation != null) simulation.Event -= OnCombatEvent;
            simulation = null; running = false; paused = false; accumulator = 0; dragging = null;
            foreach (var effect in effects) Destroy(effect.Renderer.gameObject);
            effects.Clear(); numbers.Clear();
            foreach (var view in views.Values)
            {
                view.Position = view.StartPosition;
                view.Unit = null; view.Hit = view.Punch = view.Death = 0;
                view.Renderer.color = view.Tint;
            }
            foreach (var voice in voices) voice.Stop();
        }

        void StartStageRun()
        {
            if (!stage.Begin()) return;
            selectedMapStage = null;
            labMode = false;
            ResetBattle();
            RemoveRecruit();
            preparation = new ExpeditionPreparation();
            selectedRecipient = 0;
            ConfigureStageEnemy();
        }

        void ConfigureStageEnemy()
        {
            var enemy = views["golem"];
            enemy.Renderer.sprite = forestEnemySprite != null ? forestEnemySprite : golemSprite;
            // The forest beast faces left in the sheet; the golem fallback faces right.
            enemy.Renderer.flipX = forestEnemySprite == null;
            enemy.Renderer.transform.localScale = stage.BattleNumber == 1 ? Vector3.one : new Vector3(1.12f, 1.12f, 1f);
            enemy.Tint = stage.BattleNumber == 1 ? Color.white : new Color(1f, .77f, .7f);
            enemy.Height = stage.BattleNumber == 1 ? 1.7f : 1.9f;
            enemy.Position = enemy.StartPosition = new Vector2(4.1f, -.7f);
        }

        void AddRecruit(RecruitKind kind)
        {
            RemoveRecruit();
            Rect crop = kind == RecruitKind.ShieldKnight ? new Rect(493, 641, 302, 423) :
                kind == RecruitKind.ScoutArcher ? new Rect(896, 641, 284, 393) : new Rect(64, 635, 340, 455);
            Color tint = kind == RecruitKind.FireMage ? new Color(1f, .68f, .42f) : Color.white;
            AddUnit("recruit", new Vector2(-1.4f, -2.7f), characterSheet, crop, 1.5f, tint);
        }

        void RemoveRecruit()
        {
            UnitView view;
            if (!views.TryGetValue("recruit", out view)) return;
            Destroy(view.Renderer.gameObject);
            views.Remove("recruit");
        }

        void FinishStageBattle()
        {
            if (labMode || stage.Phase != StagePhase.Battle || simulation == null || !simulation.IsFinished) return;
            int deployed = 0, survivors = 0;
            bool recruitedAlive = false;
            foreach (var view in views.Values)
            {
                if (view.Id == "golem") continue;
                deployed++;
                if (view.Unit != null && view.Unit.IsAlive) survivors++;
                if (view.Id == "recruit" && view.Unit != null && view.Unit.IsAlive) recruitedAlive = true;
            }
            bool victory = simulation.Winner == Team.Ally;
            stage.CompleteBattle(victory, survivors, deployed, recruitedAlive);
            ResetBattle();
            if (stage.Phase == StagePhase.Result && stage.Cleared)
            {
                StageRecord record = stageProgress.RecordFor(stage.Definition);
                record.RecordClear(stage.Score, stage.Stars);
                PlayerPrefs.SetInt("Ember.Stage." + stage.Definition.Id + ".BestScore", record.BestScore);
                PlayerPrefs.SetInt("Ember.Stage." + stage.Definition.Id + ".BestStars", record.BestStars);
                PlayerPrefs.Save();
            }
        }

        void ReturnToMap()
        {
            if (running) ResetBattle();
            if (!labMode) stage.ReturnMap();
            labMode = false;
            selectedMapStage = null;
            RemoveRecruit();
            preparation = new ExpeditionPreparation();
            selectedRecipient = 0;
        }

        void EnterCombatLab()
        {
            labMode = true;
            selectedMapStage = null;
            ResetBattle();
            RemoveRecruit();
            preparation = new ExpeditionPreparation();
            selectedRecipient = 0;
            var enemy = views["golem"];
            enemy.Renderer.sprite = golemSprite;
            // The golem sprite faces right in the sheet and must be mirrored toward the allies.
            enemy.Renderer.flipX = true;
            enemy.Renderer.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            enemy.Tint = new Color(.9f, .86f, .8f);
            enemy.Height = 2.7f;
            enemy.Position = enemy.StartPosition = new Vector2(4.1f, -1f);
        }

        void Update()
        {
            FitViewport();
            if (!running && !suspended && viewport.width > 0 &&
                (labMode || stage.Phase == StagePhase.Preparation)) HandlePlacement();
            float visualDt = paused || suspended ? 0 : Mathf.Min(Time.unscaledDeltaTime, .1f) * speed;
            foreach (var view in views.Values)
            {
                view.Hit = Mathf.Max(0, view.Hit - visualDt * 10);
                view.Punch = Mathf.Max(0, view.Punch - visualDt * 7);
            }
            UpdateEffects(visualDt);
            if (running && !paused && !suspended && !simulation.IsFinished)
            {
                accumulator += visualDt;
                int steps = 0;
                while (accumulator >= Tick && steps++ < 12 && !simulation.IsFinished)
                { simulation.Step(Tick); accumulator -= Tick; if (simulation.Elapsed >= 60f) simulation.EndByTimeout(); }
            }
            FinishStageBattle();
            foreach (var view in views.Values)
            {
                if (view.Unit != null) view.Position = new Vector2(view.Unit.X, view.Unit.Y);
                float direction = view.Id == "golem" ? -1 : 1;
                float anticipation = view.Unit != null ? view.Unit.AttackProgress : 0;
                Vector2 offset = new Vector2(direction * (view.Punch * .22f - anticipation * .08f - view.Hit * .1f), view.Hit * .04f);
                view.Renderer.transform.position = view.Position + offset;
                view.Renderer.sortingOrder = 30 - Mathf.RoundToInt(view.Position.y * 10);
                flashBlock.SetFloat(FlashId, view.Hit);
                view.Renderer.SetPropertyBlock(flashBlock);
                if (view.Unit != null && !view.Unit.IsAlive) view.Death = Mathf.Min(1, view.Death + visualDt * 1.8f);
                view.Renderer.color = new Color(view.Tint.r, view.Tint.g, view.Tint.b, 1 - view.Death);
            }
        }

        void HandlePlacement()
        {
            bool down, held, up, cancel = false;
            Vector2 screen;
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0); screen = touch.position;
                down = touch.phase == TouchPhase.Began;
                up = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                held = !up; cancel = touch.phase == TouchPhase.Canceled;
            }
            else { screen = Input.mousePosition; down = Input.GetMouseButtonDown(0); held = Input.GetMouseButton(0); up = Input.GetMouseButtonUp(0); }
            Vector2 ui = ScreenToUi(screen), world = gameCamera.ScreenToWorldPoint(screen);
            if (down && viewport.Contains(screen) && ui.y > 272 && ui.y < 580)
            {
                foreach (var view in views.Values)
                    if (view.Id != "golem" && view.Renderer.bounds.Contains(new Vector3(world.x, world.y, 0)))
                    { dragging = view; dragOrigin = view.Position; dragOffset = view.Position - world; break; }
            }
            if (dragging != null && held)
            {
                Vector2 target = world + dragOffset;
                dragging.Position = new Vector2(Mathf.Clamp(target.x, -6, -1), Mathf.Clamp(target.y, -2.8f, -.2f));
            }
            if (dragging != null && up)
            {
                foreach (var other in views.Values)
                    if (other != dragging && (other.Position - dragging.Position).sqrMagnitude < .8f * .8f) cancel = true;
                if (cancel) dragging.Position = dragOrigin;
                dragging = null;
            }
        }

        void OnCombatEvent(CombatEvent e)
        {
            views.TryGetValue(e.SourceId ?? "", out UnitView source);
            views.TryGetValue(e.TargetId ?? "", out UnitView target);
            if (e.Type == CombatEventType.BossTelegraph && source != null)
            {
                float baseAngle = Mathf.Atan2(e.Y, e.X) * Mathf.Rad2Deg;
                for (int i = -2; i <= 2; i++)
                {
                    float angle = baseAngle + i * 18f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                    AddEffect(source.Position + direction * 2f, source.Position + direction * 2f,
                        new Vector2(4f, i == 0 ? .25f : .12f), new Color(1f, .12f, .08f, .8f), .8f, angle: angle);
                }
            }
            if (e.Type == CombatEventType.BossSlam && source != null)
                AddEffect(source.Position + Vector2.left * 1.4f, source.Position + Vector2.left * 1.4f,
                    new Vector2(3.5f, .28f), new Color(1f, .35f, .12f), .3f);
            if (e.Type == CombatEventType.ShieldApplied && target != null)
                AddEffect(target.Position + Vector2.up * .65f, target.Position + Vector2.up * .65f,
                    new Vector2(1.2f, 1.4f), new Color(.3f, .9f, 1f, .65f), .42f);
            if (e.Type == CombatEventType.ShieldBroken && target != null)
                AddEffect(target.Position + Vector2.up * .65f, target.Position + Vector2.up * .65f,
                    new Vector2(1.5f, .14f), new Color(.6f, 1f, 1f), .2f, angle: 35f);
            if (e.Type == CombatEventType.Counterattack && source != null)
                AddEffect(source.Position + Vector2.right * .8f, source.Position + Vector2.right * 1.2f,
                    new Vector2(2.2f, .3f), new Color(.4f, .9f, 1f), .28f);
            if (e.Type == CombatEventType.BurnApplied && target != null)
                AddEffect(target.Position + Vector2.up * .35f, target.Position + Vector2.up * 1.2f,
                    new Vector2(.28f, .48f), new Color(1f, .4f, .08f), .45f);
            if (e.Type == CombatEventType.ProjectileLaunched && source != null && target != null)
            {
                AddEffect(new Vector2(e.X, e.Y + .8f), target.Position + Vector2.up * .7f,
                    new Vector2(.38f, .055f), new Color(1, .9f, .5f), 2, true, e.SourceId, e.TargetId);
                source.Punch = 1; PlaySound("arrow_launch");
            }
            if (e.Type == CombatEventType.Hit && target != null)
            {
                target.Hit = 1;
                if (source != null) source.Punch = 1;
                bool ranged = source != null && source.Unit.Definition.AttackKind == AttackKind.Ranged;
                PlaySound(ranged ? "arrow_hit" : "melee_hit");
                Vector2 impact = new Vector2(e.X, e.Y + .7f);
                AddEffect(impact, impact, new Vector2(ranged ? .6f : 1.1f, .07f), new Color(1, .94f, .65f), .12f, angle: ranged ? 0 : 40);
                AddEffect(impact, impact, new Vector2(.07f, .8f), Color.white, .1f);
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.PI * .4f;
                    AddEffect(impact, impact + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .55f,
                        new Vector2(.06f, .06f), new Color(1, .78f, .3f), .22f);
                }
                if (numbers.Count < 24) numbers.Add(new Number { Position = impact, Damage = (int)e.Damage, AlliedTarget = target.Id != "golem" });
                for (int i = effects.Count - 1; i >= 0; i--)
                    if (effects[i].Projectile && effects[i].Source == e.SourceId && effects[i].Target == e.TargetId)
                    { Destroy(effects[i].Renderer.gameObject); effects.RemoveAt(i); break; }
            }
            if (e.Type == CombatEventType.Defeated) PlaySound("defeat");
        }

        void AddEffect(Vector2 start, Vector2 end, Vector2 size, Color tint, float duration,
            bool projectile = false, string source = null, string target = null, float angle = 0)
        {
            if (effects.Count >= 64) return;
            var renderer = NewRenderer("Combat Effect", solid, 100);
            renderer.transform.position = start;
            renderer.transform.localScale = new Vector3(size.x, size.y, 1);
            renderer.color = tint;
            renderer.transform.rotation = Quaternion.Euler(0, 0, angle);
            if (projectile) renderer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg);
            effects.Add(new Effect { Renderer = renderer, Start = start, End = end,
                Size = size, Tint = tint, Duration = duration, Projectile = projectile, Source = source, Target = target });
        }

        void UpdateEffects(float dt)
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i]; effect.Age += dt;
                if (effect.Age >= effect.Duration) { Destroy(effect.Renderer.gameObject); effects.RemoveAt(i); continue; }
                float progress = effect.Age / effect.Duration;
                if (effect.Projectile)
                {
                    if (views.TryGetValue(effect.Target, out UnitView target)) effect.End = target.Position + Vector2.up * .7f;
                    effect.Start = Vector2.MoveTowards(effect.Start, effect.End, 12 * dt);
                    effect.Renderer.transform.position = effect.Start;
                    Vector2 direction = effect.End - effect.Start;
                    effect.Renderer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                }
                else effect.Renderer.transform.position = Vector2.Lerp(effect.Start, effect.End, progress);
                effect.Renderer.transform.localScale = new Vector3(effect.Size.x, effect.Size.y, 1);
                effect.Renderer.color = new Color(effect.Tint.r, effect.Tint.g, effect.Tint.b, effect.Projectile ? 1 : 1 - progress);
            }
            for (int i = numbers.Count - 1; i >= 0; i--)
            { numbers[i].Age += dt; if (numbers[i].Age > .65f) numbers.RemoveAt(i); }
        }

        void PlaySound(string name)
        {
            if (muted || suspended || !clips.TryGetValue(name, out AudioClip clip) || clip == null) return;
            foreach (var voice in voices) if (!voice.isPlaying) { voice.clip = clip; voice.Play(); return; }
        }

        void SetPaused(bool value)
        {
            paused = value;
            foreach (var voice in voices) { if (value) voice.Pause(); else if (!muted && !suspended) voice.UnPause(); }
        }

        void OnApplicationPause(bool value)
        {
            suspended = value;
            if (value) { SetPaused(true); if (dragging != null) { dragging.Position = dragOrigin; dragging = null; } }
        }

        Vector2 ScreenToUi(Vector2 screen) => new Vector2((screen.x - viewport.x) / viewport.width * UiWidth,
            (viewport.yMax - screen.y) / viewport.height * UiHeight);

        void OnGUI()
        {
            if (gameCamera == null || viewport.width <= 0) return;
            float top = Screen.height - viewport.yMax;
            Fill(new Rect(0, 0, Screen.width, top), Color.black);
            Fill(new Rect(0, top + viewport.height, Screen.width, Screen.height), Color.black);
            Fill(new Rect(0, top, viewport.x, viewport.height), Color.black);
            Fill(new Rect(viewport.xMax, top, Screen.width - viewport.xMax, viewport.height), Color.black);
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 23 };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 23, fontStyle = FontStyle.Bold };
                prepButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                numberStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            GUI.matrix = Matrix4x4.TRS(new Vector3(viewport.x, Screen.height - viewport.yMax, 0), Quaternion.identity,
                new Vector3(viewport.width / UiWidth, viewport.height / UiHeight, 1));
            if (!labMode && stage.Phase != StagePhase.Preparation && stage.Phase != StagePhase.Battle)
            {
                DrawStageScreen();
                GUI.matrix = Matrix4x4.identity;
                return;
            }
            Fill(new Rect(0, 0, UiWidth, 88), new Color(.035f, .08f, .075f, .95f));
            Fill(new Rect(0, 588, UiWidth, 132), new Color(.035f, .08f, .075f, .96f));
            GUI.Label(new Rect(26, 10, 650, 40), labMode ? "EMBER EXPEDITION / COMBAT LAB" :
                stage.Definition.Id + " " + stage.Definition.Name + " / WAVE " + stage.BattleNumber +
                "/" + stage.Definition.Waves.Length, titleStyle);
            GUI.Label(new Rect(28, 51, 850, 30), running ? "BATTLE  /  " + simulation.Elapsed.ToString("0.0") + "s  /  60s LIMIT" :
                "PREPARE  /  TRAIN " + preparation.Progress + "/2  /  DRAG ALLIES IN THE LEFT ZONE", smallStyle);
            if (labMode)
            {
                GUI.enabled = !running || simulation.IsFinished;
                if (GUI.Button(new Rect(680, 17, 96, 55), "MAP", prepButtonStyle)) ReturnToMap();
                GUI.enabled = true;
            }
            GUI.enabled = !running || (labMode && simulation.IsFinished);
            if (GUI.Button(new Rect(788, 17, 170, 55), labMode ? "NEW RUN" : "MAP", buttonStyle))
            {
                if (labMode)
                { if (running) ResetBattle(); preparation = new ExpeditionPreparation(); selectedRecipient = 0; }
                else ReturnToMap();
            }
            GUI.enabled = true;
            if (GUI.Button(new Rect(970, 17, 130, 55), lowFrameRate ? "30 FPS" : "60 FPS", buttonStyle))
            { lowFrameRate = !lowFrameRate; Application.targetFrameRate = lowFrameRate ? 30 : 60; }
            if (GUI.Button(new Rect(1116, 17, 138, 55), muted ? "MUTED" : "SOUND ON", buttonStyle))
            { muted = !muted; foreach (var voice in voices) voice.mute = muted; }
            if (!running)
            {
                Fill(new Rect(18, 96, 1244, 174), new Color(.025f, .07f, .065f, .93f));
                GUI.Label(new Rect(28, 98, 1210, 28), "TRAIN KNIGHT +1: choose a reserve to consume. PASS: choose recipient, then donor. UNDO before battle.", smallStyle);
                string[] donor = { "KNIGHT", "PRIEST", "FIRE MAGE" };
                string[] recipient = { "KNIGHT", "PRIEST", "MAGE" };
                if (GUI.Button(new Rect(28, 130, 235, 45), "PASS TO " + recipient[selectedRecipient], prepButtonStyle))
                    selectedRecipient = (selectedRecipient + 1) % 3;
                for (int i = 0; i < 3; i++)
                {
                    GUI.enabled = preparation.IsReserveAvailable(i) && preparation.Progress < 2 && preparation.Evolution == KnightEvolution.None;
                    if (GUI.Button(new Rect(272 + i * 258, 130, 250, 45), "USE " + donor[i], prepButtonStyle)) preparation.Train(i);
                }
                for (int i = 0; i < 3; i++)
                {
                    GUI.enabled = preparation.IsReserveAvailable(i) && i != selectedRecipient &&
                        preparation.InheritedTraitFor(selectedRecipient) != preparation.ReserveTrait(i);
                    if (GUI.Button(new Rect(28 + i * 199, 181, 192, 49), "PASS " + (i == 2 ? "MAGE" : donor[i]), prepButtonStyle))
                        preparation.Inherit(i, selectedRecipient);
                }
                GUI.enabled = preparation.Progress == 2 && preparation.Evolution == KnightEvolution.None;
                if (GUI.Button(new Rect(625, 181, 210, 49), "COUNTER", prepButtonStyle)) preparation.Evolve(KnightEvolution.CounterKnight);
                if (GUI.Button(new Rect(843, 181, 210, 49), "GUARDIAN", prepButtonStyle)) preparation.Evolve(KnightEvolution.GuardianKnight);
                GUI.enabled = preparation.CanUndo;
                if (GUI.Button(new Rect(1061, 181, 185, 49), "UNDO", prepButtonStyle)) preparation.Undo();
                GUI.enabled = true;
                string available = "Available: ";
                for (int i = 0; i < 3; i++) available += donor[i] + (preparation.IsReserveAvailable(i) ? " ready" : " used") + (i < 2 ? "  /  " : "");
                GUI.Label(new Rect(28, 235, 1210, 30), available + "   |   " + recipient[selectedRecipient] +
                    " inherited: " + preparation.InheritedTraitFor(selectedRecipient).ToString().ToUpperInvariant(), smallStyle);
                Fill(new Rect(128, 392, 442, 192), new Color(.5f, .95f, .65f, .06f));
                Fill(new Rect(128, 392, 442, 2), new Color(.6f, .9f, .6f, .5f));
                GUI.Label(new Rect(26, 549, 600, 30), "ALLIED DEPLOYMENT ZONE", smallStyle);
            }
            foreach (var view in views.Values)
            {
                if (view.Death >= 1) continue;
                Vector2 point = ScreenToUi(gameCamera.WorldToScreenPoint(view.Position + Vector2.up * (view.Height + .16f)));
                if (!running && point.y < 288f)
                    point.y = Mathf.Max(288f, ScreenToUi(gameCamera.WorldToScreenPoint(view.Position)).y + 10f);
                float health = view.Unit == null ? 1 : Mathf.Clamp01((float)view.Unit.Hp / view.Unit.Definition.MaxHp);
                Fill(new Rect(point.x - 46, point.y, 92, 8), new Color(.03f, .04f, .03f, .85f));
                Fill(new Rect(point.x - 44, point.y + 2, 88 * health, 4), view.Id == "golem" ? new Color(.95f, .4f, .3f) : new Color(.6f, .9f, .5f));
                if (view.Unit != null && view.Unit.Shield > 0f)
                    Fill(new Rect(point.x - 44, point.y + 10, 88 * Mathf.Clamp01(view.Unit.Shield / view.Unit.Definition.MaxHp), 4), new Color(.3f, .9f, 1f));
                if (!running)
                {
                    string name = DisplayName(view);
                    GUI.Label(view.Id == "recruit" ? new Rect(point.x - 46, point.y - 27, 150, 28) :
                        new Rect(point.x - 115, point.y - 27, 230, 28), name, smallStyle);
                }
            }
            foreach (var number in numbers)
            {
                Vector2 point = ScreenToUi(gameCamera.WorldToScreenPoint(number.Position + Vector2.up * number.Age * 1.1f));
                GUI.color = new Color(0, 0, 0, 1 - number.Age / .65f);
                GUI.Label(new Rect(point.x - 39, point.y - 39, 80, 42), number.Damage.ToString(), numberStyle);
                GUI.color = number.AlliedTarget ? new Color(1, .6f, .45f, GUI.color.a) : new Color(1, .96f, .65f, GUI.color.a);
                GUI.Label(new Rect(point.x - 41, point.y - 41, 80, 42), number.Damage.ToString(), numberStyle);
            }
            GUI.color = Color.white;
            string status = !running ? "Knight: " + (preparation.Evolution == KnightEvolution.None ? "BASE" : preparation.Evolution.ToString().ToUpperInvariant()) +
                "  /  inherited: " + preparation.InheritedTrait.ToString().ToUpperInvariant() : simulation.IsFinished ?
                (simulation.Winner == Team.Ally ? "VICTORY - retry with a new formation." : "DEFEAT - adjust formation and retry.") : paused ?
                "PAUSED - tap RESUME." : labMode ? "Red lines lock the golem's slam direction for 0.8s." :
                "Defeat the forest enemy. The recruited ally joins wave two.";
            GUI.Label(new Rect(28, 607, 710, 40), status, labelStyle);
            GUI.Label(new Rect(28, 652, 730, 36), "COUNTER: cone + slow on shield break  /  GUARDIAN: ally shield every 5s", smallStyle);
            if (GUI.Button(new Rect(776, 611, 105, 76), speed == 1 ? "1x" : "2x", buttonStyle)) speed = speed == 1 ? 2 : 1;
            GUI.enabled = running && !simulation.IsFinished;
            if (GUI.Button(new Rect(895, 611, 154, 76), paused ? "RESUME" : "PAUSE", buttonStyle)) SetPaused(!paused);
            GUI.enabled = true;
            GUI.enabled = !running || labMode;
            if (GUI.Button(new Rect(1063, 611, 191, 76), running ? "RETRY" : "FIGHT", buttonStyle))
            { if (running) ResetBattle(); else StartBattle(); }
            GUI.enabled = true;
            GUI.matrix = Matrix4x4.identity;
        }

        string DisplayName(UnitView view)
        {
            if (view.Id == "golem") return labMode ? "GATE GOLEM (ART TEMP)" :
                stage.Definition.Waves[stage.BattleNumber - 1].Name;
            if (view.Id == "recruit") return stage.Recruited == RecruitKind.ScoutArcher ? "NEW ARCHER" :
                stage.Recruited == RecruitKind.ShieldKnight ? "NEW KNIGHT" :
                stage.Recruited == RecruitKind.LanternPriest ? "NEW PRIEST" : "NEW MAGE";
            if (view.Id == "mage") return "FIRE MAGE (ART TEMP)";
            if (view.Id == "priest") return "LANTERN PRIEST";
            return preparation.Evolution == KnightEvolution.CounterKnight ? "COUNTER KNIGHT" :
                preparation.Evolution == KnightEvolution.GuardianKnight ? "GUARDIAN KNIGHT" : "SHIELD KNIGHT";
        }

        static string RecruitName(RecruitKind kind)
        {
            if (kind == RecruitKind.ShieldKnight) return "SHIELD KNIGHT";
            if (kind == RecruitKind.LanternPriest) return "LANTERN PRIEST";
            if (kind == RecruitKind.FireMage) return "FIRE MAGE";
            return "SCOUT ARCHER";
        }

        static string RecruitDescription(RecruitKind kind)
        {
            if (kind == RecruitKind.ShieldKnight) return "Front line / starting shield";
            if (kind == RecruitKind.LanternPriest) return "Support / periodic shield";
            if (kind == RecruitKind.FireMage) return "Ranged / burning attacks";
            return "Ranged / focused arrows";
        }

        void DrawMapNode(StageDefinition definition, Vector2 center)
        {
            bool unlocked = stageProgress.IsUnlocked(definition);
            StageRecord record = stageProgress.RecordFor(definition);
            Color old = GUI.color;
            GUI.color = unlocked ? definition.Playable ? new Color(.97f, .76f, .34f) :
                new Color(.77f, .66f, .38f) : new Color(.42f, .5f, .47f);
            GUI.DrawTexture(new Rect(center.x - 31, center.y - 31, 62, 62), mapBadge);
            GUI.color = new Color(.04f, .105f, .09f);
            GUI.DrawTexture(new Rect(center.x - 25, center.y - 25, 50, 50), mapBadge);
            GUI.color = old;
            GUI.Label(new Rect(center.x - 35, center.y - 18, 70, 36), definition.Id, numberStyle);
            string stars = new string('★', record.BestStars) + new string('☆', 3 - record.BestStars);
            Fill(new Rect(center.x - 38, center.y + 33, 76, 24), new Color(.02f, .06f, .05f, .8f));
            GUI.Label(new Rect(center.x - 35, center.y + 32, 74, 27), stars, smallStyle);
            if (selectedMapStage == null && GUI.Button(new Rect(center.x - 42, center.y - 40, 84, 100),
                GUIContent.none, GUIStyle.none)) selectedMapStage = definition;
        }

        void DrawMapRoute()
        {
            Vector2[] road = { new Vector2(195, 550), new Vector2(350, 535), new Vector2(510, 470),
                new Vector2(635, 410), new Vector2(800, 390), new Vector2(950, 305), new Vector2(1090, 220) };
            Color old = GUI.color;
            GUI.color = new Color(.98f, .78f, .36f, .82f);
            for (int segment = 0; segment < road.Length - 1; segment++)
            {
                float distance = Vector2.Distance(road[segment], road[segment + 1]);
                int count = Mathf.CeilToInt(distance / 28f);
                for (int i = 1; i < count; i++)
                {
                    Vector2 point = Vector2.Lerp(road[segment], road[segment + 1], i / (float)count);
                    GUI.DrawTexture(new Rect(point.x - 3, point.y - 3, 6, 6), mapBadge);
                }
            }
            GUI.color = old;
        }

        void DrawMapDialog()
        {
            StageDefinition definition = selectedMapStage;
            bool unlocked = stageProgress.IsUnlocked(definition);
            StageRecord record = stageProgress.RecordFor(definition);
            Fill(new Rect(0, 0, UiWidth, UiHeight), new Color(0, 0, 0, .64f));
            Fill(new Rect(260, 143, 760, 440), new Color(.035f, .09f, .075f, .98f));
            Fill(new Rect(260, 143, 760, 5), new Color(.9f, .68f, .31f));
            GUI.Label(new Rect(302, 180, 655, 48), definition.Id + "  " + definition.Name, titleStyle);
            GUI.Label(new Rect(303, 257, 650, 37), unlocked ? definition.Playable ?
                "READY  /  " + definition.Waves.Length + " BATTLES" : "ROUTE OPEN  /  COMING NEXT" :
                "LOCKED  /  CLEAR " + definition.PrerequisiteId + " FIRST", labelStyle);
            GUI.Label(new Rect(303, 315, 650, 37), definition.Objective == StageObjective.RecruitSurvives ?
                "GOAL  /  BRING THE RECRUIT HOME" : definition.Objective == StageObjective.HoldOut ?
                "GOAL  /  HOLD THE OUTPOST" : "GOAL  /  DEFEAT THE GATE GOLEM", labelStyle);
            GUI.Label(new Rect(303, 373, 650, 37), "BEST  " + record.BestScore + "/100    " + new string('★', record.BestStars), labelStyle);
            GUI.Label(new Rect(303, 423, 650, 36), definition.Playable ?
                "Survive both waves with your new ally for three stars." :
                "This stage is planned for the next playable update.", smallStyle);
            if (GUI.Button(new Rect(301, 500, 232, 58), "CLOSE", buttonStyle)) selectedMapStage = null;
            GUI.enabled = unlocked && definition.Playable;
            if (GUI.Button(new Rect(746, 500, 232, 58), "START STAGE", buttonStyle))
            {
                if (stage.OpenBriefing(definition)) StartStageRun();
            }
            GUI.enabled = true;
        }

        void DrawStageScreen()
        {
            if (stage.Phase == StagePhase.Map)
            {
                if (routeMap != null) GUI.DrawTexture(new Rect(0, 0, UiWidth, UiHeight), routeMap, ScaleMode.ScaleAndCrop);
                else
                {
                    Fill(new Rect(0, 0, UiWidth, UiHeight), new Color(.07f, .18f, .14f));
                    Fill(new Rect(90, 513, 480, 24), new Color(.57f, .43f, .23f));
                    Fill(new Rect(550, 330, 400, 24), new Color(.57f, .43f, .23f));
                }
                Fill(new Rect(0, 0, UiWidth, 84), new Color(.018f, .055f, .05f, .88f));
                GUI.Label(new Rect(35, 13, 1000, 42), "EMBER EXPEDITION  /  ASHEN BEACON FOREST", titleStyle);
                GUI.Label(new Rect(38, 52, 1000, 27), "Follow the winding road. Clear 1-1 to open the outpost.", smallStyle);
                DrawMapRoute();
                DrawMapNode(StageCatalog.ForestEntrance, new Vector2(195, 550));
                DrawMapNode(StageCatalog.RuinedOutpost, new Vector2(635, 410));
                DrawMapNode(StageCatalog.CastleGate, new Vector2(1090, 220));
                Fill(new Rect(0, 629, UiWidth, 91), new Color(.018f, .055f, .05f, .88f));
                GUI.enabled = selectedMapStage == null;
                if (GUI.Button(new Rect(36, 642, 252, 65), "COMBAT LAB", buttonStyle)) EnterCombatLab();
                GUI.enabled = true;
                GUI.Label(new Rect(311, 654, 930, 40), "Gate Golem challenge  /  training and evolution sandbox", smallStyle);
                if (selectedMapStage != null) DrawMapDialog();
            }
            else
            {
            Fill(new Rect(0, 0, UiWidth, UiHeight), new Color(.018f, .045f, .043f, .98f));
            Fill(new Rect(0, 0, UiWidth, 92), new Color(.04f, .1f, .09f));
            GUI.Label(new Rect(46, 23, 850, 46), "EMBER EXPEDITION  /  ASHEN BEACON FOREST", titleStyle);
            if (stage.Phase == StagePhase.Briefing)
            {
                StageRecord record = stageProgress.RecordFor(stage.Definition);
                Fill(new Rect(72, 125, 1136, 448), new Color(.075f, .14f, .12f));
                GUI.Label(new Rect(112, 155, 1050, 54), stage.Definition.Id + "  " + stage.Definition.Name, titleStyle);
                GUI.Label(new Rect(112, 227, 1020, 38), stage.Definition.Waves.Length + " battles  /  forest beast, then a tougher elite", labelStyle);
                GUI.Label(new Rect(112, 278, 1020, 38), "After battle one: choose 1 of 3 random recruits for your fourth slot.", labelStyle);
                GUI.Label(new Rect(112, 329, 1020, 38), "Objective: bring the recruited ally home. Keep everyone alive for three stars.", labelStyle);
                GUI.Label(new Rect(112, 380, 1020, 38), "Score: clear 65 + wave 1 survival 10 + wave 2 survival 15 + recruit survives 10", smallStyle);
                GUI.Label(new Rect(112, 451, 1020, 38), "Best: " + record.BestScore + " / 100     Stars: " + new string('★', record.BestStars), labelStyle);
                if (GUI.Button(new Rect(715, 594, 240, 75), "BACK TO MAP", buttonStyle)) ReturnToMap();
                if (GUI.Button(new Rect(971, 594, 240, 75), "START STAGE", buttonStyle)) StartStageRun();
            }
            else if (stage.Phase == StagePhase.Recruitment)
            {
                GUI.Label(new Rect(47, 116, 1120, 47), "WAVE 1 CLEAR  /  RECRUIT ONE ALLY", titleStyle);
                GUI.Label(new Rect(48, 165, 1150, 40), "Three candidates are drawn for this run. Your choice joins the second battle as a deployable ally.", smallStyle);
                for (int i = 0; i < stage.Candidates.Count; i++)
                {
                    RecruitKind kind = stage.Candidates[i];
                    float x = 53 + i * 408;
                    Fill(new Rect(x, 245, 370, 240), new Color(.08f, .16f, .14f));
                    GUI.Label(new Rect(x + 20, 275, 330, 45), RecruitName(kind), titleStyle);
                    GUI.Label(new Rect(x + 20, 339, 330, 50), RecruitDescription(kind), smallStyle);
                    if (GUI.Button(new Rect(x + 20, 412, 330, 62), "RECRUIT", buttonStyle))
                    {
                        stage.ChooseRecruit(i);
                        AddRecruit(kind);
                        ConfigureStageEnemy();
                        PlaySound("ui_confirm");
                    }
                }
                GUI.Label(new Rect(56, 557, 1150, 42), "All allies recover before wave two. Drag the new ally in preparation.", labelStyle);
            }
            else if (stage.Phase == StagePhase.Result)
            {
                StageRecord record = stageProgress.RecordFor(stage.Definition);
                Fill(new Rect(82, 120, 1116, 480), new Color(.075f, .14f, .12f));
                GUI.Label(new Rect(118, 154, 1020, 52), stage.Cleared ? "STAGE CLEAR  /  1-1 FOREST ENTRANCE" :
                    "EXPEDITION FAILED  /  1-1 FOREST ENTRANCE", titleStyle);
                GUI.Label(new Rect(118, 224, 1030, 42), "This run: " + stage.Score + " / 100    " + new string('★', stage.Stars), labelStyle);
                GUI.Label(new Rect(118, 285, 1030, 42), stage.Cleared ?
                    "Clear 65 + wave 1 " + stage.FirstSurvivalBonus + " + wave 2 " + stage.FinalSurvivalBonus +
                    " + recruit " + stage.ObjectiveBonus :
                    "No score or stars on defeat. Choose 1-1 again from the map.", labelStyle);
                GUI.Label(new Rect(118, 344, 1030, 42), "Survivors: wave 1 " + stage.FirstSurvivors + "/" + stage.FirstDeployed +
                    "    final " + stage.LastSurvivors + "/" + stage.LastDeployed, labelStyle);
                GUI.Label(new Rect(118, 407, 1030, 42), "Best record: " + record.BestScore + " / 100    " + new string('★', record.BestStars), labelStyle);
                GUI.Label(new Rect(118, 469, 1030, 42), stage.Cleared ? "1-2 path opened on the map." :
                    "A new attempt draws a fresh recruit offer.", labelStyle);
                if (GUI.Button(new Rect(940, 613, 276, 74), "BACK TO MAP", buttonStyle)) ReturnToMap();
            }
            }
        }

        static void Fill(Rect rect, Color color)
        { Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous; }

        void OnDestroy()
        {
            if (simulation != null) simulation.Event -= OnCombatEvent;
            foreach (var sprite in ownedSprites) Destroy(sprite);
            if (spriteMaterial != null) Destroy(spriteMaterial);
            if (solidTexture != null) Destroy(solidTexture);
            if (mapBadge != null) Destroy(mapBadge);
        }
    }
}
