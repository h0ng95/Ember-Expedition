using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Expedition.Core;

static class Program
{
    static int checks;
    static CatalogDatabase db;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static string Snapshot(ExpeditionRun run)
    {
        using (var writer = new StringWriter()) { new XmlSerializer(typeof(ExpeditionRunSave)).Serialize(writer, run.Capture()); return writer.ToString(); }
    }
    static ExpeditionRun Fixture(string form, string branch, string other, int effects)
    {
        var run = ExpeditionRun.New(db, 41001, ExpeditionDifficulty.Normal, effects == 0 ? null :
            new[] { "tactical-start", "tactical-vigor", "ember-start", "ember-insight", "frost-start", "frost-focus" });
        while (run.Wave < 15) { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
        if (effects == 1) { run.PurchaseTactic("bulwark"); run.PurchaseTactic("shieldcraft"); }
        if (effects == 2) { run.PurchaseTactic("marksmanship"); run.PurchaseTactic("spellcraft"); run.PurchaseTactic("cinders"); }
        var save = run.Capture();
        save.Roster.Members[0].FormId = form; save.Roster.Members[0].BranchId = branch;
        save.Roster.Members[0].Growth = form.StartsWith("B-") ? 2 : form.StartsWith("E-") ? 6 : 0;
        save.Roster.Members[0].InheritedTraitId = "T-PR";
        if (other != null) { save.Roster.Members[1].FormId = other; save.Roster.Members[1].BranchId = other; save.Roster.Members[1].InheritedTraitId = "T-IC"; }
        return ExpeditionRun.Restore(db, save);
    }
    static void Verify(ExpeditionRun run, string result, string donor)
    {
        string before = Snapshot(run);
        var preview = ExpeditionUnitPreview.Create(db, run, "ally-01", result, donor);
        Check(preview != null && before == Snapshot(run), "Preview mutated source or failed: " + result);
        var actual = ExpeditionRun.Restore(db, run.Capture());
        if (result != null) Check(donor == null ? actual.Evolve("ally-01", result) : actual.Combine("ally-01", donor, result), "Expected legal operation");
        var member = actual.Roster.Find("ally-01");
        var battle = actual.BeginBattle();
        // The result may remain benched, so create its stats with the same battle factory.
        var expected = new CatalogBattle(db, actual.TacticIds, actual.LegacyResearchIds, actual.RulesVersion).AddUnit("expected", CatalogTeam.Ally,
            member.FormId, member.BranchId, member.InheritedTraitId, 0, 0);
        Check(preview.Stats.MaxHp == expected.MaxHp && preview.Stats.Power == expected.Power &&
            preview.Stats.Defense == expected.Defense && preview.Stats.AttackInterval == expected.AttackInterval &&
            preview.Stats.Range == expected.Range, "Stats differ from real combat: " + result);
        Check(preview.Member.BranchId == member.BranchId && preview.Member.InheritedTraitId == member.InheritedTraitId &&
            preview.DeployedCount == actual.DeployedIds.Count && preview.RosterCount == actual.Roster.Members.Count,
            "Projected traits/branch/deployment differ: " + result);
        if (actual.DeployedIds.Contains(member.Id)) Check(battle.Get(member.Id).MaxHp == preview.Stats.MaxHp, "Deployed battle stat mismatch");
        Check(before == Snapshot(run), "Source/RNG/Undo changed after comparison");
    }
    static int Main(string[] args)
    {
        try
        {
            db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
            foreach (var form in db.All)
            {
                Check(!string.IsNullOrEmpty(CatalogUnitInfo.Role(form.Id)) && !string.IsNullOrEmpty(CatalogUnitInfo.Placement(form.Id)) &&
                    !string.IsNullOrEmpty(CatalogUnitInfo.Skill(form.Id)) && !string.IsNullOrEmpty(CatalogUnitInfo.Trait(form.InnateTrait)), "Missing unit information");
                string branch = form.Id.StartsWith("F-") ? db.All.First(f => f.Id.StartsWith("E-" + CatalogRoster.FamilyCode(form.Id))).Id :
                    form.Id.StartsWith("E-") ? form.Id : null;
                for (int e = 0; e < 3; e++) Verify(Fixture(form.Id, branch, null, e), null, null);
            }
            foreach (var branch in db.All.Where(f => f.Id.StartsWith("E-")))
                for (int e = 0; e < 3; e++)
                {
                    Verify(Fixture("B-" + CatalogRoster.FamilyCode(branch.Id), null, null, e), branch.Id, null);
                    Verify(Fixture(branch.Id, branch.Id, null, e), "F-" + CatalogRoster.FamilyCode(branch.Id), null);
                }
            string[][] pairs = { new[]{"E-KN-G","E-FI-B"}, new[]{"E-KN-C","E-PR-G"}, new[]{"E-AR-P","E-IC-F"},
                new[]{"E-FI-E","E-IC-S"}, new[]{"E-PR-H","E-BA-A"}, new[]{"E-AR-T","E-BA-G"} };
            for (int h = 0; h < 6; h++) for (int direction = 0; direction < 2; direction++)
                for (int deployment = 0; deployment < 4; deployment++) for (int e = 0; e < 3; e++)
                {
                    var run = Fixture(pairs[h][direction], pairs[h][direction], pairs[h][1-direction], e);
                    if ((deployment & 1) != 0) run.SetDeployed("ally-01", false);
                    if ((deployment & 2) != 0) run.SetDeployed("ally-02", false);
                    Verify(run, "H-" + (h + 1).ToString("00"), "ally-02");
                    run.SetMaterialLocked("ally-02", true);
                    Check(ExpeditionUnitPreview.Create(db, run, "ally-01", "H-" + (h + 1).ToString("00"), "ally-02") == null, "Protected donor accepted");
                }
            var baseRun = ExpeditionRun.New(db, 41);
            for (int rules = 1; rules <= CatalogBalance.CurrentRules; rules++)
            {
                var saved = Fixture("E-FI-B", "E-FI-B", null, 2).Capture();
                saved.Version = rules + 8; saved.RulesVersion = rules;
                Verify(ExpeditionRun.Restore(db, saved), null, null);
            }
            Check(ExpeditionUnitPreview.Create(db, baseRun, "ally-01", "E-KN-G") == null, "Unready evolution accepted");
            Check(CatalogUnitInfo.Skill("E-IC-F").Contains("이동 속도") && !CatalogUnitInfo.Skill("E-IC-F").Contains("빙결"), "Slow incorrectly described as freeze");
            Check(CatalogUnitInfo.Skill("E-KN-C").Contains("2.5초") && CatalogUnitInfo.Trait("T-H05").Contains("자신에게"), "Conditional skill/trait description regressed");
            Console.WriteLine("PASS: unit information / projections, " + checks + " assertions; 306 stat and growth cases plus " + CatalogBalance.CurrentRules + " saved-rules cases.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}

