using System;
using System.Reflection;
using Expedition.Presentation;
using UnityEditor;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class CombatSettlementValidation
    {
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        static void Set(CatalogCombatLab lab, string name, object value)
        {
            typeof(CatalogCombatLab).GetField(name, Private).SetValue(lab, value);
        }

        static T Get<T>(CatalogCombatLab lab, string name)
        {
            return (T)typeof(CatalogCombatLab).GetField(name, Private).GetValue(lab);
        }

        static void Advance(CatalogCombatLab lab, float realDt)
        {
            typeof(CatalogCombatLab).GetMethod("AdvanceSettlement", Private).Invoke(lab, new object[] { realDt });
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Combat settlement: " + message);
        }

        [MenuItem("Expedition/Validate Combat Settlement")]
        public static void Validate()
        {
            var go = new GameObject("Combat settlement validation");
            try
            {
                var lab = go.AddComponent<CatalogCombatLab>();
                int callbacks = 0;
                bool result = false;
                Set(lab, "settling", true);
                Set(lab, "settlementVictory", true);
                Set(lab, "expeditionResult", (Action<bool>)(victory => { callbacks++; result = victory; }));
                Advance(lab, .2f);
                Check(callbacks == 0 && Mathf.Abs(Get<float>(lab, "settlementAge") - .2f) < .001f,
                    "settlement holds its callback while death frames play");
                Set(lab, "paused", true);
                Advance(lab, 1f);
                Check(callbacks == 0 && Mathf.Abs(Get<float>(lab, "settlementAge") - .2f) < .001f,
                    "pause freezes settlement time");
                Set(lab, "paused", false);
                Advance(lab, .36f);
                Check(callbacks == 1 && result, "result callback fires once with the saved outcome");
                Advance(lab, 1f);
                Check(callbacks == 1, "result callback cannot repeat");

                lab.enabled = true;
                Set(lab, "settling", true);
                Set(lab, "settlementAge", .4f);
                Set(lab, "expeditionResult", (Action<bool>)(_ => callbacks++));
                typeof(CatalogCombatLab).GetMethod("ClearBattle", Private).Invoke(lab, null);
                Check(!Get<bool>(lab, "settling") && Get<float>(lab, "settlementAge") == 0f,
                    "ClearBattle resets settlement state");
                Advance(lab, 1f);
                Check(callbacks == 1, "cleared battle cannot dispatch an old result");
                Debug.Log("Combat settlement validated: callback once, pause, reset.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
