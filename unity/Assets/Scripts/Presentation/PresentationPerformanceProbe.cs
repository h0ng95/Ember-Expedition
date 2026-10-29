using UnityEngine;

namespace Expedition.Presentation
{
    // Development APK diagnostics only; never stores player data or sends telemetry.
    public sealed class PresentationPerformanceProbe : MonoBehaviour
    {
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Mark(string context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            current = context;
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static string current = "startup";
        string measured;
        readonly float[] frames = new float[8192];
        int count;
        float duration, warmup;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("Development presentation metrics");
            DontDestroyOnLoad(go); go.AddComponent<PresentationPerformanceProbe>();
        }
        void ResetSample() { count = 0; duration = 0; warmup = 3; }
        void OnApplicationPause(bool paused) { ResetSample(); }
        void LateUpdate()
        {
            if (measured != current) { Flush(); measured = current; ResetSample(); }
            float dt = Time.unscaledDeltaTime;
            if (warmup > 0) { warmup -= dt; return; }
            if (dt <= 0) return;
            frames[count++] = dt * 1000; duration += dt;
            if (duration >= 15 || count == frames.Length) { Flush(); count = 0; duration = 0; }
        }
        void Flush()
        {
            if (duration < 3 || count == 0) return;
            System.Array.Sort(frames, 0, count);
            int slow = 0; for (int i = 0; i < count; i++) if (frames[i] > 50) slow++;
            Debug.Log("PRESENTATION_PERF context=" + measured + " seconds=" + duration.ToString("0.0") +
                " frames=" + count + " fps=" + (count / duration).ToString("0.0") +
                " p95_ms=" + frames[Mathf.Min(count - 1, Mathf.CeilToInt(count * .95f) - 1)].ToString("0.0") +
                " over50ms=" + slow + " allocated_mb=" +
                (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f).ToString("0.0"));
        }
#endif
    }
}
