using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class AccountSettingsValidation
    {
        static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static int checks;
        static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception("Account/settings: " + why); }
        static object Get(object obj, string field) { return obj.GetType().GetField(field, Flags).GetValue(obj); }
        static void Set(object obj, string field, object value) { obj.GetType().GetField(field, Flags).SetValue(obj, value); }
        static object Call(object obj, string method, params object[] args) { return obj.GetType().GetMethod(method, Flags).Invoke(obj, args); }
        static ExpeditionScreen Load(GameObject go, CatalogDatabase db, string dir)
        {
            var screen = go.AddComponent<ExpeditionScreen>();
            Set(screen, "database", db); Set(screen, "playtest", go.AddComponent<PlaytestRecorder>());
            Call(screen, "InitializeAccountStorage", dir);
            Call(screen, "LoadJournal"); Call(screen, "LoadProgress"); Call(screen, "LoadSave");
            Call(screen, "LoadTutorial"); Call(screen, "LoadLessons"); Set(screen, "startupReady", true);
            return screen;
        }
        public static void ValidateAndBuild() { Validate(); PrototypeSetup.BuildAndroid(); }
        public static void Validate()
        {
            string[] keys = { "ember.expedition.run.v013", "ember.expedition.story.v025", "ember.expedition.progress.v027",
                "ember.expedition.tutorial.v036", "ember.expedition.lessons.v042", "ignis.preferences.v044", "ignis.preferences.v044.backup" };
            var old = new Dictionary<string, string>();
            for (int i = 0; i < keys.Length; i++) if (PlayerPrefs.HasKey(keys[i])) old[keys[i]] = i == 3 || i == 4 ? PlayerPrefs.GetInt(keys[i]).ToString() : PlayerPrefs.GetString(keys[i]);
            var objects = new List<GameObject>();
            Font font = null;
            try
            {
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
                var run = ExpeditionRun.NewLegacy(db, 44006);
                while (run.Wave < 6) { run.BeginBattle(); run.ResolveBattle(true); run.Continue(); }
                var progress = ExpeditionProgress.Restore(new ExpeditionProgressSave { Version = 2, BestWave = 5, ResearchIds = new[] { "tactical-start" } });
                var journal = ExpeditionJournal.New(); journal.Observe(run);
                PlayerPrefs.SetString(keys[0], JsonUtility.ToJson(run.Capture()));
                PlayerPrefs.SetString(keys[1], JsonUtility.ToJson(journal.Capture()));
                PlayerPrefs.SetString(keys[2], JsonUtility.ToJson(progress.Capture()));
                PlayerPrefs.SetInt(keys[3], 1); PlayerPrefs.SetInt(keys[4], 2);
                string root = Path.GetFullPath("Logs/v044-" + Guid.NewGuid().ToString("N"));
                Func<string, ExpeditionScreen> load = dir => { var go = new GameObject("Account QA"); go.SetActive(false); objects.Add(go); return Load(go, db, dir); };
                var screen = load(root); Call(screen, "FinalizeAccountLoad");
                var store = (ExpeditionSaveStore)Get(screen, "accountStore");
                if (store.Load().State != SaveReadState.Loaded) Call(screen, "PersistAccount");
                Check(store.Load().State == SaveReadState.Loaded, "migration redundant files");
                Check(((ExpeditionRun)Get(screen, "run")).Wave == 6 && ((ExpeditionProgress)Get(screen, "progress")).ResearchIds.Count == 1, "legacy run/research migration");
                Check((int)Get(screen, "tutorialMask") == 1 && (int)Get(screen, "lessonMask") == 2, "guide migration");
                Check(PlayerPrefs.GetString(keys[0]) == JsonUtility.ToJson(run.Capture()), "legacy source changed");
                var active = (ExpeditionRun)Get(screen, "run"); active.BeginBattle(); active.ResolveBattle(true);
                Call(screen, "Save", "battle_win", null, 12f);
                var complete = JsonUtility.FromJson<ExpeditionAccountSave>(store.Load().Payload); complete.Validate(db);
                Check(complete.Run.Victories == 6 && complete.Progress.BestWave == 6, "atomic victory/progress");
                int points = ExpeditionProgress.Restore(complete.Progress).ResearchPoints;
                var reloaded = load(root);
                Check(((ExpeditionProgress)Get(reloaded, "progress")).ResearchPoints == points, "duplicate research reward");
                Check(((ExpeditionRun)Get(reloaded, "run")).Gold == active.Gold, "gold after reload");
                File.WriteAllText(store.Load().Source, "truncated checkpoint");
                var recovered = load(root);
                Check(!(bool)Get(recovered, "badSave") && ((string)Get(recovered, "error")).Contains("복구"), "recovery notice");
                Call(recovered, "FinalizeAccountLoad");
                Check(store.Load().State == SaveReadState.Loaded, "repair redundancy");
                File.WriteAllText(Path.Combine(root, "account-a.sav"), "broken A");
                File.WriteAllText(Path.Combine(root, "account-b.sav"), "broken B");
                var blocked = load(root); Call(blocked, "NewRun");
                Check((bool)Get(blocked, "storageBlocked") && Get(blocked, "run") == null, "damaged new files silently fall back to stale PlayerPrefs");
                Check(File.ReadAllText(Path.Combine(root, "account-a.sav")) == "broken A", "blocked new run erased data");
                PlayerPrefs.SetString(keys[2], "{");
                var badLegacy = load(root + "-legacy"); Call(badLegacy, "FinalizeAccountLoad");
                Check((bool)Get(badLegacy, "storageBlocked") && !Directory.Exists(root + "-legacy"), "bad legacy progress migrated as empty");
                GamePreferences.Load(); GamePreferences.Defaults(); GamePreferences.Current.SoundVolume = .3f;
                GamePreferences.Current.ReduceMotion = GamePreferences.Current.ReduceFlashes = true;
                Check(GamePreferences.TrySave(), "preference write");
                typeof(GamePreferences).GetField("loaded", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
                GamePreferences.Load();
                Check(Mathf.Abs(AudioListener.volume - .3f) < .001f && GamePreferences.Current.ReduceFlashes && GamePreferences.Current.ReduceMotion, "preference reload/application");
                GamePreferences.Current.SoundVolume = .7f; GamePreferences.TrySave(); PlayerPrefs.SetString(keys[5], "{");
                typeof(GamePreferences).GetField("loaded", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
                GamePreferences.Load(); Check(Mathf.Abs(GamePreferences.Current.SoundVolume - .3f) < .001f, "preference backup fallback");
                font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);
                var text = new GUIStyle { font = font, fontSize = 16, wordWrap = true };
                foreach (string label in new[] { "공격에 맞았을 때 유닛이 흔들리는 움직임을 줄입니다.", "피격 번쩍임과 공격 효과 밝기를 낮춥니다.\n공격 범위 예고와 상태 표시는 유지합니다." })
                    Check(text.CalcHeight(new GUIContent(label), 540) <= 48, "settings description clipped");
                Check(GameSettingsPanel.Window.center == new Vector2(640, 360), "settings not centered");
                Debug.Log("PASS account/settings Unity integration: " + checks + " checks; migration, reward boundaries, fallback, damage preservation, preferences and text layout.");
            }
            finally
            {
                foreach (var go in objects) UnityEngine.Object.DestroyImmediate(go);
                if (font != null) UnityEngine.Object.DestroyImmediate(font);
                for (int i = 0; i < keys.Length; i++)
                {
                    if (!old.ContainsKey(keys[i])) PlayerPrefs.DeleteKey(keys[i]);
                    else if (i == 3 || i == 4) PlayerPrefs.SetInt(keys[i], int.Parse(old[keys[i]]));
                    else PlayerPrefs.SetString(keys[i], old[keys[i]]);
                }
                PlayerPrefs.Save();
                typeof(GamePreferences).GetField("loaded", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
                GamePreferences.Load();
            }
            BattleReadabilityValidation.Validate();

        }
    }
}
