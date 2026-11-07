using System;
using System.Security.Cryptography;
using System.Text;

namespace Expedition.Core
{
    [Serializable]
    public sealed class CloudCheckpoint
    {
        public int Version = 1;
        public string PlayerId, Revision, Payload;
        public long SavedUtcTicks;

        public void Validate(string playerId, Action<string> validatePayload)
        {
            if (Version > 1) throw new FutureSaveVersionException();
            Guid revision;
            if (Version != 1 || PlayerId != playerId || !Guid.TryParse(Revision, out revision) ||
                SavedUtcTicks < DateTime.MinValue.Ticks || SavedUtcTicks > DateTime.MaxValue.Ticks ||
                string.IsNullOrEmpty(Payload) || Encoding.UTF8.GetByteCount(Payload) > CloudSavePolicy.MaxPayloadBytes)
                throw new ArgumentException("Invalid cloud checkpoint.");
            validatePayload(Payload);
        }
    }

    [Serializable]
    public sealed class CloudSyncReceipt
    {
        public int Version = 1;
        public string Scope, PlayerId, Revision, PayloadHash;
        public long SyncedUtcTicks;
        public bool Matches(string scope, string playerId)
        { return Version == 1 && Scope == scope && PlayerId == playerId && !string.IsNullOrEmpty(Revision) && !string.IsNullOrEmpty(PayloadHash); }
    }

    public enum CloudSyncDecision { Unchanged, Upload, Restore, Conflict, MissingRemote }

    // Compare common ancestry, never device clocks or the largest score. A later NEW run may be less advanced.
    public static class CloudSavePolicy
    {
        // Leave room for Cloud Save JSON escaping, metadata and the previous checkpoint.
        public const int MaxPayloadBytes = 1024 * 1024;
        public static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""))).Replace("-", "");
        }
        public static CloudSyncDecision Decide(string scope, string playerId, string localPayload,
            bool localHasProgress, CloudSyncReceipt receipt, CloudCheckpoint remote)
        {
            bool known = receipt != null && receipt.Matches(scope, playerId);
            if (remote == null)
            {
                if (known) return CloudSyncDecision.MissingRemote;
                // Never silently copy a different linked account's progress into an empty account.
                if (localHasProgress && receipt != null && !known) return CloudSyncDecision.Conflict;
                return CloudSyncDecision.Upload;
            }
            if (remote.PlayerId != playerId) throw new ArgumentException("Cloud owner mismatch.");
            string localHash = Hash(localPayload), remoteHash = Hash(remote.Payload);
            if (localHash == remoteHash) return CloudSyncDecision.Unchanged;
            if (!known) return localHasProgress ? CloudSyncDecision.Conflict : CloudSyncDecision.Restore;
            bool localChanged = localHash != receipt.PayloadHash;
            bool remoteChanged = remote.Revision != receipt.Revision;
            if (!remoteChanged) return CloudSyncDecision.Upload;
            return localChanged ? CloudSyncDecision.Conflict : CloudSyncDecision.Restore;
        }
        public static CloudSyncReceipt Receipt(string scope, CloudCheckpoint checkpoint)
        {
            return new CloudSyncReceipt { Scope = scope, PlayerId = checkpoint.PlayerId,
                Revision = checkpoint.Revision, PayloadHash = Hash(checkpoint.Payload), SyncedUtcTicks = DateTime.UtcNow.Ticks };
        }
    }
}
