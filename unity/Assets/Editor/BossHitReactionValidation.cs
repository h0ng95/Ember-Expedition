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

        [MenuItem("Expedition/Validate Boss Hit Reactions")]
        public static void Validate()
        {
            var catalog = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            CheckTarget(catalog, true, CatalogEnemyRole.Beast);
            CheckTarget(catalog, true, CatalogEnemyRole.Golem);
            CheckTarget(catalog, false, CatalogEnemyRole.None);
            Debug.Log("Boss hit reactions validated: both bosses retain pose; ordinary hit reactions and damage feedback remain.");
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
