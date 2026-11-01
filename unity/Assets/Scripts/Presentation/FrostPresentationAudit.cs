#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    // One-shot, isolated visual QA. Never reads or writes expedition account state.
    public sealed class FrostPresentationAudit : MonoBehaviour
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            string flag = Path.Combine(Application.persistentDataPath, "frost-art-audit.flag");
            if (!File.Exists(flag)) return;
            File.Delete(flag);
            new GameObject("Explicit frost art QA").AddComponent<FrostPresentationAudit>();
        }
        IEnumerator Start()
        {
            var screen = FindFirstObjectByType<ExpeditionScreen>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (screen != null && !(bool)typeof(ExpeditionScreen).GetField("startupReady", Hidden).GetValue(screen) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            if (screen == null || !(bool)typeof(ExpeditionScreen).GetField("startupReady", Hidden).GetValue(screen))
            { Debug.LogError("FROST_ART_AUDIT startup failed"); Destroy(gameObject); yield break; }
            screen.enabled = false;
            bool originalMotion = GamePreferences.Current.ReduceMotion;
            var database = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            string directory = Path.Combine(Application.persistentDataPath, "FrostArtAudit");
            Directory.CreateDirectory(directory);
            for (int quiet = 0; quiet <= 1; quiet++) for (int speed = 1; speed <= 2; speed++)
            {
                GamePreferences.Current.ReduceMotion = quiet == 1;
                string name = "frost-" + speed + "x-" + (quiet == 1 ? "still" : "motion");
                var battle = new CatalogBattle(database);
                string[] forms = { "B-KN", "B-AR", "B-PR" };
                for (int i = 0; i < forms.Length; i++)
                {
                    var ally = battle.AddUnit("qa-" + i, CatalogTeam.Ally, forms[i], null, null,
                        -1.2f - i * .8f, (i - 1) * 1.2f);
                    typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(ally, 10000f, null);
                    typeof(CatalogBattleUnit).GetProperty("MaxHp").SetValue(ally, 10000f, null);
                }
                var boss = battle.AddEnemy("frost-boss", 100000, 5, 15, 1.8f, 1.4f, .55f,
                    true, 1.6f, 0f, CatalogEnemyRole.FrostWarden, true);
                int basics = 0, impacts = 0, results = 0, completions = 0;
                battle.Event += e => { if (e.SourceId != boss.Id) return;
                    if (e.Kind == CatalogEventKind.BasicHit) basics++;
                    if (e.Kind == CatalogEventKind.BossImpact) impacts++; };
                var go = new GameObject(name); var lab = go.AddComponent<CatalogCombatLab>();
                lab.OpenExpeditionBattle(battle, 20, 30, "QA · 서리 " + speed + "×",
                    won => { if (won) results++; }, () => {}, () => completions++);
                lab.ConfigureExpeditionSpeed(speed, null);
                Debug.Log("FROST_ART_AUDIT_BEGIN " + name);
                yield return new WaitForSecondsRealtime(4f / speed);
                yield return new WaitForEndOfFrame(); Capture(directory, name + "-battle");
                yield return new WaitForSecondsRealtime(4f / speed);
                typeof(CatalogBattleUnit).GetProperty("Hp").SetValue(boss, 0f, null);
                yield return null; yield return new WaitForEndOfFrame(); Capture(directory, name + "-fall-0");
                for (int pose = 1; pose < 4; pose++)
                { yield return new WaitForSecondsRealtime(.26f); yield return new WaitForEndOfFrame(); Capture(directory, name + "-fall-" + pose); }
                yield return new WaitForSecondsRealtime(3.2f);
                yield return new WaitForEndOfFrame(); Capture(directory, name + "-dialogue");
                int rendered = -1;
                foreach (object view in (IList)typeof(CatalogCombatLab).GetField("combatViews", Hidden).GetValue(lab))
                    if (((CatalogBattleUnit)view.GetType().GetField("Unit").GetValue(view)).Id == boss.Id)
                        rendered = (int)view.GetType().GetField("RenderedEnemyFrame").GetValue(view);
                typeof(CatalogCombatLab).GetMethod("FinishDialogue", Hidden).Invoke(lab, null);
                if (basics < 1 || impacts < 1 || results != 1 || completions != 1 || rendered != 21)
                    Debug.LogError("FROST_ART_AUDIT_FAIL " + name + " basic=" + basics + " impact=" + impacts +
                        " result=" + results + " completed=" + completions + " frame=" + rendered);
                else Debug.Log("FROST_ART_AUDIT_PASS " + name + " basic=" + basics + " impact=" + impacts + " frame=21");
                Destroy(go); yield return null;
            }
            GamePreferences.Current.ReduceMotion = originalMotion;
            screen.enabled = true;
            Debug.Log("FROST_ART_AUDIT_COMPLETE"); Destroy(gameObject);
        }
        static void Capture(string directory, string name)
        {
            var shot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); shot.Apply();
            File.WriteAllBytes(Path.Combine(directory, name + ".rgb"), shot.GetRawTextureData());
            File.WriteAllText(Path.Combine(directory, "capture-size.txt"), Screen.width + "," + Screen.height);
            Destroy(shot);
        }
    }
}
#endif
