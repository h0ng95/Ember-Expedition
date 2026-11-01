using System;
using System.IO;
using Expedition.Core;
using Expedition.Presentation;
using UnityEngine;

namespace Expedition.EditorTools
{
    public static class LocalizationValidation
    {
        static int checks;
        static void Check(bool value, string reason) { checks++; if (!value) throw new Exception("Localization: " + reason); }
        public static void ValidateAndBuild() { Validate(); PrototypeSetup.BuildAndroid(); }
        public static void Validate()
        {
            string beforeLanguage = CatalogText.Language;
            var old = JsonUtility.FromJson<ExpeditionPreferences>("{\"Version\":1,\"SoundVolume\":0.3,\"ReduceMotion\":true,\"ReduceFlashes\":true}");
            old.Validate(); Check(old.SoundVolume == .3f && old.ReduceMotion && old.ReduceFlashes, "Unity legacy preferences preserved");
            old.Language = "en"; var restored = JsonUtility.FromJson<ExpeditionPreferences>(JsonUtility.ToJson(old));
            Check(restored.Language == "en" && restored.SoundVolume == .3f, "Unity preferences round trip");
            var db = CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
            foreach (var form in db.All) LocalizationAudit.Fixture(db, 15, new[] { form.Id });
            var run = LocalizationAudit.Fixture(db, 15); string captured = JsonUtility.ToJson(run.Capture());
            var speechStyle = new GUIStyle { font = Resources.Load<Font>("Fonts/Galmuri11"), fontSize = 20, wordWrap = true };
            foreach (string locale in new[] { "ko", "en", "ko" })
            {
                CatalogText.SetLanguage(locale);
                foreach (int wave in new[] { 5, 10, 15, 20, 25, 30 })
                    foreach (var line in ExpeditionDialogue.ForBoss(wave))
                    {
                        string wrapped = CatalogCombatLab.WrapDialogueText(speechStyle, line.Text, 328);
                        float height = speechStyle.CalcHeight(new GUIContent(wrapped), 328);
                        Check(height + 36 <= 300, "Speech bubble remains compact: " + locale + " " + wave);
                        Check(locale != "en" || !System.Text.RegularExpressions.Regex.IsMatch(line.Text, "[가-힣]"), "English dialogue");
                    }
                Check(captured == JsonUtility.ToJson(run.Capture()), "Locale does not touch Unity run save");
            }
            CatalogText.SetLanguage(beforeLanguage);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/v069-validation.txt", "PASS " + checks + " checks: Unity preferences migration/roundtrip, unchanged run state, all boss dialogue bounds in Korean and English.");
            Debug.Log("LOCALIZATION_VALIDATION_PASS " + checks);
        }
    }
}
