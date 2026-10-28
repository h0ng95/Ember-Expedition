using System;
using System.IO;
using System.Linq;
using Expedition.Core;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class DrakeSpacingValidation
    {
        public static void ValidateAndBuild()
        {
            ExpeditionResetValidation.Validate();
            var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var legacy=JsonUtility.FromJson<ExpeditionAccountSave>(File.ReadAllText("../tests/DrakeSpacing/qa-roster-v048.json"));
            legacy.Validate(db);
            var old=ExpeditionRun.Restore(db,legacy.Run);
            if(old.RulesVersion!=3||old.BeginBattle().Get("palace-boss").Range!=1.1f)
                throw new Exception("Legacy drake movement changed.");
            old.ResolveBattle(true);
            var record=new ExpeditionHistory();record.Observe(old);
            var current=ExpeditionRun.New(db,50001);
            while(current.Victories<29){current.BeginBattle();current.ResolveBattle(true);current.Continue();}
            var boss=current.BeginBattle().Get("palace-boss");
            if(!boss.DrakeRangedBasic||boss.Range!=3.2f||boss.Power!=145f||boss.AttackInterval!=1.8f)
                throw new Exception("New drake firebolt/range mismatch.");
            current.ResolveBattle(true);record.Observe(current);
            var restored=ExpeditionHistory.Restore(db,JsonUtility.FromJson<ExpeditionHistorySave>(JsonUtility.ToJson(record.Capture())));
            if(restored.Best.BalanceVersion!=ExpeditionHistory.BalanceVersion||restored.ArchivedBests.Single().BalanceVersion!="normal-048")
                throw new Exception("Drake revision mixed score versions.");
            Debug.Log("PASS drake spacing Unity: v048 account retains melee; v050 range/firebolt, original power/cadence and versioned records verified.");
            DragonBreathPlumeValidation.ValidateAndBuild();
            Directory.CreateDirectory("Logs/v050-fixtures");
            File.Copy("Logs/v049-fixtures/wave-30.json","Logs/v050-fixtures/wave-30.json",true);
        }
    }
}
