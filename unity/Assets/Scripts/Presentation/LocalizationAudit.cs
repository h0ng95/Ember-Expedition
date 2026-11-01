#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // Explicit development-only visual fixtures. These do not measure battle balance.
    // Run/account writes are blocked and the caller backs up device preferences before using touch mode.
    public sealed class LocalizationAudit : MonoBehaviour
    {
        static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        string mode, directory;
        readonly List<string> results = new List<string>();
        int captures, screens;
        static object Get(object o, string key) { return o.GetType().GetField(key, Hidden).GetValue(o); }
        static void Set(object o, string key, object value)
        { var f = o.GetType().GetField(key, Hidden); f.SetValue(o, f.FieldType.IsEnum && value is string ? Enum.Parse(f.FieldType, (string)value) : value); }
        static object Call(object o, string key, params object[] args) { return o.GetType().GetMethod(key, Hidden).Invoke(o, args); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            string flag = Path.Combine(Application.persistentDataPath, "localization-audit.flag");
            if (!File.Exists(flag)) return;
            string requested = File.ReadAllText(flag).Trim(); File.Delete(flag);
            var go = new GameObject("Explicit localization QA"); DontDestroyOnLoad(go);
            go.AddComponent<LocalizationAudit>().mode = requested;
        }
        public static ExpeditionRun Fixture(CatalogDatabase db, int wave, string[] forms = null)
        {
            var run = ExpeditionRun.New(db, 69001);
            run.SetDeployed(run.Summon(), true); run.SetDeployed(run.Summon(), true);
            while (run.Wave < wave) { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
            if (forms == null) forms = new[] { "F-KN", "H-06", "F-PR", "F-FI", "F-BA" };
            var save = run.Capture();
            for (int i = 0; i < forms.Length; i++)
            {
                var member = save.Roster.Members[i]; member.FormId = forms[i];
                member.BranchId = forms[i].StartsWith("E-") ? forms[i] : forms[i].StartsWith("F-") ?
                    forms[i] == "F-KN" ? "E-KN-G" : forms[i] == "F-PR" ? "E-PR-H" :
                    forms[i] == "F-FI" ? "E-FI-E" : forms[i] == "F-BA" ? "E-BA-A" : forms[i] == "F-AR" ? "E-AR-P" : "E-IC-F" : null;
                member.Growth = forms[i].StartsWith("B-") ? 2 : forms[i].StartsWith("E-") ? 6 : 0;
            }
            save.UndoHistory = new ExpeditionPreparationSave[0];
            save.Roster.History = new CatalogRosterHistorySave[0];
            return ExpeditionRun.Restore(db, save);
        }
        IEnumerator CheckView(string name, bool capture)
        {
            LocalizedGui.Overflows.Clear();
            yield return null; yield return null; yield return new WaitForEndOfFrame();
            screens++;
            foreach (string overflow in LocalizedGui.Overflows) results.Add(name + " | " + overflow);
            if (capture)
            {
                var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(directory, name + ".rgb"), texture.GetRawTextureData()); Destroy(texture);
                File.WriteAllText(Path.Combine(directory, "capture-size.txt"), Screen.width + "," + Screen.height); captures++;
            }
        }
        IEnumerator Start()
        {
            var screen = FindFirstObjectByType<ExpeditionScreen>();
            float deadline = Time.realtimeSinceStartup + 45;
            while (screen != null && !(bool)Get(screen, "startupReady") && Time.realtimeSinceStartup < deadline) yield return null;
            if (screen == null || !(bool)Get(screen, "startupReady")) { Debug.LogError("LOCALIZATION_AUDIT startup timeout"); yield break; }
            directory = Path.Combine(Application.persistentDataPath, "LocalizationAudit"); Directory.CreateDirectory(directory);
            var db = (CatalogDatabase)Get(screen, "database");
            var saved = new Dictionary<string, object>();
            foreach (string field in new[] { "run", "home", "modal", "badSave", "storageBlocked", "tutorialMask", "lessonMask", "selectedId", "selectedStoryId",
                "journal", "recordHistory", "selectedRecord", "pendingAction", "pendingFormId", "pendingOtherId", "growthDetails", "growthPreviewKey", "infoTabIndex",
                "battleReport", "battleReportRun", "battleReportComplete", "selectedTacticNodeId" }) saved[field] = Get(screen, field);
            string language = GamePreferences.Current.Language;
            float volume = GamePreferences.Current.SoundVolume;
            CatalogCombatLab lab = null;
            try
            {
                Set(screen, "storageBlocked", true); Set(screen, "badSave", true);
                Set(screen, "tutorialMask", 3); Set(screen, "lessonMask", 15);
                LocalizedGui.Audit = true;
                if (mode == "touch" || mode == "verify")
                {
                    Set(screen, "home", true); Set(screen, "modal", mode == "touch" ? "Settings" : "None");
                    if (mode == "touch")
                    {
                        Debug.Log("LOCALIZATION_TOUCH_READY"); deadline = Time.realtimeSinceStartup + 180;
                        while (Get(screen, "modal").ToString() == "Settings" && Time.realtimeSinceStartup < deadline) yield return null;
                        if (GamePreferences.Current.Language != "en" || Get(screen, "modal").ToString() != "None") throw new Exception("Language touch/save failed");
                    }
                    else if (GamePreferences.Current.Language != "en" || CatalogText.Language != "en") throw new Exception("Language did not persist after restart");
                    yield return CheckView("en-" + mode, true);
                    File.WriteAllText(Path.Combine(directory, mode + ".txt"), "PASS English " + mode);
                    language = GamePreferences.Current.Language;
                    Debug.Log("LOCALIZATION_" + mode.ToUpperInvariant() + "_PASS"); yield break;
                }
                GamePreferences.Current.SoundVolume = 0;
                var journal = ExpeditionJournal.New();
                var full = Fixture(db, 30); full.BeginBattle(); full.ResolveBattle(true); journal.Observe(full);
                var history = new ExpeditionHistory(); history.Observe(full);
                Set(screen, "journal", journal); Set(screen, "recordHistory", history);
                foreach (string locale in new[] { "en", "ko" })
                {
                    GamePreferences.Current.Language = locale; GamePreferences.Apply();
                    Set(screen, "run", Fixture(db, 15)); Set(screen, "home", true); Set(screen, "modal", "None");
                    yield return CheckView(locale + "-home", true);
                    foreach (string modal in new[] { "Settings", "NewRun", "Records", "GuideMenu", "Progress" })
                    { Set(screen, "modal", modal); yield return CheckView(locale + "-" + modal, locale == "en" || modal == "Settings"); }
                    Set(screen, "modal", "Records"); Set(screen, "selectedRecord", history.Best);
                    yield return CheckView(locale + "-record-detail", locale == "en"); Set(screen, "selectedRecord", null);
                    Set(screen, "home", false); Set(screen, "modal", "None");
                    yield return CheckView(locale + "-preparation", true);
                    Set(screen, "modal", "Tactics");
                    foreach (var node in CatalogResearch.Nodes)
                    { Set(screen, "selectedTacticNodeId", node.Id); yield return CheckView(locale + "-research-" + node.Id, locale == "en" && node.Tier == 2); }
                    Set(screen, "modal", "Journal");
                    foreach (var entry in ExpeditionStory.Entries)
                    { Set(screen, "selectedStoryId", entry.Id); yield return CheckView(locale + "-story-" + entry.Id, locale == "en" && entry.Id == "palace-clear"); }
                    foreach (var form in db.All)
                    {
                        var run = Fixture(db, 15, new[] { form.Id, "H-06", "F-PR", "F-FI", "F-BA" });
                        Set(screen, "run", run); Set(screen, "selectedId", run.DeployedIds[0]); Set(screen, "modal", "Detail");
                        for (int tab = 0; tab < 3; tab++)
                        { Set(screen, "infoTabIndex", tab); yield return CheckView(locale + "-detail-" + form.Id + "-" + tab, locale == "en" && form.Id == "H-06" && tab == 0); }
                        Set(screen, "modal", "None"); Call(screen, "SelectPreparation", run.DeployedIds[0]);
                        yield return CheckView(locale + "-range-" + form.Id, locale == "en" && (form.Id == "H-05" || form.Id == "E-KN-G"));
                    }
                    if (locale == "en") for (int wave = 1; wave <= 30; wave++)
                    { Set(screen, "run", Fixture(db, wave)); Set(screen, "modal", "None"); yield return CheckView("en-wave-" + wave, false); }
                    var fusion = Fixture(db, 15, new[] { "E-AR-T", "E-BA-G", "F-PR", "F-FI", "F-KN" });
                    Set(screen, "run", fusion); Set(screen, "selectedId", fusion.DeployedIds[0]);
                    Set(screen, "pendingOtherId", fusion.DeployedIds[1]); Set(screen, "pendingFormId", "H-06");
                    Set(screen, "pendingAction", "Combine"); Set(screen, "modal", "Confirm"); Set(screen, "growthPreviewKey", null);
                    yield return CheckView(locale + "-fusion", true);
                    Set(screen, "growthDetails", true); yield return CheckView(locale + "-fusion-details", locale == "en");
                    Set(screen, "run", ExpeditionRun.New(db, 69999)); Set(screen, "selectedId", null); Set(screen, "modal", "None"); Set(screen, "tutorialMask", 0);
                    yield return CheckView(locale + "-tutorial", true); Set(screen, "tutorialMask", 3);
                    var result = Fixture(db, 30); var battle = result.BeginBattle(); var report = new CatalogBattleReport(battle);
                    while (!battle.Ended && battle.Time < 75) battle.Step(.25f);
                    bool won = battle.Ended && battle.Winner == CatalogTeam.Ally; result.ResolveBattle(won); report.Complete(won);
                    Set(screen, "run", result); Set(screen, "battleReport", report); Set(screen, "battleReportRun", result); Set(screen, "battleReportComplete", true);
                    yield return CheckView(locale + "-result", locale == "en"); Set(screen, "modal", "BattleReport");
                    yield return CheckView(locale + "-report", true); Set(screen, "modal", "None"); report.Dispose(); Set(screen, "battleReportComplete", false);
                    Set(screen, "run", full); yield return CheckView(locale + "-summary", locale == "en");
                }
                GamePreferences.Current.Language = "en"; GamePreferences.Apply();
                var combatRun = Fixture(db, 30); var combat = combatRun.BeginBattle();
                screen.enabled = false;
                lab = screen.GetComponent<CatalogCombatLab>(); if (lab == null) lab = screen.gameObject.AddComponent<CatalogCombatLab>();
                lab.OpenExpeditionBattle(combat, 30, 30, combatRun.EncounterName, won => { }, () => { });
                yield return new WaitForSeconds(.7f); Call(lab, "SetPaused", true);
                yield return CheckView("en-battle-pause", true); Set(lab, "battleSettingsOpen", true);
                yield return CheckView("en-battle-settings", true); Set(lab, "battleSettingsOpen", false);
                Call(lab, "SetPaused", false); yield return CheckView("en-battle", true);
                lab.enabled = false; screen.enabled = true;
                foreach (int wave in new[] { 5, 10, 15, 20, 25, 30 })
                {
                    var sceneRun = Fixture(db, wave); var sceneBattle = sceneRun.BeginBattle();
                    // Presentation fixture only: finish enemies immediately, never counted as a balance win.
                    foreach (var unit in sceneBattle.Units) if (unit.Team == CatalogTeam.Enemy)
                        typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(unit, 0f, null);
                    sceneBattle.Step(.05f);
                    screen.enabled = false;
                    lab.OpenExpeditionBattle(sceneBattle, wave, 30, sceneRun.EncounterName, won => { }, () => { }, () => { });
                    deadline = Time.realtimeSinceStartup + 5;
                    while (!(bool)Get(lab, "dialogueActive") && Time.realtimeSinceStartup < deadline) yield return null;
                    if (!(bool)Get(lab, "dialogueActive")) throw new Exception("Dialogue did not open: " + wave);
                    Call(lab, "CompleteForestReveal"); Call(lab, "CompleteCastleReveal"); Call(lab, "CompleteRegionalReveal");
                    for (int line = 0; line < 4; line++)
                    {
                        Set(lab, "dialogueIndex", line); Call(lab, "SetDialogueSpeaker"); Set(lab, "dialogueAge", 100f);
                        yield return CheckView("en-dialogue-" + wave + "-" + line, line == 3);
                    }
                    lab.enabled = false; screen.enabled = true;
                }
                var ending = new SeasonOneEndingView(new[] { "F-KN", "H-06", "F-PR" }, () => { });
                object oldEnding = Get(screen, "ending"); Set(screen, "ending", ending);
                try { for (int page = 0; page < 3; page++) { ending.Tick(3f); yield return CheckView("en-ending-" + page, true); ending.Next(); } }
                finally { Set(screen, "ending", oldEnding); ending.Dispose(); }
                File.WriteAllLines(Path.Combine(directory, "overflows.txt"), results.ToArray());
                string status = results.Count == 0 ? "PASS" : "BOUNDS_ISSUES";
                File.WriteAllText(Path.Combine(directory, "result.txt"), status + " screens=" + screens + " captures=" + captures + " overflows=" + results.Count);
                Debug.Log("LOCALIZATION_AUDIT_" + status + " screens=" + screens + " overflows=" + results.Count);
            }
            finally
            {
                if (lab != null) lab.enabled = false;
                screen.enabled = true;
                foreach (var item in saved) Set(screen, item.Key, item.Value);
                GamePreferences.Current.Language = language; GamePreferences.Current.SoundVolume = volume; GamePreferences.Apply();
                LocalizedGui.Audit = false; Destroy(gameObject);
            }
        }
    }
}
#endif
