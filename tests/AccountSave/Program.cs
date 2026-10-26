using System;
using System.IO;
using System.Web.Script.Serialization;
using Expedition.Core;

class Program
{
    static int checks;
    static JavaScriptSerializer json = new JavaScriptSerializer();
    static CatalogDatabase db;
    static void Check(bool condition, string why) { checks++; if (!condition) throw new Exception(why); }
    static void Throws(Action action, string why) { bool threw = false; try { action(); } catch { threw = true; } Check(threw, why); }
    static void Validate(string text) { json.Deserialize<ExpeditionAccountSave>(text).Validate(db); }
    static string Snapshot(ExpeditionRun run, ExpeditionProgress progress, ExpeditionJournal journal)
    {
        progress.Observe(run); journal.Observe(run);
        return json.Serialize(new ExpeditionAccountSave { Version = 1, Run = run.Capture(), Progress = progress.Capture(),
            Journal = journal.Capture(), TutorialMask = 3, LessonMask = 7 });
    }
    static void Main(string[] args)
    {
        db = CatalogDatabase.FromJson(File.ReadAllText(args[0]));
        string root = args[1]; Directory.CreateDirectory(root);
        var run = ExpeditionRun.New(db, 44001); var progress = ExpeditionProgress.New(); var journal = ExpeditionJournal.New();
        string initial = Snapshot(run, progress, journal);
        var store = new ExpeditionSaveStore(Path.Combine(root, "journey"), Validate);
        Check(store.Load().State == SaveReadState.Empty, "fresh account"); store.Commit(initial);
        Check(store.Load().Payload == initial && store.Load().State == SaveReadState.Loaded, "initial redundant copies");
        int operations = 0;
        for (int wave = 1; wave <= 30; wave++)
        {
            run.BeginBattle(); store.Commit(Snapshot(run, progress, journal)); operations++;
            var saved = json.Deserialize<ExpeditionAccountSave>(store.Load().Payload);
            Check(ExpeditionRun.Restore(db, saved.Run).Phase == ExpeditionPhase.Battle, "battle checkpoint");
            run.ResolveBattle(true); store.Commit(Snapshot(run, progress, journal)); operations++;
            saved = json.Deserialize<ExpeditionAccountSave>(store.Load().Payload);
            var restored = ExpeditionRun.Restore(db, saved.Run);
            var restoredProgress = ExpeditionProgress.Restore(saved.Progress);
            int gold = restored.Gold, points = restoredProgress.ResearchPoints;
            Check(!restored.ResolveBattle(true) && restored.Gold == gold, "reload duplicates battle reward");
            Check(!restoredProgress.Observe(restored) && restoredProgress.ResearchPoints == points, "reload duplicates research points");
            run.Continue(); store.Commit(Snapshot(run, progress, journal)); operations++;
            Check(store.Load().State == SaveReadState.Loaded, "checkpoint reload");
        }
        Check(progress.PurchaseResearch("tactical-start"), "research purchase");
        store.Commit(Snapshot(run, progress, journal));
        Check(json.Deserialize<ExpeditionAccountSave>(store.Load().Payload).Progress.ResearchIds.Length == 1, "research persists with run");
        var invalid = json.Deserialize<ExpeditionAccountSave>(store.Load().Payload);
        invalid.Progress = ExpeditionProgress.New().Capture();
        Throws(() => Validate(json.Serialize(invalid)), "mixed checkpoint accepted");
        invalid = json.Deserialize<ExpeditionAccountSave>(initial); invalid.TutorialMask = 4;
        Throws(() => Validate(json.Serialize(invalid)), "invalid guide mask");

        for (int cut = 0; cut < 5; cut++)
        {
            string dir = Path.Combine(root, "torn-" + cut); var s = new ExpeditionSaveStore(dir, Validate);
            s.Commit(initial); s.Commit(Snapshot(run, progress, journal));
            var newest = s.Load(); byte[] bytes = File.ReadAllBytes(newest.Source);
            int length = new[] { 0, 8, 30, bytes.Length / 2, bytes.Length - 1 }[cut];
            using (var f = new FileStream(newest.Source, FileMode.Create)) f.Write(bytes, 0, length);
            Check(s.Load().State == SaveReadState.Recovered && s.Load().Payload == initial, "torn write recovery " + cut);
            s.Commit(initial);
            Check(s.Load().State == SaveReadState.Loaded && File.Exists(newest.Source + ".damaged"), "repair and retain damaged bytes");
        }
        string badDir = Path.Combine(root, "both-bad"); var bad = new ExpeditionSaveStore(badDir, Validate); bad.Commit(initial);
        File.WriteAllText(Path.Combine(badDir, "account-a.sav"), "broken A"); File.WriteAllText(Path.Combine(badDir, "account-b.sav"), "broken B");
        Check(bad.Load().State == SaveReadState.Damaged, "both damaged");
        Throws(() => bad.Commit(initial), "silently replaced damaged account");
        Check(File.ReadAllText(Path.Combine(badDir, "account-a.sav")) == "broken A", "damaged original preserved");
        string futureDir = Path.Combine(root, "future"); var future = new ExpeditionSaveStore(futureDir, Validate); future.Commit(initial);
        var futureData = json.Deserialize<ExpeditionAccountSave>(initial); futureData.Version = 2;
        new ExpeditionSaveStore(futureDir, text => { }).Commit(json.Serialize(futureData));
        Check(future.Load().State == SaveReadState.FutureVersion, "future version rolled back");
        Throws(() => future.Commit(initial), "future version overwritten");
        string semanticDir = Path.Combine(root, "semantic"); var semantic = new ExpeditionSaveStore(semanticDir, Validate); semantic.Commit(initial);
        new ExpeditionSaveStore(semanticDir, text => { }).Commit(json.Serialize(invalid));
        Check(semantic.Load().State == SaveReadState.Recovered && semantic.Load().Payload == initial, "valid checksum but invalid content");
        string missingDir = Path.Combine(root, "missing"); var missing = new ExpeditionSaveStore(missingDir, Validate); missing.Commit(initial);
        File.Delete(missing.Load().Source); Check(missing.Load().Payload == initial && missing.Load().State == SaveReadState.Recovered, "missing slot");
        foreach (float v in new[] { 0f, .5f, 1f }) { var p = new ExpeditionPreferences { SoundVolume = v }; p.Validate(); Check(json.Deserialize<ExpeditionPreferences>(json.Serialize(p)).SoundVolume == v, "volume roundtrip"); }
        foreach (float v in new[] { -1f, 2f, float.NaN, float.PositiveInfinity }) Throws(() => new ExpeditionPreferences { SoundVolume = v }.Validate(), "bad volume");
        Console.WriteLine("PASS account saving: " + checks + " checks, " + operations + " journey checkpoints, torn/corrupt/future-version recovery and preference boundaries.");
    }
}
