using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.Core;
using Unity.Services.Core.Environments;

namespace Expedition.EditorTools
{
    // Explicit integration check only. Uses synthetic data and a separate auth profile in development.
    [InitializeOnLoad]
    public static class UgsConnectionValidation
    {
        const string ProbePending = "ignis.cloud.probe.pending";
        static UgsConnectionValidation()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ProbePending, false))
                { SessionState.SetBool(ProbePending, false); Probe(); }
            };
        }
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            SessionState.SetBool(ProbePending, true);
            EditorApplication.EnterPlaymode();
        }
        static async void Probe()
        {
            int exit = 0;
            try
            {
                await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName("development").SetProfile("ignis-cloud-probe"));
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                const string key = "ignis_connection_probe_v072";
                string marker = Guid.NewGuid().ToString("N");
                var before = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key });
                string oldLock = before.ContainsKey(key) ? before[key].WriteLock : null;
                var written = await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, SaveItem> { { key, new SaveItem(marker, oldLock) } });
                var read = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key });
                if (read[key].Value.GetAs<string>() != marker) throw new Exception("Readback mismatch.");
                string staleLock = read[key].WriteLock;
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, SaveItem> { { key, new SaveItem(marker + "-next", staleLock) } });
                bool rejected = false;
                try { await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, SaveItem> { { key, new SaveItem("stale", staleLock) } }); }
                catch (CloudSaveConflictException) { rejected = true; }
                if (!rejected) throw new Exception("Stale lock not rejected.");
                File.WriteAllText("Logs/v072-live-cloud.txt", "PASS development: anonymous authentication, Cloud Save write/read, stale-write rejection. Synthetic probe only; Google login not covered.");
                UnityEngine.Debug.Log("UGS_CONNECTION_VALIDATION_PASS");
                AuthenticationService.Instance.SignOut(true);
            }
            catch (Exception ex)
            {
                exit = 1;
                // Record only the error category/code, never tokens or response bodies.
                string category = ex.GetType().Name + (ex is RequestFailedException ? " code=" + ((RequestFailedException)ex).ErrorCode : "");
                File.WriteAllText("Logs/v072-live-cloud.txt", "FAIL " + category);
                UnityEngine.Debug.LogError("UGS_CONNECTION_VALIDATION_FAIL " + category);
            }
            EditorApplication.Exit(exit);
        }
    }
}
