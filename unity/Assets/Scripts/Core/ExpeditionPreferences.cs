using System;

namespace Expedition.Core
{
    [Serializable]
    public sealed class ExpeditionPreferences
    {
        public int Version = 1;
        public float SoundVolume = 1f;
        public bool ReduceMotion, ReduceFlashes;
        // Missing in v1 saves: null/empty means automatic, preserving all older options.
        public string Language = "auto";
        public void Validate()
        {
            if (Version != 1 || float.IsNaN(SoundVolume) || float.IsInfinity(SoundVolume) || SoundVolume < 0 || SoundVolume > 1)
                throw new ArgumentException("Invalid preferences.");
            if (!string.IsNullOrEmpty(Language) && Language != "auto" && Language != "ko" && Language != "en")
                throw new ArgumentException("Invalid language preference.");
        }
    }
}
