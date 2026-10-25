using System;
using System.IO;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class UnitPreviewValidation
    {
        static int checks;
        static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception("Unit preview: " + why); }
        static void Set(object obj, string name, object value) { obj.GetType().GetField(name, Flags).SetValue(obj, value); }
        static object Call(object obj, string name, params object[] args) { return obj.GetType().GetMethod(name, Flags).Invoke(obj, args); }
        static ExpeditionRun Fixture(CatalogDatabase db, string form, string branch, string partner)
        {
            var run = ExpeditionRun.New(db, 41001, ExpeditionDifficulty.Normal,
                new[] { "tactical-start", "tactical-vigor", "ember-start", "ember-insight", "frost-start", "frost-focus" });
            while (run.Wave < 15) { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
            run.PurchaseTactic("bulwark"); run.PurchaseTactic("shieldcraft"); run.PurchaseTactic("spellcraft");
            var save = run.Capture();
            save.Roster.Members[0].FormId = form; save.Roster.Members[0].BranchId = branch;
            save.Roster.Members[0].Growth = form.StartsWith("B-") ? 2 : form.StartsWith("E-") ? 6 : 0;
            save.Roster.Members[0].InheritedTraitId = "T-PR";
            if (partner != null) { save.Roster.Members[1].FormId = partner; save.Roster.Members[1].BranchId = partner; save.Roster.Members[1].InheritedTraitId = "T-IC"; }
            return ExpeditionRun.Restore(db, save);
        }
        public static void ValidateAndBuild()
        {
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var go = new GameObject("Unit preview validation"); go.SetActive(false);
            var screen = go.AddComponent<ExpeditionScreen>(); Set(screen, "database", db);
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 19);
            var nameStyle = new GUIStyle { font = font, fontSize = 25, fontStyle = FontStyle.Bold, wordWrap = true };
            var bodyStyle = new GUIStyle { font = font, fontSize = 19, wordWrap = true };
            try
            {
                Directory.CreateDirectory("Logs/v041-fixtures");
                foreach (CatalogForm form in db.All)
                {
                    string branch = form.Id.StartsWith("E-") ? form.Id : null;
                    if (form.Id.StartsWith("F-"))
                        foreach (CatalogForm candidate in db.All)
                            if (candidate.Id.StartsWith("E-" + CatalogRoster.FamilyCode(form.Id))) { branch = candidate.Id; break; }
                    var run = Fixture(db, form.Id, branch, null); Set(screen, "run", run);
                    string before = JsonUtility.ToJson(run.Capture());
                    var preview = (ExpeditionUnitPreview)Call(screen, "UnitPreview", run.Roster.Find("ally-01"), null, null);
                    Check(preview != null, "missing preview " + form.Id);
                    Check(ReferenceEquals(preview, Call(screen, "UnitPreview", run.Roster.Find("ally-01"), null, null)), "uncached preview");
                    string skills = (string)Call(screen, "UnitSkills", preview.Member);
                    string effects = (string)Call(screen, "UnitEffects", preview.Member);
                    Check(skills.Contains(CatalogUnitInfo.Skill(form.Id)) && effects.Contains(CatalogUnitInfo.Trait(form.InnateTrait)), "missing details");
                    if (form.Id.StartsWith("F-")) Check(skills.Contains(CatalogUnitInfo.Skill(branch)), "final lost branch details");
                    Check(before == JsonUtility.ToJson(run.Capture()), "preview changed save");
                    Check(nameStyle.CalcHeight(new GUIContent("✓ " + form.Name), 342) <= 40, "choice title clipped " + form.Id);
                    Check(bodyStyle.CalcHeight(new GUIContent(CatalogUnitInfo.Role(form.Id)), 342) <= 31, "role clipped " + form.Id);
                    Check(Resources.Load<Texture2D>("Art/Units/v010/" + form.Id) != null, "portrait missing " + form.Id);
                }
                var baseRun = Fixture(db, "B-KN", null, null); Set(screen, "run", baseRun);
                var first = (ExpeditionUnitPreview)Call(screen, "UnitPreview", baseRun.Roster.Find("ally-01"), "E-KN-G", null);
                var second = (ExpeditionUnitPreview)Call(screen, "UnitPreview", baseRun.Roster.Find("ally-01"), "E-KN-C", null);
                Check(first.Member.FormId != second.Member.FormId && baseRun.Roster.Find("ally-01").FormId == "B-KN", "branch selection mutates source");
                File.WriteAllText("Logs/v041-fixtures/evolution.json", JsonUtility.ToJson(baseRun.Capture()));
                File.WriteAllText("Logs/v041-fixtures/final.json", JsonUtility.ToJson(Fixture(db, "E-PR-H", "E-PR-H", null).Capture()));
                string[][] pairs = { new[]{"E-KN-G","E-FI-B"}, new[]{"E-KN-C","E-PR-G"}, new[]{"E-AR-P","E-IC-F"},
                    new[]{"E-FI-E","E-IC-S"}, new[]{"E-PR-H","E-BA-A"}, new[]{"E-AR-T","E-BA-G"} };
                for (int i = 0; i < pairs.Length; i++)
                    File.WriteAllText("Logs/v041-fixtures/hybrid-" + (i + 1) + ".json", JsonUtility.ToJson(Fixture(db, pairs[i][0], pairs[i][0], pairs[i][1]).Capture()));
                Debug.Log("PASS: Unity unit information, " + checks + " assertions; QA checkpoints exported (simulated victories, not balance evidence).");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(font); }
            PrototypeSetup.BuildAndroid();
        }
    }
}
