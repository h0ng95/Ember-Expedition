using System;

namespace Expedition.Core
{
    [Serializable]
    public sealed class ExpeditionPreferences
    {
        public int Version = 1;
        public float SoundVolume = 1f;
        public bool ReduceMotion, ReduceFlashes;
        public void Validate()
        {
            if (Version != 1 || float.IsNaN(SoundVolume) || float.IsInfinity(SoundVolume) || SoundVolume < 0 || SoundVolume > 1)
                throw new ArgumentException("Invalid preferences.");
        }
    }
}
