using System;
using System.Collections.Generic;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Bounded, code-drawn effects: no particle objects or additional sprite sheets per hit.
    internal sealed class CatalogBattleEffects : IDisposable
    {
        enum EffectKind { Impact, Slash, Support, GroundImpact, RootBurst, Breath, BombBurst }
        sealed class Effect
        {
            public Vector2 Point;
            public Vector2 EndPoint;
            public Color Color;
            public EffectKind Kind;
            public float Age, Duration, Size, BodyOffset, HalfWidth;
            public int Direction;
        }

        readonly List<Effect> effects = new List<Effect>(24);
        readonly AudioSource[] voices = new AudioSource[4];
        readonly AudioClip hit, arrow, launch;
        readonly HashSet<string> launched = new HashSet<string>();
        float soundCooldown, hitStopCooldown;
        int nextVoice;
        public float HitStopRemaining { get; private set; }

        public CatalogBattleEffects(GameObject owner)
        {
            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null)
                owner.AddComponent<AudioListener>();
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = owner.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0;
                voices[i].volume = .22f;
            }
            hit = Resources.Load<AudioClip>("Audio/melee_hit");
            arrow = Resources.Load<AudioClip>("Audio/arrow_hit");
            launch = Resources.Load<AudioClip>("Audio/arrow_launch");
        }

        static Color AttackColor(CatalogBattleUnit unit)
        {
            string id = unit == null ? "" : unit.FormId;
            if (unit != null && unit.EnemyRole == CatalogEnemyRole.Furnace)
                return new Color(1f, .38f, .11f);
            if (unit != null && unit.EnemyRole == CatalogEnemyRole.CrownDrake)
                return new Color(1f, .47f, .15f);
            if (unit != null && unit.EnemyRole == CatalogEnemyRole.FrostWarden)
                return new Color(.36f, .78f, 1f);
            if (unit != null && unit.Id.StartsWith("garden-", StringComparison.Ordinal))
                return new Color(.65f, .91f, .3f);
            if (id.Contains("-FI") || id == "H-01" || id == "H-04") return new Color(1f, .46f, .13f);
            if (id.Contains("-IC") || id == "H-03") return new Color(.4f, .85f, 1f);
            if (id.Contains("-PR") || id == "H-02" || id == "H-05") return new Color(.55f, 1f, .69f);
            return unit != null && unit.Team == CatalogTeam.Enemy ?
                new Color(1f, .59f, .35f) : new Color(1f, .87f, .52f);
        }

        static bool IsArrow(CatalogBattleUnit unit)
        {
            return unit.EnemyRole == CatalogEnemyRole.Raider ||
                unit.EnemyRole == CatalogEnemyRole.GardenSpitter || unit.FormId.Contains("-AR") ||
                unit.FormId == "H-03" || unit.FormId == "H-06";
        }

        static bool IsProjectile(CatalogBattleUnit unit) { return unit.Range > 2.1f; }

        void Add(EffectKind kind, CatalogBattleUnit unit, Color color, float size, float duration, int direction)
        {
            if (unit == null) return;
            if (effects.Count == 24) effects.RemoveAt(0);
            effects.Add(new Effect { Point = new Vector2(unit.X, unit.Y), Color = color,
                Kind = kind, Size = size, Duration = duration, Direction = direction,
                BodyOffset = unit.EnemyRole == CatalogEnemyRole.CrownDrake ?
                    150f + unit.FlightLift * 60f : 57f });
        }

        public void Observe(CatalogBattleEvent e, CatalogBattle battle)
        {
            CatalogBattleUnit source = battle.Get(e.SourceId), target = battle.Get(e.TargetId);
            Color color = AttackColor(source);
            int direction = source != null && source.Team == CatalogTeam.Enemy ? -1 : 1;
            if (e.Kind == CatalogEventKind.BossImpact && source != null)
            {
                if (effects.Count == 24) effects.RemoveAt(0);
                bool charge = e.AbilityId == "BOSS_CHARGE";
                bool roots = e.AbilityId == "GARDEN_BRIARS";
                bool breath = e.AbilityId == "DRACONIC_BREATH";
                bool bomb = e.AbilityId == "DRACONIC_BOMB";
                effects.Add(new Effect { Kind = breath ? EffectKind.Breath : bomb ?
                    EffectKind.BombBurst : roots ? EffectKind.RootBurst : EffectKind.GroundImpact,
                    Point = breath ? new Vector2(source.BreathOriginX, source.BreathOriginY) :
                        charge ? new Vector2(source.ChargeImpactX, source.ChargeImpactY) :
                        new Vector2(source.SlamTargetX, source.SlamTargetY),
                    EndPoint = breath ? new Vector2(source.BreathTargetX, source.BreathTargetY) : Vector2.zero,
                    Color = source.EnemyRole == CatalogEnemyRole.GardenLord ?
                        new Color(.55f, .86f, .27f) :
                        source.EnemyRole == CatalogEnemyRole.CrownDrake ?
                        new Color(1f, .43f, .12f) :
                        source.EnemyRole == CatalogEnemyRole.Furnace ?
                        new Color(1f, .37f, .1f) :
                        source.EnemyRole == CatalogEnemyRole.FrostWarden ?
                        new Color(.32f, .76f, 1f) : source.BossEnraged ?
                        new Color(1f, .25f, .13f) : new Color(1f, .7f, .3f),
                    Size = breath ? source.BreathLength : charge ? source.ChargeWidth : source.SlamRadius,
                    HalfWidth = breath ? source.BreathHalfWidth : 0f,
                    Duration = roots ? .6f : breath ? .4f : .45f });
                Play(hit, 1f);
            }
            if ((e.Kind == CatalogEventKind.BasicHit || e.Kind == CatalogEventKind.SkillUsed) &&
                source != null && target != null && !IsProjectile(source))
                Add(EffectKind.Slash, target, color, e.Kind == CatalogEventKind.SkillUsed ? 40 : 27, .18f, direction);
            if (e.Kind == CatalogEventKind.Damage && target != null)
            {
                bool periodic = e.AbilityId == "BURN" || e.AbilityId == "POISON";
                bool strong = !periodic && e.AbilityId != "BASIC";
                Add(EffectKind.Impact, target, color, periodic ? 9 : strong ? 35 : 20, .23f, direction);
                if (!periodic)
                {
                    Play(source != null && IsProjectile(source) ? arrow : hit, strong ? .85f : .65f);
                    if (strong && !target.IsBoss && hitStopCooldown <= 0)
                    { HitStopRemaining = .035f; hitStopCooldown = .22f; }
                }
            }
            if (e.Kind == CatalogEventKind.Heal || e.Kind == CatalogEventKind.Shield)
                Add(EffectKind.Support, target, e.Kind == CatalogEventKind.Heal ?
                    new Color(.45f, 1f, .6f) : new Color(.5f, .8f, 1f), 32, .4f, 1);
            if (e.Kind == CatalogEventKind.Poison)
                Add(EffectKind.Impact, target, new Color(.75f, 1f, .35f), 27, .35f, 1);
        }

        void Play(AudioClip clip, float volume)
        {
            if (clip == null || soundCooldown > 0) return;
            AudioSource voice = voices[nextVoice++ % voices.Length];
            voice.clip = clip;
            voice.volume = .25f * volume;
            voice.pitch = 1f;
            voice.Play();
            soundCooldown = .045f;
        }

        public void SetPaused(bool value)
        {
            foreach (AudioSource voice in voices)
                if (value) voice.Pause(); else voice.UnPause();
        }

        public void Tick(float realDt, float gameDt, CatalogBattle battle)
        {
            HitStopRemaining = Mathf.Max(0, HitStopRemaining - realDt);
            hitStopCooldown = Mathf.Max(0, hitStopCooldown - realDt);
            soundCooldown = Mathf.Max(0, soundCooldown - realDt);
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                effects[i].Age += gameDt;
                if (effects[i].Age >= effects[i].Duration) effects.RemoveAt(i);
            }
            foreach (CatalogBattleUnit unit in battle.Units)
            {
                if (unit.AttackPhase == CatalogAttackPhase.Ready || !unit.IsAlive)
                { launched.Remove(unit.Id); continue; }
                if (IsProjectile(unit) && unit.AttackTargetId != null && unit.AttackWindupRemaining <= .18f &&
                    launched.Add(unit.Id)) Play(launch, .4f);
            }
        }

        public void DrawGround(CatalogBattle battle, Func<Vector2, Vector2> project)
        {
            foreach (CatalogBattleUnit unit in battle.Units)
            {
                if (!unit.IsAlive) continue;
                if (unit.EnemyRole == CatalogEnemyRole.CrownDrake)
                {
                    Vector2 ground = project(new Vector2(unit.X, unit.Y));
                    float lift = unit.FlightLift;
                    if (lift > 0f)
                    {
                        Ring(ground + new Vector2(0, -4), 65 + lift * 19, 11 + lift * 4,
                            new Color(.08f, .045f, .055f, .65f - lift * .2f), 7);
                        Line(ground + new Vector2(-30, -3), ground + new Vector2(30, -3), 3,
                            new Color(.17f, .09f, .07f, .45f));
                    }
                    if (unit.BreathWarningRemaining > 0f)
                    {
                        Vector2 origin = new Vector2(unit.BreathOriginX, unit.BreathOriginY);
                        Vector2 end = new Vector2(unit.BreathTargetX, unit.BreathTargetY);
                        Vector2 axis = (end - origin).normalized;
                        Vector2 side = new Vector2(-axis.y, axis.x) * unit.BreathHalfWidth;
                        Vector2 a = project(origin + side), b = project(end + side);
                        Vector2 c = project(end - side), d = project(origin - side);
                        Color fireLine = new Color(1f, .48f, .16f, .9f);
                        Line(a, b, 4, fireLine); Line(b, c, 4, fireLine);
                        Line(c, d, 4, fireLine); Line(d, a, 4, fireLine);
                        // Core tests a capsule around the segment, including both rounded ends.
                        Ring(project(origin), unit.BreathHalfWidth * 83f,
                            unit.BreathHalfWidth * 48f, fireLine, 3f);
                        Ring(project(end), unit.BreathHalfWidth * 83f,
                            unit.BreathHalfWidth * 48f, fireLine, 3f);
                        for (int i = 0; i < 7; i++)
                            Line(Vector2.Lerp(project(origin), project(end), i / 7f),
                                Vector2.Lerp(project(origin), project(end), (i + .45f) / 7f),
                                3, new Color(1f, .76f, .3f, .8f));
                    }
                }
                if (unit.SlamWarningRemaining > 0)
                {
                    Vector2 center = project(new Vector2(unit.SlamTargetX, unit.SlamTargetY));
                    Color warning = unit.EnemyRole == CatalogEnemyRole.CrownDrake ?
                        unit.DrakePhase == CrownDrakePhase.AirWarning ?
                        new Color(1f, .37f, .16f, .95f) : new Color(1f, .68f, .28f, .95f) :
                        unit.EnemyRole == CatalogEnemyRole.GardenLord ?
                        new Color(.64f, .92f, .35f, .95f) :
                        unit.EnemyRole == CatalogEnemyRole.FrostWarden ?
                        new Color(.35f, .83f, 1f, .95f) :
                        unit.EnemyRole == CatalogEnemyRole.Furnace ?
                        unit.SlamIsAftershock ? new Color(1f, .16f, .08f, .95f) :
                        new Color(1f, .46f, .1f, .9f) :
                        unit.SlamIsAftershock ? new Color(1f, .2f, .1f, .9f) :
                        new Color(1f, .65f, .2f, .8f);
                    Ring(center, unit.SlamRadius * 83, unit.SlamRadius * 48, warning, 4);
                    Ring(center, unit.SlamRadius * 83 * .92f, unit.SlamRadius * 48 * .92f,
                        unit.EnemyRole == CatalogEnemyRole.GardenLord ?
                        new Color(.65f, .36f, .78f, .9f) : warning, 1);
                    Line(center - new Vector2(10, 0), center + new Vector2(10, 0), 3, warning);
                    Line(center - new Vector2(0, 6), center + new Vector2(0, 6), 3, warning);
                }
                if (unit.ChargeWarningRemaining <= 0 && !unit.IsCharging) continue;
                Vector2 from = project(new Vector2(unit.X, unit.Y));
                Vector2 to = project(new Vector2(unit.ChargeTargetX, unit.ChargeTargetY));
                Color color = new Color(1f, .43f, .18f, .65f + .25f * Mathf.Sin(battle.Time * 18));
                if (unit.BossPatternsEnabled)
                {
                    Vector2 start = new Vector2(unit.ChargeOriginX, unit.ChargeOriginY);
                    Vector2 end = new Vector2(unit.ChargeImpactX, unit.ChargeImpactY);
                    Vector2 delta = (end - start).normalized;
                    Vector2 side = new Vector2(-delta.y, delta.x) * unit.ChargeWidth;
                    Vector2 a = project(start + side), b = project(end + side);
                    Vector2 c = project(end - side), d = project(start - side);
                    Line(a, b, 3, color); Line(b, c, 3, color); Line(c, d, 3, color); Line(d, a, 3, color);
                    from = project(start); to = project(end);
                    Ring(from, unit.ChargeWidth * 83, unit.ChargeWidth * 48, color, 2);
                    Ring(to, unit.ChargeWidth * 83, unit.ChargeWidth * 48, color, 2);
                }
                for (int i = 0; i < 8; i++)
                    Line(Vector2.Lerp(from, to, i / 8f), Vector2.Lerp(from, to, (i + .55f) / 8f), 3, color);
                Ring(to, 21, 9, color, 2);
            }
        }

        public void Draw(CatalogBattle battle, Func<Vector2, Vector2> project)
        {
            foreach (CatalogBattleUnit unit in battle.Units)
            {
                if (!unit.IsAlive || unit.EnemyRole != CatalogEnemyRole.CrownDrake ||
                    unit.DrakePhase != CrownDrakePhase.AirWarning || unit.SlamWarningRemaining <= 0f)
                    continue;
                Vector2 center = project(new Vector2(unit.SlamTargetX, unit.SlamTargetY));
                float fall = Mathf.Clamp01(1f - unit.SlamWarningRemaining / 1.1f);
                Vector2 fireball = center + new Vector2(0, -150f * (1f - fall));
                Ring(fireball, 11, 11, new Color(1f, .72f, .22f, .96f), 6);
                Line(fireball + new Vector2(0, -27), fireball + new Vector2(0, -12),
                    7, new Color(1f, .31f, .1f, .8f));
            }
            foreach (CatalogBattleUnit unit in battle.Units)
            {
                if (!unit.IsAlive) continue;
                Vector2 foot = project(new Vector2(unit.X, unit.Y));
                if (unit.FrozenRemaining > 0f)
                {
                    Color ice = new Color(.59f, .87f, 1f, .85f);
                    Ring(foot + new Vector2(0, -8), 28, 9, ice, 2);
                    Line(foot + new Vector2(-25, -10), foot + new Vector2(-18, -58), 3, ice);
                    Line(foot + new Vector2(24, -10), foot + new Vector2(17, -66), 3, ice);
                    Line(foot + new Vector2(-18, -58), foot + new Vector2(-9, -73), 2, ice);
                    Line(foot + new Vector2(17, -66), foot + new Vector2(8, -79), 2, ice);
                }
                if (unit.BurnRemaining > 0f)
                {
                    Color ember = new Color(1f, .45f, .13f, .85f);
                    Line(foot + new Vector2(-16, -20), foot + new Vector2(-11, -37), 3, ember);
                    Line(foot + new Vector2(13, -13), foot + new Vector2(18, -29), 3, ember);
                }
                if (unit.PoisonRemaining > 0f)
                {
                    Color venom = new Color(.68f, .94f, .36f, .88f);
                    Line(foot + new Vector2(-22, -29), foot + new Vector2(-18, -43), 3, venom);
                    Line(foot + new Vector2(20, -37), foot + new Vector2(25, -51), 3, venom);
                    Fill(new Rect(foot.x - 3, foot.y - 57, 6, 6),
                        new Color(.64f, .34f, .77f, .9f));
                }
            }
            foreach (CatalogBattleUnit unit in battle.Units)
            {
                if (!unit.IsAlive || unit.AttackPhase == CatalogAttackPhase.Ready || !IsProjectile(unit) ||
                    unit.AttackWindupRemaining > .18f || unit.ChargeWarningRemaining > 0) continue;
                CatalogBattleUnit target = battle.Get(unit.AttackTargetId);
                if (target == null || !target.IsAlive) continue;
                Vector2 from = project(new Vector2(unit.X, unit.Y)) + new Vector2(unit.Team == CatalogTeam.Ally ? 20 : -20, -65);
                float targetBodyOffset = target.EnemyRole == CatalogEnemyRole.CrownDrake ?
                    150f + target.FlightLift * 60f : 58f;
                Vector2 to = project(new Vector2(target.X, target.Y)) +
                    new Vector2(0, -targetBodyOffset);
                float progress = Mathf.Clamp01(1 - unit.AttackWindupRemaining / .18f);
                Vector2 point = Vector2.Lerp(from, to, progress);
                Vector2 direction = (to - from).normalized;
                Color color = AttackColor(unit);
                Line(point - direction * 27, point, IsArrow(unit) ? 3 : 7, color);
                if (IsArrow(unit))
                {
                    Vector2 normal = new Vector2(-direction.y, direction.x);
                    Line(point, point - direction * 9 + normal * 5, 2, color);
                    Line(point, point - direction * 9 - normal * 5, 2, color);
                }
                else
                {
                    Fill(new Rect(point.x - 4, point.y - 4, 8, 8), new Color(1, 1, 1, .95f));
                    Ring(point, 10, 10, color, 2);
                }
            }
            foreach (Effect effect in effects)
            {
                float t = Mathf.Clamp01(effect.Age / effect.Duration);
                if (effect.Kind == EffectKind.GroundImpact)
                {
                    Color impactColor = effect.Color;
                    impactColor.a = 1 - t;
                    Ring(project(effect.Point), effect.Size * 83 * (.6f + .4f * t),
                        effect.Size * 48 * (.6f + .4f * t), impactColor, 6 * (1 - t) + 1);
                    continue;
                }
                if (effect.Kind == EffectKind.RootBurst)
                {
                    DrawRootBurst(project(effect.Point), t);
                    continue;
                }
                if (effect.Kind == EffectKind.Breath)
                {
                    DrawBreath(project, effect, t);
                    continue;
                }
                if (effect.Kind == EffectKind.BombBurst)
                {
                    DrawBombBurst(project(effect.Point), t);
                    continue;
                }
                Vector2 point = project(effect.Point) + new Vector2(0, -effect.BodyOffset);
                Color color = effect.Color;
                color.a = 1 - t;
                float radius = effect.Size * (.3f + t);
                if (effect.Kind == EffectKind.Slash)
                {
                    Vector2 previous = point + new Vector2(-radius * effect.Direction, -radius);
                    for (int i = 1; i <= 6; i++)
                    {
                        float angle = Mathf.Lerp(-1.3f, 1.3f, i / 6f);
                        Vector2 next = point + new Vector2(Mathf.Cos(angle) * radius * effect.Direction, Mathf.Sin(angle) * radius);
                        Line(previous, next, 4 * (1 - t) + 1, color);
                        previous = next;
                    }
                }
                else if (effect.Kind == EffectKind.Support)
                {
                    Ring(point, radius, radius * .8f, color, 2);
                    Line(point + new Vector2(-7, -t * 20), point + new Vector2(7, -t * 20), 3, color);
                    Line(point + new Vector2(0, -7 - t * 20), point + new Vector2(0, 7 - t * 20), 3, color);
                }
                else
                {
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = i * Mathf.PI * .4f + .3f;
                        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        Line(point + direction * radius * .45f, point + direction * radius, 3 * (1 - t) + 1, color);
                    }
                    if (t < .35f) Fill(new Rect(point.x - 5, point.y - 5, 10, 10), new Color(1, 1, .9f, 1 - t * 2));
                }
            }
        }

        // Five narrow roots rise at the stored impact point, then retract into the ground.
        // This is drawn after actors so each of the boss's separate target strikes is visible.
        static void DrawRootBurst(Vector2 center, float progress)
        {
            float fade = 1f - Mathf.Clamp01((progress - .55f) / .45f);
            Ring(center + new Vector2(0, -3), 69, 12, new Color(.23f, .11f, .28f, .7f * fade), 4);
            for (int i = 0; i < 5; i++)
            {
                float local = progress - i * .035f;
                if (local <= 0f) continue;
                float rise = local < .19f ? local / .19f : local < .66f ? 1f :
                    1f - Mathf.Clamp01((local - .66f) / .34f);
                if (rise <= 0f) continue;
                int spread = i - 2;
                Vector2 basePoint = center + new Vector2(spread * 21f, Mathf.Abs(spread) * 2f);
                Vector2 tip = basePoint + new Vector2(spread * 3f, -(80f - Mathf.Abs(spread) * 11f) * rise);
                Line(basePoint, tip, 8f, new Color(.22f, .11f, .27f, .92f * fade));
                Line(basePoint + new Vector2(2, 0), tip + new Vector2(2, 0), 3f,
                    new Color(.53f, .88f, .27f, .95f * fade));
                Vector2 fork = Vector2.Lerp(basePoint, tip, .52f);
                Line(fork, fork + new Vector2(spread <= 0 ? -10f : 10f, -14f * rise), 3f,
                    new Color(.62f, .35f, .73f, .9f * fade));
            }
        }

        static void DrawBreath(Func<Vector2, Vector2> project, Effect effect, float progress)
        {
            Vector2 axis = (effect.EndPoint - effect.Point).normalized;
            Vector2 side = new Vector2(-axis.y, axis.x);
            float reach = Mathf.Lerp(.32f, 1f, Mathf.Clamp01(progress / .3f));
            float fade = 1f - Mathf.Clamp01((progress - .45f) / .55f);
            for (int i = -3; i <= 3; i++)
            {
                float offset = i / 3f * effect.HalfWidth * .88f;
                Vector2 start = effect.Point + axis * .35f + side * offset;
                Vector2 end = Vector2.Lerp(effect.Point, effect.EndPoint, reach) + side * offset;
                Color flame = i == 0 ? new Color(1f, .88f, .32f, .95f * fade) :
                    i % 2 == 0 ? new Color(1f, .32f, .1f, .82f * fade) :
                    new Color(1f, .58f, .17f, .9f * fade);
                Line(project(start), project(end), i == 0 ? 12f : 7f, flame);
                Vector2 ember = project(Vector2.Lerp(start, end, .45f + (i + 3) * .05f));
                Fill(new Rect(ember.x - 4, ember.y - 4, 8, 8), flame);
            }
        }

        static void DrawBombBurst(Vector2 center, float progress)
        {
            float fade = 1f - progress;
            Ring(center, 48f * (.55f + progress), 22f * (.55f + progress),
                new Color(1f, .44f, .13f, fade), 6f * fade + 1f);
            for (int i = -2; i <= 2; i++)
            {
                Vector2 basePoint = center + new Vector2(i * 15f, -5f);
                Vector2 tip = basePoint + new Vector2(i * 3f, -(35f - Mathf.Abs(i) * 5f) * fade);
                Line(basePoint, tip, 6f * fade + 1f,
                    new Color(1f, i == 0 ? .8f : .42f, .14f, fade));
            }
        }

        static void Ring(Vector2 center, float xRadius, float yRadius, Color color, float thickness)
        {
            Vector2 previous = center + new Vector2(xRadius, 0);
            for (int i = 1; i <= 12; i++)
            {
                float angle = i * Mathf.PI / 6f;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * xRadius, Mathf.Sin(angle) * yRadius);
                Line(previous, next, thickness, color); previous = next;
            }
        }

        static void Line(Vector2 from, Vector2 to, float thickness, Color color)
        {
            if ((to - from).sqrMagnitude < .01f) return;
            Matrix4x4 matrix = GUI.matrix;
            // Compose in canvas coordinates; RotateAroundPivot uses screen pivots under a scaled safe area.
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(from.x, from.y, 0),
                Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg), Vector3.one);
            Fill(new Rect(0, -thickness * .5f, (to - from).magnitude, thickness), color);
            GUI.matrix = matrix;
        }

        static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public void Dispose()
        {
            foreach (AudioSource voice in voices)
                if (voice != null)
                {
                    voice.Stop();
                    if (Application.isPlaying) UnityEngine.Object.Destroy(voice);
                    else UnityEngine.Object.DestroyImmediate(voice);
                }
            effects.Clear(); launched.Clear();
        }
    }
}
