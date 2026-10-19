using System;
using System.Collections.Generic;

namespace Expedition.Core
{
    // The first recruit fills the fourth slot; a fifth owned ally starts on the bench.
    public sealed class StageDeployment
    {
        private readonly List<string> owned = new List<string>(5);
        public string BenchedId { get; private set; }
        public int OwnedCount { get { return owned.Count; } }
        public int DeployedCount { get { return owned.Count - (BenchedId == null ? 0 : 1); } }

        public StageDeployment() { Reset(); }

        public void Reset()
        {
            owned.Clear();
            owned.Add("knight");
            owned.Add("priest");
            owned.Add("mage");
            BenchedId = null;
        }

        public void AddRecruit(string id)
        {
            if (string.IsNullOrEmpty(id) || owned.Contains(id) || owned.Count >= 5)
                throw new ArgumentException("Recruit must be a new fourth or fifth ally.");
            owned.Add(id);
            if (owned.Count == 5) BenchedId = id;
        }

        public bool IsDeployed(string id) { return owned.Contains(id) && id != BenchedId; }

        public bool CycleBench()
        {
            if (owned.Count <= 4) return false;
            int index = owned.IndexOf(BenchedId);
            BenchedId = owned[(index + 1) % owned.Count];
            return true;
        }
    }
}
