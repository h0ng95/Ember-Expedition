using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Expedition.Core;

static class Program
{
    static int checks;
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 4000000 };
    static void Check(bool ok, string reason) { checks++; if (!ok) throw new Exception(reason); }
    static void English(string text, string context)
    { Check(!string.IsNullOrEmpty(text) && !Regex.IsMatch(text, "[가-힣]") && !text.Contains("text.0"), context + ": " + text); }
    static string Capture(ExpeditionRun run) { return Json.Serialize(run.Capture()); }
    static int Main(string[] args)
    {
        try
        {
            var db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
            var rows = (System.Collections.IList)Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(args[1]))["entries"];
            Check(CatalogText.Count == rows.Count, "Source and runtime table counts match");
            foreach (Dictionary<string, object> row in rows)
            {
                string key = (string)row["key"];
                CatalogText.SetLanguage("ko"); Check(CatalogText.Get(key) == (string)row["ko"], "Korean source " + key);
                CatalogText.SetLanguage("en"); Check(CatalogText.Get(key) == (string)row["en"], "English source " + key);
                English(CatalogText.Get(key), key);
            }
            foreach (string locale in new[] { "ko", "ko-KR", "Korean", "KO-kr" })
                Check(CatalogText.ResolveLanguage(null, locale) == "ko", "Korean auto detection");
            foreach (string locale in new[] { "en-US", "Japanese", "French", "", null })
                Check(CatalogText.ResolveLanguage("auto", locale) == "en", "English fallback");
            Check(CatalogText.ResolveLanguage("ko", "English") == "ko" && CatalogText.ResolveLanguage("en", "Korean") == "en", "Manual override");
            var old = Json.Deserialize<ExpeditionPreferences>("{\"Version\":1,\"SoundVolume\":0.4,\"ReduceMotion\":true,\"ReduceFlashes\":true}");
            old.Validate(); Check(old.SoundVolume == .4f && old.ReduceMotion && old.ReduceFlashes, "Legacy preferences preserved");
            foreach (string language in new[] { null, "", "auto", "ko", "en" })
            { old.Language = language; old.Validate(); var round = Json.Deserialize<ExpeditionPreferences>(Json.Serialize(old)); Check(round.Language == language && round.ReduceFlashes && round.SoundVolume == .4f, "Preference round trip"); }
            bool rejected = false; try { new ExpeditionPreferences { Language = "invalid" }.Validate(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Unknown saved preference rejected for backup recovery");

            // Initialize all static content in Korean, then switch without reloading the objects.
            CatalogText.SetLanguage("ko");
            string story = ExpeditionStory.Get("garden-clear").Body;
            string node = CatalogResearch.Nodes[0].Title;
            string line = ExpeditionDialogue.ForBoss(30)[0].Text;
            var labels = new CatalogLocalizedStrings("text.0580", "text.0594");
            var run = ExpeditionRun.New(db, 69001);
            var frozen = Capture(run);
            for (int cycle = 0; cycle < 5; cycle++)
            {
                CatalogText.SetLanguage("en");
                English(ExpeditionStory.Get("garden-clear").Body, "Static story refresh");
                English(CatalogResearch.Nodes[0].Title, "Static research refresh");
                English(ExpeditionDialogue.ForBoss(30)[0].Text, "Static dialogue refresh");
                English(labels[0], "Static string array refresh");
                English(run.EncounterName, "Cached run encounter");
                foreach (var form in db.All)
                {
                    English(form.Name, form.Id + " name"); English(form.Tier, form.Id + " tier"); English(form.Family, form.Id + " family");
                    foreach (string text in new[] { CatalogUnitInfo.Role(form.Id), CatalogUnitInfo.Placement(form.Id), CatalogUnitInfo.Summary(form.Id),
                        CatalogUnitInfo.Decision(form.Id), CatalogUnitInfo.Skill(form.Id), CatalogUnitInfo.Trait(form.InnateTrait) }) English(text, form.Id);
                }
                foreach (var entry in ExpeditionStory.Entries) { English(entry.Title, entry.Id); English(entry.Body, entry.Id); English(entry.Excerpt, entry.Id); }
                foreach (var research in CatalogResearch.Nodes) { English(research.Title, research.Id); English(research.DescriptionForRules(6), research.Id); }
                for (int wave = 5; wave <= 30; wave += 5) foreach (var speech in ExpeditionDialogue.ForBoss(wave)) English(speech.Text, "Dialogue " + wave);
                CatalogText.SetLanguage("ko");
                Check(story == ExpeditionStory.Get("garden-clear").Body && node == CatalogResearch.Nodes[0].Title && line == ExpeditionDialogue.ForBoss(30)[0].Text, "Korean restored");
                Check(Capture(run) == frozen, "Switching leaves complete run/RNG/undo untouched");
            }

            // Equal seeds, recruitment, research and combat must be locale-independent.
            for (uint seed = 1; seed <= 12; seed++)
            {
                CatalogText.SetLanguage("ko"); var a = ExpeditionRun.New(db, seed); var initial = a.Capture(); a.Summon();
                CatalogText.SetLanguage("en"); var b = ExpeditionRun.Restore(db, initial); b.Summon();
                Check(Capture(a) == Capture(b), "Recruitment RNG is locale-independent");
                var first = a.BeginBattle(); var second = b.BeginBattle();
                for (int step = 0; step < 300; step++)
                {
                    CatalogText.SetLanguage("ko"); first.Step(.25f);
                    CatalogText.SetLanguage(step % 2 == 0 ? "en" : "ko"); second.Step(.25f);
                }
                Check(first.Winner == second.Winner && first.Time == second.Time, "Combat result unchanged");
                foreach (var ally in first.Units)
                { var other = second.Get(ally.Id); Check(ally.Hp == other.Hp && ally.Shield == other.Shield && ally.X == other.X && ally.Y == other.Y, "Combat state unchanged"); }
                a.ResolveBattle(first.Winner == CatalogTeam.Ally); b.ResolveBattle(second.Winner == CatalogTeam.Ally);
                Check(Capture(a) == Capture(b), "Post-battle resources unchanged");
            }
            CatalogText.SetLanguage("ko");
            Console.WriteLine("PASS localization: " + checks + " checks; complete tables, live content switching, legacy preferences, 12 paired combat simulations.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
