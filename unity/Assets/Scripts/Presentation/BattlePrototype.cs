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
            var sheet = Resources.Load<Texture2D>("Art/expedition-sprites-v2");
            AddUnit("knight", new Vector2(-2.9f, -.8f), sheet, new Rect(493, 641, 302, 423), 1.5f, Color.white);
            AddUnit("priest", new Vector2(-4.7f, -1.8f), sheet, new Rect(64, 635, 340, 455), 1.5f, new Color(.9f, 1f, 1f));
            // The lantern silhouette is a clearly labelled temporary mage proxy until matching art exists.
            AddUnit("mage", new Vector2(-5.7f, -1f), sheet, new Rect(64, 635, 340, 455), 1.5f, new Color(1f, .68f, .42f));
            AddUnit("golem", new Vector2(4.1f, -.7f), sheet, new Rect(63, 113, 333, 395), 1.8f, Color.white);
            FitViewport();
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
            // Enemies enter from the right and face the allied deployment zone.
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
            dragging = null;
            var units = new List<CombatUnit>();
            foreach (var view in views.Values)
            {
                bool enemy = view.Id == "golem", ranged = view.Id == "priest" || view.Id == "mage";
                UnitRole role = view.Id == "knight" ? UnitRole.ShieldKnight :
                    view.Id == "priest" ? UnitRole.LanternPriest : view.Id == "mage" ? UnitRole.FireMage : UnitRole.GateGolem;
                var definition = new UnitDefinition
                {
                    Id = view.Id, Team = enemy ? Team.Enemy : Team.Ally,
                    AttackKind = ranged ? AttackKind.Ranged : AttackKind.Melee,
                    MaxHp = enemy ? 650 : role == UnitRole.ShieldKnight ?
                        (preparation.Evolution == KnightEvolution.None ? 320 : 350) : role == UnitRole.LanternPriest ? 165 : 150,
                    Damage = enemy ? 25 : role == UnitRole.ShieldKnight ?
                        (preparation.Evolution == KnightEvolution.CounterKnight ? 30 :
                        preparation.Evolution == KnightEvolution.GuardianKnight ? 28 : 26) :
                        role == UnitRole.LanternPriest ? 9 : 23,
                    Range = ranged ? (role == UnitRole.LanternPriest ? 3.6f : 5.7f) : 1.15f,
                    MoveSpeed = enemy ? .65f : role == UnitRole.ShieldKnight ? 1.25f : 1.05f,
                    Cooldown = enemy ? 1.45f : role == UnitRole.ShieldKnight ? .9f : role == UnitRole.LanternPriest ? 1.7f : 1.25f,
                    Windup = ranged ? .24f : .2f, ProjectileSpeed = 12f, Radius = enemy ? .48f : .32f,
                    Role = role,
                    NativeTrait = role == UnitRole.ShieldKnight ? NativeTrait.Resolve :
                        role == UnitRole.LanternPriest ? NativeTrait.LanternBlessing : role == UnitRole.FireMage ? NativeTrait.Ember : NativeTrait.None,
                    InheritedTrait = role == UnitRole.ShieldKnight ? preparation.InheritedTrait :
                        role == UnitRole.LanternPriest ? preparation.PriestInheritedTrait :
                        role == UnitRole.FireMage ? preparation.MageInheritedTrait : NativeTrait.None,
                    Evolution = role == UnitRole.ShieldKnight ? preparation.Evolution : KnightEvolution.None
                };
                view.StartPosition = view.Position;
                view.Unit = new CombatUnit(definition, view.Position.x, view.Position.y);
                units.Add(view.Unit);
            }
            simulation = new CombatSimulation(units);
            simulation.Event += OnCombatEvent;
            preparation.Commit();
            running = true; paused = false; accumulator = 0;
            PlaySound("ui_confirm");
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

        void Update()
        {
            FitViewport();
            if (!running && !suspended && viewport.width > 0) HandlePlacement();
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
                for (int i = -1; i <= 1; i++)
                {
                    float angle = baseAngle + i * 35f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                    AddEffect(source.Position + direction * 1.55f, source.Position + direction * 1.55f,
                        new Vector2(3.1f, i == 0 ? .25f : .09f), new Color(1f, .15f, .12f, .75f), .8f, angle: angle);
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
            Fill(new Rect(0, 0, UiWidth, 88), new Color(.035f, .08f, .075f, .95f));
            Fill(new Rect(0, 588, UiWidth, 132), new Color(.035f, .08f, .075f, .96f));
            GUI.Label(new Rect(26, 10, 650, 40), "EMBER EXPEDITION / GATE GOLEM", titleStyle);
            GUI.Label(new Rect(28, 51, 850, 30), running ? "BATTLE  /  " + simulation.Elapsed.ToString("0.0") + "s  /  60s LIMIT" :
                "PREPARE  /  TRAIN " + preparation.Progress + "/2  /  DRAG ALLIES IN THE LEFT ZONE", smallStyle);
            GUI.enabled = !running || simulation.IsFinished;
            if (GUI.Button(new Rect(788, 17, 170, 55), "NEW RUN", buttonStyle))
            { if (running) ResetBattle(); preparation = new ExpeditionPreparation(); selectedRecipient = 0; }
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
                    string name = view.Id == "golem" ? "GATE GOLEM (ART TEMP)" :
                        view.Id == "mage" ? "FIRE MAGE (ART TEMP)" :
                        view.Id == "priest" ? "LANTERN PRIEST" :
                        preparation.Evolution == KnightEvolution.CounterKnight ? "COUNTER KNIGHT" :
                        preparation.Evolution == KnightEvolution.GuardianKnight ? "GUARDIAN KNIGHT" : "SHIELD KNIGHT";
                    GUI.Label(new Rect(point.x - 115, point.y - 27, 230, 28), name, smallStyle);
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
                "PAUSED - tap RESUME." : "Red lines lock the golem's slam direction for 0.8s.";
            GUI.Label(new Rect(28, 607, 710, 40), status, labelStyle);
            GUI.Label(new Rect(28, 652, 730, 36), "COUNTER: cone + slow on shield break  /  GUARDIAN: ally shield every 5s", smallStyle);
            if (GUI.Button(new Rect(776, 611, 105, 76), speed == 1 ? "1x" : "2x", buttonStyle)) speed = speed == 1 ? 2 : 1;
            GUI.enabled = running && !simulation.IsFinished;
            if (GUI.Button(new Rect(895, 611, 154, 76), paused ? "RESUME" : "PAUSE", buttonStyle)) SetPaused(!paused);
            GUI.enabled = true;
            if (GUI.Button(new Rect(1063, 611, 191, 76), running ? "RETRY" : "FIGHT", buttonStyle))
            { if (running) ResetBattle(); else StartBattle(); }
            GUI.matrix = Matrix4x4.identity;
        }

        static void Fill(Rect rect, Color color)
        { Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous; }

        void OnDestroy()
        {
            if (simulation != null) simulation.Event -= OnCombatEvent;
            foreach (var sprite in ownedSprites) Destroy(sprite);
            if (spriteMaterial != null) Destroy(spriteMaterial);
            if (solidTexture != null) Destroy(solidTexture);
        }
    }
}
