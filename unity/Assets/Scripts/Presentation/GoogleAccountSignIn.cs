using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace Expedition.Presentation
{
    public sealed class GoogleAccountSignIn : MonoBehaviour
    {
        [Serializable] sealed class Response { public string requestId, token, error; }
        TaskCompletionSource<string> pending;
        string requestId;
        float deadline;
        public Task<string> SignInAsync(string clientId)
        {
            if (pending != null) throw new InvalidOperationException("Google sign-in already active.");
#if UNITY_ANDROID && !UNITY_EDITOR
            pending = new TaskCompletionSource<string>();
            var result = pending.Task;
            requestId = Guid.NewGuid().ToString("N"); deadline = Time.realtimeSinceStartup + 120;
            try
            {
                using (var bridge = new AndroidJavaClass("com.ignis.account.GoogleAccountBridge"))
                    bridge.CallStatic("signIn", clientId, gameObject.name, requestId);
            }
            catch (Exception) { Fail(false); }
            return result;
#else
            return Task.FromException<string>(new PlatformNotSupportedException("Google sign-in requires an Android device."));
#endif
        }
        [Preserve]
        public void OnGoogleAccountResult(string json)
        {
            if (pending == null) return;
            Response response;
            try { response = JsonUtility.FromJson<Response>(json); }
            catch (Exception) { Fail(false); return; }
            if (response == null || response.requestId != requestId) return;
            if (!string.IsNullOrEmpty(response.error)) { Fail(response.error == "cancelled"); return; }
            if (string.IsNullOrEmpty(response.token)) { Fail(false); return; }
            var completion = pending; pending = null; requestId = null;
            completion.TrySetResult(response.token); // ID tokens are never logged or written to disk.
        }
        void Update() { if (pending != null && Time.realtimeSinceStartup >= deadline) { CancelNative(); Fail(false); } }
        void OnDestroy() { CancelNative(); Fail(true); }
        void Fail(bool cancelled)
        {
            var completion = pending; pending = null; requestId = null;
            if (completion == null) return;
            if (cancelled) completion.TrySetCanceled(); else completion.TrySetException(new InvalidOperationException("Google sign-in unavailable."));
        }
        void CancelNative()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var bridge = new AndroidJavaClass("com.ignis.account.GoogleAccountBridge")) bridge.CallStatic("cancel"); }
            catch (Exception) { }
#endif
        }
        public static void ClearSession()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var bridge = new AndroidJavaClass("com.ignis.account.GoogleAccountBridge")) bridge.CallStatic("clearSession"); }
            catch (Exception) { }
#endif
        }
    }
}
