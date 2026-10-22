using System;
using System.Collections.Generic;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Bounded, code-drawn effects: no particle objects or additional sprite sheets per hit.
    internal sealed class CatalogBattleEffects : IDisposable
    {
        enum EffectKind { Impact, Slash, Support, GroundImpact }
        sealed class Effect
        {
            public Vector2 Point;
            public Color Color;
            public EffectKind Kind;
            public float Age, Duration, Size;
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
            if (id.Contains("-FI") || id == "H-01" || id == "H-04") return new Color(1f, .46f, .13f);
            if (id.Contains("-IC") || id == "H-03") return new Color(.4f, .85f, 1f);
            if (id.Contains("-PR") || id == "H-02" || id == "H-05") return new Color(.55f, 1f, .69f);
            return unit != null && unit.Team == CatalogTeam.Enemy ?
                new Color(1f, .59f, .35f) : new Color(1f, .87f, .52f);
        }

        static bool IsArrow(CatalogBattleUnit unit)
        {
            return unit.EnemyRole == CatalogEnemyRole.Raider || unit.FormId.Contains("-AR") ||
                unit.FormId == "H-03" || unit.FormId == "H-06";
        }

        static bool IsProjectile(CatalogBattleUnit unit) { return unit.Range > 2.1f; }

        void Add(EffectKind kind, CatalogBattleUnit unit, Color color, float size, float duration, int direction)
        {
            if (unit == null) return;
            if (effects.Count == 24) effects.RemoveAt(0);
            effects.Add(new Effect { Point = new Vector2(unit.X, unit.Y), Color = color,
                Kind = kind, Size = size, Duration = duration, Direction = direction });
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
                effects.Add(new Effect { Kind = EffectKind.GroundImpact,
                    Point = charge ? new Vector2(source.ChargeImpactX, source.ChargeImpactY) :
                        new Vector2(source.SlamTargetX, source.SlamTargetY),
                    Color = source.BossEnraged ? new Color(1f, .25f, .13f) : new Color(1f, .7f, .3f),
                    Size = charge ? source.ChargeWidth : source.SlamRadius, Duration = .45f });
                Play(hit, 1f);
            }
            if ((e.Kind == CatalogEventKind.BasicHit || e.Kind == CatalogEventKind.SkillUsed) &&
                source != null && target != null && !IsProjectile(source))
                Add(EffectKind.Slash, target, color, e.Kind == CatalogEventKind.SkillUsed ? 40 : 27, .18f, direction);
            if (e.Kind == CatalogEventKind.Damage && target != null)
            {
                bool periodic = e.AbilityId == "BURN";
                bool strong = !periodic && e.AbilityId != "BASIC";
                Add(EffectKind.Impact, target, color, periodic ? 9 : strong ? 35 : 20, .23f, direction);
                if (!periodic)
                {
                    Play(source != null && IsProjectile(source) ? arrow : hit, strong ? .85f : .65f);
                    if (strong && hitStopCooldown <= 0)
                    { HitStopRemaining = .035f; hitStopCooldown = .22f; }
                }
            }
            if (e.Kind == CatalogEventKind.Heal || e.Kind == CatalogEventKind.Shield)
                Add(EffectKind.Support, target, e.Kind == CatalogEventKind.Heal ?
                    new Color(.45f, 1f, .6f) : new Color(.5f, .8f, 1f), 32, .4f, 1);
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
                if (unit.SlamWarningRemaining > 0)
                {
                    Vector2 center = project(new Vector2(unit.SlamTargetX, unit.SlamTargetY));
                    Color warning = unit.SlamIsAftershock ? new Color(1f, .2f, .1f, .9f) :
                        new Color(1f, .65f, .2f, .8f);
                    Ring(center, unit.SlamRadius * 83, unit.SlamRadius * 48, warning, 4);
                    Ring(center, unit.SlamRadius * 83 * .92f, unit.SlamRadius * 48 * .92f, warning, 1);
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
                if (!unit.IsAlive || unit.AttackPhase == CatalogAttackPhase.Ready || !IsProjectile(unit) ||
                    unit.AttackWindupRemaining > .18f || unit.ChargeWarningRemaining > 0) continue;
                CatalogBattleUnit target = battle.Get(unit.AttackTargetId);
                if (target == null || !target.IsAlive) continue;
                Vector2 from = project(new Vector2(unit.X, unit.Y)) + new Vector2(unit.Team == CatalogTeam.Ally ? 20 : -20, -65);
                Vector2 to = project(new Vector2(target.X, target.Y)) + new Vector2(0, -58);
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
                Vector2 point = project(effect.Point) + new Vector2(0, -57);
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
                if (voice != null) { voice.Stop(); UnityEngine.Object.Destroy(voice); }
            effects.Clear(); launched.Clear();
        }
    }
}
