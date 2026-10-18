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
            public SpriteRenderer Renderer;
            public float Height, Hit, Punch, Death;
            public CombatUnit Unit;
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
        UnitView dragging;
        Vector2 dragOrigin, dragOffset;
        Rect viewport;
        float accumulator, speed = 1f;
        bool running, paused, muted, suspended, lowFrameRate;
        GUIStyle titleStyle, labelStyle, smallStyle, buttonStyle, numberStyle;

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
            AddUnit("knight", new Vector2(-3, -1), sheet, new Rect(493, 641, 302, 423), 1.5f, new Color(.95f, .8f, .35f));
            AddUnit("archer", new Vector2(-5, -2), sheet, new Rect(896, 641, 284, 393), 1.4f, new Color(.5f, .85f, .7f));
            AddUnit("beast", new Vector2(4, -1), sheet, new Rect(892, 124, 300, 353), 1.7f, new Color(.85f, .35f, .3f));
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
            if (sheet == null) { renderer.color = fallback; renderer.transform.localScale = new Vector3(.6f, height, 1); }
            renderer.transform.position = position;
            views.Add(id, new UnitView { Id = id, Position = position, Renderer = renderer, Height = height });
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
                bool enemy = view.Id == "beast", ranged = view.Id == "archer";
                var definition = new UnitDefinition
                {
                    Id = view.Id, Team = enemy ? Team.Enemy : Team.Ally,
                    AttackKind = ranged ? AttackKind.Ranged : AttackKind.Melee,
                    MaxHp = enemy ? 750 : ranged ? 120 : 200, Damage = enemy ? 8 : ranged ? 22 : 32,
                    Range = ranged ? 5.8f : 1.15f, MoveSpeed = enemy ? .75f : 1.5f,
                    Cooldown = enemy ? 1.2f : ranged ? 1.05f : .8f,
                    Windup = ranged ? .24f : .18f, ProjectileSpeed = 12f, Radius = .32f
                };
                view.Unit = new CombatUnit(definition, view.Position.x, view.Position.y);
                units.Add(view.Unit);
            }
            simulation = new CombatSimulation(units);
            simulation.Event += OnCombatEvent;
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
                view.Position = view.Id == "knight" ? new Vector2(-3, -1) : view.Id == "archer" ? new Vector2(-5, -2) : new Vector2(4, -1);
                view.Unit = null; view.Hit = view.Punch = view.Death = 0;
                view.Renderer.color = Color.white;
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
                { simulation.Step(Tick); accumulator -= Tick; }
            }
            foreach (var view in views.Values)
            {
                if (view.Unit != null) view.Position = new Vector2(view.Unit.X, view.Unit.Y);
                float direction = view.Id == "beast" ? -1 : 1;
                float anticipation = view.Unit != null ? view.Unit.AttackProgress : 0;
                Vector2 offset = new Vector2(direction * (view.Punch * .22f - anticipation * .08f - view.Hit * .1f), view.Hit * .04f);
                view.Renderer.transform.position = view.Position + offset;
                view.Renderer.sortingOrder = 30 - Mathf.RoundToInt(view.Position.y * 10);
                flashBlock.SetFloat(FlashId, view.Hit);
                view.Renderer.SetPropertyBlock(flashBlock);
                if (view.Unit != null && !view.Unit.IsAlive) view.Death = Mathf.Min(1, view.Death + visualDt * 1.8f);
                view.Renderer.color = new Color(1, 1, 1, 1 - view.Death);
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
            if (down && viewport.Contains(screen) && ui.y > 90 && ui.y < 580)
            {
                foreach (var view in views.Values)
                    if (view.Id != "beast" && view.Renderer.bounds.Contains(new Vector3(world.x, world.y, 0)))
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
                if (numbers.Count < 24) numbers.Add(new Number { Position = impact, Damage = (int)e.Damage, AlliedTarget = target.Id != "beast" });
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
                numberStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            GUI.matrix = Matrix4x4.TRS(new Vector3(viewport.x, Screen.height - viewport.yMax, 0), Quaternion.identity,
                new Vector3(viewport.width / UiWidth, viewport.height / UiHeight, 1));
            Fill(new Rect(0, 0, UiWidth, 88), new Color(.035f, .08f, .075f, .95f));
            Fill(new Rect(0, 588, UiWidth, 132), new Color(.035f, .08f, .075f, .96f));
            GUI.Label(new Rect(26, 10, 650, 40), "EMBER EXPEDITION / COMBAT LAB", titleStyle);
            GUI.Label(new Rect(28, 51, 720, 30), running ? "TWO ALLIES  /  ONE ENEMY  /  " + simulation.Elapsed.ToString("0.0") + "s" : "PREPARATION  /  DRAG YOUR ALLIES", smallStyle);
            if (GUI.Button(new Rect(970, 17, 130, 55), lowFrameRate ? "30 FPS" : "60 FPS", buttonStyle))
            { lowFrameRate = !lowFrameRate; Application.targetFrameRate = lowFrameRate ? 30 : 60; }
            if (GUI.Button(new Rect(1116, 17, 138, 55), muted ? "SOUND OFF" : "SOUND ON", buttonStyle))
            { muted = !muted; foreach (var voice in voices) voice.mute = muted; }
            if (!running)
            {
                Fill(new Rect(160, 376, 400, 208), new Color(.5f, .95f, .65f, .07f));
                Fill(new Rect(160, 376, 400, 2), new Color(.6f, .9f, .6f, .55f));
                Fill(new Rect(160, 376, 2, 208), new Color(.6f, .9f, .6f, .55f));
                Fill(new Rect(558, 376, 2, 208), new Color(.6f, .9f, .6f, .55f));
                Fill(new Rect(160, 582, 400, 2), new Color(.6f, .9f, .6f, .55f));
            }
            foreach (var view in views.Values)
            {
                if (view.Death >= 1) continue;
                Vector2 point = ScreenToUi(gameCamera.WorldToScreenPoint(view.Position + Vector2.up * (view.Height + .16f)));
                float health = view.Unit == null ? 1 : Mathf.Clamp01((float)view.Unit.Hp / view.Unit.Definition.MaxHp);
                Fill(new Rect(point.x - 46, point.y, 92, 8), new Color(.03f, .04f, .03f, .85f));
                Fill(new Rect(point.x - 44, point.y + 2, 88 * health, 4), view.Id == "beast" ? new Color(.95f, .4f, .3f) : new Color(.6f, .9f, .5f));
                if (!running) GUI.Label(new Rect(point.x - 75, point.y - 25, 150, 26), view.Id == "beast" ? "MOSS BEAST" : view.Id.ToUpperInvariant(), smallStyle);
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
            string status = !running ? "Place the knight ahead of the archer." : simulation.IsFinished ?
                (simulation.Winner == Team.Ally ? "VICTORY - compare another formation." : "DEFEAT - adjust your formation.") : paused ? "PAUSED - tap RESUME to continue." : "Watch attacks, projectiles and impact timing.";
            GUI.Label(new Rect(28, 607, 710, 40), status, labelStyle);
            GUI.Label(new Rect(28, 652, 710, 36), "Concept art + procedural motion / no progress saving", smallStyle);
            if (GUI.Button(new Rect(776, 611, 105, 76), speed == 1 ? "1x" : "2x", buttonStyle)) speed = speed == 1 ? 2 : 1;
            GUI.enabled = running && !simulation.IsFinished;
            if (GUI.Button(new Rect(895, 611, 154, 76), paused ? "RESUME" : "PAUSE", buttonStyle)) SetPaused(!paused);
            GUI.enabled = true;
            if (GUI.Button(new Rect(1063, 611, 191, 76), running ? "RESET" : "FIGHT", buttonStyle))
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
