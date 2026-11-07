using System;
using System.IO;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class CloudAccountValidation
    {
        static int checks;
        static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception("Cloud account: " + message); }
        public static void ValidateAndBuild() { Validate(); PrototypeSetup.BuildAndroid(); }
        public static void Validate()
        {
            string language = CatalogText.Language;
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            var run = ExpeditionRun.New(db, 72001);
            var progress = ExpeditionProgress.New(); progress.Observe(run);
            var journal = ExpeditionJournal.New(); journal.Observe(run);
            var account = new ExpeditionAccountSave { Version = 2, HasRun = true, Run = run.Capture(),
                Progress = progress.Capture(), Journal = journal.Capture(), History = new ExpeditionHistory().Capture() };
            account.Validate(db);
            string payload = JsonUtility.ToJson(account);
            var point = new CloudCheckpoint { PlayerId = "test", Revision = Guid.NewGuid().ToString("N"), Payload = payload, SavedUtcTicks = DateTime.UtcNow.Ticks };
            var document = new CloudAccountDocument { Current = point, Previous = point };
            var roundtrip = JsonUtility.FromJson<CloudAccountDocument>(JsonUtility.ToJson(document));
            roundtrip.Current.Validate("test", json => JsonUtility.FromJson<ExpeditionAccountSave>(json).Validate(db));
            Check(roundtrip.Current.Payload == payload && roundtrip.Previous.Payload == payload, "cloud envelope roundtrip");
            Check(!UgsAccountBackend.Linked, "local tests must not initialize real authentication");
            Check(Mathf.Abs(ExpeditionScreen.AccountWindow.center.x - 640) < .1f &&
                Mathf.Abs(ExpeditionScreen.AccountWindow.center.y - 360) < .1f, "account centered");
            var font = Resources.Load<Font>("Fonts/Galmuri11");
            var body = new GUIStyle { font = font, fontSize = 19, wordWrap = true };
            var small = new GUIStyle { font = font, fontSize = 15, wordWrap = true };
            var hint = new GUIStyle { font = font, fontSize = 17, wordWrap = true };
            try
            {
                foreach (string locale in new[] { "ko", "en" })
                {
                    CatalogText.SetLanguage(locale);
                    foreach (string key in new[] { "guest_help", "linked_help" })
                        Check(body.CalcHeight(new GUIContent(CatalogText.Get("cloud." + key)), 846) <= 94, locale + key + " clipped");
                    foreach (string key in new[] { "choose_help", "replace_confirm" })
                        Check(body.CalcHeight(new GUIContent(CatalogText.Get("cloud." + key)), 846) <= 56, locale + key + " clipped");
                    Check(small.CalcHeight(new GUIContent(CatalogText.Get("cloud.signout_help")), 846) <= 38, locale + " signout clipped");
                    Check(small.CalcHeight(new GUIContent(CatalogText.Get("cloud.ranking_later")), 846) <= 52, locale + " ranking clipped");
                    foreach (string key in new[] { "failed", "changed", "invalid", "future", "remote_missing", "restore_available", "conflict", "cancelled" })
                        Check(hint.CalcHeight(new GUIContent(CatalogText.Get("cloud." + key)), 846) <= 44, locale + key + " status clipped");
                }
                string folder = "Logs/cloud-account-validation-" + Guid.NewGuid().ToString("N");
                var store = new ExpeditionSaveStore(folder, json => JsonUtility.FromJson<ExpeditionAccountSave>(json).Validate(db));
                store.Commit(payload); Check(store.Load().Payload == payload, "existing local save format preserved");
                Debug.Log("CLOUD_ACCOUNT_VALIDATION_PASS " + checks + " checks");
                File.WriteAllText("Logs/v072-validation.txt", "PASS " + checks + " checks; Unity JSON, unchanged local format, Korean/English panel layout.");
            }
            finally { CatalogText.SetLanguage(language); }
        }
    }
}
