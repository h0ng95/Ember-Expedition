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
    // Explicit one-shot review. Replays real legal checkpoints offscreen and uses the production result UI.
    // Save writes are blocked throughout and the original screen state is restored in finally.
    public sealed class MidgameReportAudit : MonoBehaviour
    {
        static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        bool manual, retry;
        static object Get(object screen, string field) { return typeof(ExpeditionScreen).GetField(field, Hidden).GetValue(screen); }
        static void Set(object screen, string field, object value) { typeof(ExpeditionScreen).GetField(field, Hidden).SetValue(screen, value); }
        static void Modal(object screen, string value)
        { var field = typeof(ExpeditionScreen).GetField("modal", Hidden); field.SetValue(screen, Enum.Parse(field.FieldType, value)); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            string flag = Path.Combine(Application.persistentDataPath, "midgame-report-audit.flag");
            if (!File.Exists(flag)) return;
            string mode=File.ReadAllText(flag).Trim(); File.Delete(flag);
            var go = new GameObject("Explicit midgame report QA"); DontDestroyOnLoad(go);
            var audit=go.AddComponent<MidgameReportAudit>(); audit.manual=mode=="manual"||mode=="retry-manual";
            audit.retry=mode.StartsWith("retry");
        }
        IEnumerator Start()
        {
            var screen = FindFirstObjectByType<ExpeditionScreen>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (screen != null && !(bool)Get(screen, "startupReady") && Time.realtimeSinceStartup < deadline) yield return null;
            if (screen == null || !(bool)Get(screen, "startupReady"))
            { Debug.LogError("MIDGAME_AUDIT startup timeout"); Destroy(gameObject); yield break; }
            var saved = new Dictionary<string, object>();
            foreach (string field in new[] { "run", "home", "modal", "badSave", "storageBlocked", "battleReport", "battleReportRun", "battleReportComplete", "tutorialMask", "lessonMask",
                "retryReportRun", "retryReportWave", "selectedId", "preparationSelectionId", "preparationOwner", "preparationForm", "preparationBranch", "previewFinalSkill" })
                saved[field] = Get(screen, field);
            string directory = Path.Combine(Application.persistentDataPath, "MidgameReportAudit");
            var db = (CatalogDatabase)Get(screen, "database");
            CatalogBattleReport report = null; int captures = 0;
            try
            {
                Set(screen, "badSave", true); Set(screen, "storageBlocked", true);
                Set(screen, "home", false); Set(screen, "tutorialMask", 3); Set(screen, "lessonMask", 15);
                foreach (string name in new[] { "legal-15-timeout", "legal-18-defeat", "legal-19-defeat", "legal-18-win" })
                {
                    var run = ExpeditionRun.Restore(db, JsonUtility.FromJson<ExpeditionRunSave>(File.ReadAllText(Path.Combine(directory, name + ".json"))));
                    var battle = run.BeginBattle(); report = new CatalogBattleReport(battle);
                    while (!battle.Ended && battle.Time < 75f) battle.Step(battle.Time < 74.75f ? .25f : 1f / 60f);
                    bool won = battle.Ended && battle.Winner == CatalogTeam.Ally;
                    run.ResolveBattle(won); report.Complete(won);
                    Set(screen, "run", run); Set(screen, "battleReportRun", run); Set(screen, "battleReport", report);
                    Set(screen, "battleReportComplete", true); Modal(screen, "None");
                    yield return null; yield return new WaitForEndOfFrame(); Capture(directory, name + "-result"); captures++;
                    if (retry && manual)
                    {
                        string target=report.FirstLostUnitId;
                        Debug.Log("RETRY_AUDIT_OPEN_READY"); deadline=Time.realtimeSinceStartup+180;
                        while(Get(screen,"modal").ToString()!="BattleReport"&&Time.realtimeSinceStartup<deadline)yield return null;
                        if(Get(screen,"modal").ToString()!="BattleReport")throw new Exception("Retry report opening timed out");
                        yield return new WaitForEndOfFrame();Capture(directory,"retry-touch-report");
                        Debug.Log("RETRY_AUDIT_FOCUS_READY");
                        while(run.Phase!=ExpeditionPhase.Preparation&&Time.realtimeSinceStartup<deadline)yield return null;
                        if(run.Phase!=ExpeditionPhase.Preparation||(string)Get(screen,"preparationSelectionId")!=target)throw new Exception("Retry focus failed");
                        yield return new WaitForEndOfFrame();Capture(directory,"retry-touch-first");
                        Debug.Log("RETRY_AUDIT_REOPEN_READY");
                        while(Get(screen,"modal").ToString()!="BattleReport"&&Time.realtimeSinceStartup<deadline)yield return null;
                        if(Get(screen,"modal").ToString()!="BattleReport")throw new Exception("Previous report reopening failed");
                        Debug.Log("RETRY_AUDIT_SECOND_READY");
                        while((Get(screen,"modal").ToString()!="None"||(string)Get(screen,"preparationSelectionId")!=report.Allies[2].Id)&&Time.realtimeSinceStartup<deadline)yield return null;
                        if((string)Get(screen,"preparationSelectionId")!=report.Allies[2].Id)throw new Exception("Second ally selection failed");
                        yield return new WaitForEndOfFrame();Capture(directory,"retry-touch-support");
                        File.WriteAllText(Path.Combine(directory,"retry-touch.txt"),"PASS result -> report -> first loss -> previous report -> healer focus");
                        Debug.Log("RETRY_AUDIT_TOUCH_PASS");break;
                    }
                    if (manual)
                    {
                        Debug.Log("MIDGAME_AUDIT_TOUCH_READY " + name);
                        deadline = Time.realtimeSinceStartup + 120;
                        while (Get(screen, "modal").ToString() != "BattleReport" && Time.realtimeSinceStartup < deadline) yield return null;
                        if (Get(screen, "modal").ToString() != "BattleReport") throw new Exception("Report button touch timed out");
                        yield return new WaitForEndOfFrame(); Capture(directory, "touch-report");
                        while (Get(screen, "modal").ToString() == "BattleReport" && Time.realtimeSinceStartup < deadline) yield return null;
                        if (Get(screen, "modal").ToString() != "None") throw new Exception("Report close touch timed out");
                        Debug.Log("MIDGAME_AUDIT_TOUCH_PASS"); break;
                    }
                    Modal(screen, "BattleReport");
                    yield return null; yield return new WaitForEndOfFrame(); Capture(directory, name + "-report"); captures++;
                    if(retry&&!won)
                    {
                        string id=report.FirstLostUnitId??report.Allies[0].Id;
                        if(!(bool)typeof(ExpeditionScreen).GetMethod("PrepareRetryFor",Hidden).Invoke(screen,new object[]{id}))throw new Exception("Retry focus missing");
                        yield return null;yield return new WaitForEndOfFrame();Capture(directory,name+"-focus");captures++;
                        Modal(screen,"BattleReport");yield return null;yield return new WaitForEndOfFrame();Capture(directory,name+"-reopened");captures++;
                        typeof(ExpeditionScreen).GetMethod("PrepareRetryFor",Hidden).Invoke(screen,new object[]{report.Allies[2].Id});
                        yield return null;yield return new WaitForEndOfFrame();Capture(directory,name+"-support");captures++;
                    }
                    Set(screen, "battleReportComplete", false); Modal(screen, "BattleReport");
                    if (name == "legal-15-timeout")
                    { yield return null; yield return new WaitForEndOfFrame(); Capture(directory, "restored-no-evidence"); captures++; }
                    report.Dispose(); report = null;
                }
                File.WriteAllText(Path.Combine(directory, manual ? "touch.txt" : "result.txt"), "PASS captures=" + captures);
                Debug.Log("MIDGAME_AUDIT_PASS " + captures);
            }
            finally
            {
                if (report != null) report.Dispose();
                foreach (var entry in saved) Set(screen, entry.Key, entry.Value);
                Destroy(gameObject);
            }
        }
        static void Capture(string directory, string name)
        {
            var capture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); capture.Apply();
            File.WriteAllBytes(Path.Combine(directory, name + ".rgb"), capture.GetRawTextureData());
            File.WriteAllText(Path.Combine(directory, "capture-size.txt"), Screen.width + "," + Screen.height);
            Destroy(capture);
        }
    }
}
#endif
