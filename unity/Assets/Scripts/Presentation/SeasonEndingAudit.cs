#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Expedition.Presentation
{
    // Explicit one-shot QA; captures the same ending view without altering account state.
    public sealed class SeasonEndingAudit : MonoBehaviour
    {
        SeasonOneEndingView view;
        bool manual;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            string flag = Path.Combine(Application.persistentDataPath, "ending-audit.flag");
            if (!File.Exists(flag)) return;
            string mode = File.ReadAllText(flag).Trim(); File.Delete(flag);
            new GameObject("Explicit ending QA").AddComponent<SeasonEndingAudit>().manual = mode == "manual";
        }
        IEnumerator Start()
        {
            var screen = FindFirstObjectByType<ExpeditionScreen>();
            var ready = typeof(ExpeditionScreen).GetField("startupReady", BindingFlags.Instance | BindingFlags.NonPublic);
            float deadline = Time.realtimeSinceStartup + 30;
            while (screen != null && !(bool)ready.GetValue(screen) && Time.realtimeSinceStartup < deadline) yield return null;
            if (screen == null || !(bool)ready.GetValue(screen)) { Debug.LogError("ENDING_AUDIT startup"); Destroy(gameObject); yield break; }
            screen.enabled = false;
            int callbacks = 0;
            view = new SeasonOneEndingView(new[] { "F-KN", "B-AR", "E-PR-H" }, () => callbacks++);
            Debug.Log("ENDING_AUDIT_READY");
            string dir = Path.Combine(Application.persistentDataPath, "EndingAudit"); Directory.CreateDirectory(dir);
            if (manual) while (view.Active) yield return null;
            else
            {
                for (int page = 0; page < 3; page++)
                {
                    yield return new WaitForSecondsRealtime(3.2f);
                    yield return new WaitForEndOfFrame();
                    var shot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                    shot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); shot.Apply();
                    File.WriteAllBytes(Path.Combine(dir, "ending-" + page + ".rgb"), shot.GetRawTextureData()); Destroy(shot);
                    File.WriteAllText(Path.Combine(dir, "capture-size.txt"), Screen.width + "," + Screen.height);
                    view.Next();
                }
            }
            if (callbacks != 1) Debug.LogError("ENDING_AUDIT callback count " + callbacks);
            else Debug.Log("ENDING_AUDIT_PASS");
            view.Dispose(); view = null; screen.enabled = true; Destroy(gameObject);
        }
        void Update() { if (view != null) view.Tick(Time.unscaledDeltaTime); }
        void OnGUI()
        {
            if (view == null || !view.Active) return;
            Rect safe = Screen.safeArea; float scale = Mathf.Min(safe.width / 1280f, safe.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x + (safe.width - 1280 * scale) / 2,
                Screen.height - safe.yMax + (safe.height - 720 * scale) / 2), Quaternion.identity, new Vector3(scale, scale, 1));
            view.Draw(); GUI.matrix = Matrix4x4.identity;
        }
        void OnDestroy() { if (view != null) view.Dispose(); }
    }
}
#endif
