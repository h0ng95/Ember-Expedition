#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // One-shot development review. Uses isolated battles and never writes expedition/account saves.
    public sealed class ExpeditionDialogueAudit : MonoBehaviour
    {
        static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        bool manual;
        int manualWave = 5;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            string flag = Path.Combine(Application.persistentDataPath, "dialogue-audit.flag");
            if (!File.Exists(flag)) return;
            string mode = File.ReadAllText(flag).Trim();
            bool manual = mode == "manual" || mode.EndsWith("-manual");
            File.Delete(flag);
            var go = new GameObject("Explicit dialogue QA"); DontDestroyOnLoad(go);
            var audit = go.AddComponent<ExpeditionDialogueAudit>();
            audit.manual = manual; audit.manualWave = mode == "castle-manual" ? 10 : mode == "mine-manual" ? 15 :
                mode == "frost-manual" ? 20 : mode == "garden-manual" ? 25 : mode == "palace-manual" ? 30 : 5;
        }
        IEnumerator Start()
        {
            var screen = FindFirstObjectByType<ExpeditionScreen>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (screen != null && !(bool)typeof(ExpeditionScreen).GetField("startupReady", Hidden).GetValue(screen) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            if (screen == null || !(bool)typeof(ExpeditionScreen).GetField("startupReady", Hidden).GetValue(screen))
            { Debug.LogError("DIALOGUE_AUDIT startup did not complete"); Destroy(gameObject); yield break; }
            screen.enabled = false;
            string directory = Path.Combine(Application.persistentDataPath, "DialogueAudit");
            Directory.CreateDirectory(directory);
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            int completed = 0;
            foreach (int wave in manual ? new[] { manualWave } : new[] { 5, 10, 15, 20, 25, 30 })
            {
                var battle = new CatalogBattle(db);
                string[] forms = wave == 15 ? new[] { "B-AR" } : wave == 25 ?
                    new[] { "F-BA", "H-02", "F-PR" } : new[] { "B-KN", "B-AR", "B-PR" };
                for (int i = 0; i < forms.Length; i++)
                {
                    var unit = battle.AddUnit("qa-" + i, CatalogTeam.Ally, forms[i],
                        forms[i].StartsWith("F-") ? forms[i] == "F-BA" ? "E-BA-G" : "E-PR-H" : null,
                        null, -1.2f - i * .8f, (i - 1) * 1.2f);
                    if (wave == 10 && i == 0) typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(unit, 0f, null);
                }
                string boss = wave == 5 ? "beast-boss" : wave == 10 ? "golem-boss" : wave == 15 ?
                    "furnace-boss" : wave == 20 ? "frost-boss" : wave == 25 ? "garden-boss" : "palace-boss";
                CatalogEnemyRole role = wave == 5 ? CatalogEnemyRole.Beast : wave == 10 ? CatalogEnemyRole.Golem :
                    wave == 15 ? CatalogEnemyRole.Furnace : wave == 20 ? CatalogEnemyRole.FrostWarden :
                    wave == 25 ? CatalogEnemyRole.GardenLord : CatalogEnemyRole.CrownDrake;
                battle.AddEnemy(boss, 1, 1, 0, 100, 1, .01f, true, .5f, 0, role);
                int outcomes = 0; bool finished = false;
                var go = new GameObject("Dialogue audit " + wave); var lab = go.AddComponent<CatalogCombatLab>();
                lab.OpenExpeditionBattle(battle, wave, 30, "QA · 보스 " + wave,
                    won => { if (!won) throw new Exception("Dialogue QA lost"); outcomes++; }, () => {}, () => finished = true);
                lab.ConfigureExpeditionSpeed(wave % 10 == 0 ? 2 : 1, null);
                deadline = Time.realtimeSinceStartup + 25;
                while (!(bool)typeof(CatalogCombatLab).GetField("dialogueActive", Hidden).GetValue(lab) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                if (!(bool)typeof(CatalogCombatLab).GetField("dialogueActive", Hidden).GetValue(lab))
                { Debug.LogError("DIALOGUE_AUDIT scene missing " + wave); Destroy(go); break; }
                Debug.Log("DIALOGUE_AUDIT_READY " + wave);
                if (manual) { while (!finished) yield return null; }
                else
                {
                    // Let the real-time Ignis reveal finish before reviewing the spoken lines.
                    if (wave >= 5)
                    {
                        string prefix = "wave-" + wave;
                        yield return new WaitForSecondsRealtime(.2f);
                        yield return new WaitForEndOfFrame(); Capture(directory, prefix + "-beat-0");
                        yield return new WaitForSecondsRealtime(1.1f);
                        yield return new WaitForEndOfFrame(); Capture(directory, prefix + "-beat-1");
                        yield return new WaitForSecondsRealtime(2.7f);
                    }
                    else yield return new WaitForSecondsRealtime(1.1f);
                    int count = ExpeditionDialogue.ForBoss(wave).Count;
                    for (int line = 0; line < count; line++)
                    {
                        typeof(CatalogCombatLab).GetField("dialogueAge", Hidden).SetValue(lab, 100f);
                        yield return new WaitForEndOfFrame();
                        Capture(directory, "boss-" + wave + "-line-" + line);
                        typeof(CatalogCombatLab).GetMethod("NextDialogue", Hidden).Invoke(lab, null);
                        yield return null;
                    }
                }
                if (outcomes != 1 || !finished) Debug.LogError("DIALOGUE_AUDIT callback mismatch " + wave);
                else { completed++; Debug.Log("DIALOGUE_AUDIT_PASS " + wave + " outcome=1"); }
                Destroy(go); yield return null;
            }
            File.WriteAllText(Path.Combine(directory, manual ? "manual.txt" : "result.txt"), "completed=" + completed);
            screen.enabled = true;
            Destroy(gameObject);
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
