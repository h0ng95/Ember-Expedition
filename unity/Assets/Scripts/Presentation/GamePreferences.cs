using Expedition.Core;
using UnityEngine;

namespace Expedition.Presentation
{
    public static class GamePreferences
    {
        const string Key = "ignis.preferences.v044", BackupKey = Key + ".backup";
        public static ExpeditionPreferences Current { get; private set; } = new ExpeditionPreferences();
        static bool loaded;
        static ExpeditionPreferences Read(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return null;
            try { var value = JsonUtility.FromJson<ExpeditionPreferences>(PlayerPrefs.GetString(key)); value.Validate(); return value; }
            catch { return null; }
        }
        public static void Load()
        {
            if (loaded) return;
            Current = Read(Key) ?? Read(BackupKey) ?? new ExpeditionPreferences();
            loaded = true; Apply();
        }
        public static void Apply() { AudioListener.volume = Current.SoundVolume; }
        public static void Defaults() { Current = new ExpeditionPreferences(); Apply(); }
        public static bool TrySave()
        {
            if (!loaded) return true;
            try
            {
                Current.Validate();
                var previous = Read(Key);
                if (previous != null) PlayerPrefs.SetString(BackupKey, JsonUtility.ToJson(previous));
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(Current)); PlayerPrefs.Save(); Apply();
                return true;
            }
            catch { return false; }
        }
    }
}
