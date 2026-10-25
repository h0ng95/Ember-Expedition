using System;
using System.Collections;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class BossHitReactionValidation
    {
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        static object Field(object owner, string name)
        {
            return owner.GetType().GetField(name, Private | BindingFlags.Public).GetValue(owner);
        }

        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Boss hit reaction: " + message);
        }

        static void SetField(object owner, string name, object value)
        {
            owner.GetType().GetField(name, Private | BindingFlags.Public).SetValue(owner, value);
        }

        [MenuItem("Expedition/Validate Boss Hit Reactions")]
        public static void Validate()
        {
            var catalog = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            CheckTarget(catalog, true, CatalogEnemyRole.Beast);
            CheckTarget(catalog, true, CatalogEnemyRole.Golem);
            CheckTarget(catalog, false, CatalogEnemyRole.None);
            CheckPeriodicAndCorpse(catalog);
            Debug.Log("Boss hit reactions validated: direct hits, periodic damage, and corpse fade.");
        }

        static void CheckPeriodicAndCorpse(CatalogDatabase catalog)
        {
            var go = new GameObject("Periodic hit presentation validation");
            try
            {
                var battle = new CatalogBattle(catalog);
                var source = battle.AddUnit("source", CatalogTeam.Ally, "B-KN", null, null, -5f, 0f);
                var target = battle.AddEnemy("target", 1000f, 1f, 0f, 20f, 1f, .1f,
                    false, 5f, 0f);
                var lab = go.AddComponent<CatalogCombatLab>();
                lab.OpenExpeditionBattle(battle, 1, 30, "validation", _ => { }, () => { });
                object targetView = null;
                foreach (object view in (IEnumerable)Field(lab, "combatViews"))
                    if (ReferenceEquals(Field(view, "Unit"), target)) targetView = view;
                Check(targetView != null, "periodic target has a presentation view");
                SetField(targetView, "HitTime", .13f);
                SetField(targetView, "NewHit", false);
                int burnTicks = 0, poisonTicks = 0;
                battle.Event += e => {
                    if (e.Kind != CatalogEventKind.Damage || e.TargetId != target.Id) return;
                    if (e.AbilityId == "BURN") burnTicks++;
                    if (e.AbilityId == "POISON") poisonTicks++;
                };
                Type[] signature = { typeof(CatalogBattleUnit), typeof(CatalogBattleUnit), typeof(float) };
                typeof(CatalogBattle).GetMethod("Burn", Private, null, signature, null)
                    .Invoke(battle, new object[] { source, target, 10f });
                typeof(CatalogBattle).GetMethod("Poison", Private, null, signature, null)
                    .Invoke(battle, new object[] { source, target, 6f });
                battle.Step(1.05f);
                Check(burnTicks == 1 && poisonTicks == 1 && target.Hp < target.MaxHp,
                    "actual burn and poison ticks apply damage");
                Check(Mathf.Abs((float)Field(targetView, "HitTime") - .13f) < .0001f &&
                    !(bool)Field(targetView, "NewHit"),
                    "periodic ticks leave an existing direct-hit timer untouched");
                Check(((IList)Field(lab, "floating")).Count >= 2,
                    "periodic damage retains visible numbers");

                MethodInfo alpha = typeof(CatalogCombatLab).GetMethod("CorpseAlpha",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Func<float, float> corpse = age => (float)alpha.Invoke(null, new object[] { age });
                Check(ForestEnemyArt.FrameIndex(4, .44f, 0f, 0f, 0f, 0f, false, 0f, false) == 21 &&
                    corpse(.44f) == 1f, "fourth death frame stays fully visible");
                Check(corpse(.55f) > .75f && corpse(.8f) > 0f && corpse(1.05f) == 0f,
                    "settlement keeps the corpse visible before it fades away");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static void CheckTarget(CatalogDatabase catalog, bool boss, CatalogEnemyRole role)
        {
            var go = new GameObject("Boss hit reaction validation");
            try
            {
                var battle = new CatalogBattle(catalog);
                battle.AddUnit("ally", CatalogTeam.Ally, "B-KN", null, null, -.5f, 0);
                var target = battle.AddEnemy("target", 10000, 1, 0, 2, 1, .01f, boss, .5f, 0, role);
                // Isolate received-hit feedback from the boss's outgoing charge impact.
                target.GetType().GetField("NextChargeAt", Private).SetValue(target, float.PositiveInfinity);
                var lab = go.AddComponent<CatalogCombatLab>();
                lab.OpenExpeditionBattle(battle, boss ? 5 : 1, 10, "validation", _ => { }, () => { });
                bool sawStrongDamage = false;
                battle.Event += e => {
                    if (e.Kind == CatalogEventKind.Damage && e.TargetId == target.Id && e.AbilityId != "BASIC")
                        sawStrongDamage = true;
                };
                battle.Step(3f);
                object targetView = null;
                foreach (object view in (IEnumerable)Field(lab, "combatViews"))
                    if (ReferenceEquals(Field(view, "Unit"), target)) targetView = view;
                Check(targetView != null && target.Hp < target.MaxHp && sawStrongDamage,
                    "real basic and skill damage reaches target");
                Check((float)Field(targetView, "HitTime") == (boss ? 0f : .24f),
                    "only ordinary targets receive the pose/flash/shake timer");
                Check(((IList)Field(lab, "floating")).Count > 0, "damage numbers remain visible");
                object effects = Field(lab, "battleEffects");
                float hitStop = (float)effects.GetType().GetProperty("HitStopRemaining").GetValue(effects, null);
                Check(boss ? hitStop == 0f : hitStop > 0f,
                    "received boss hits do not pause battle; ordinary strong hits still do");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
