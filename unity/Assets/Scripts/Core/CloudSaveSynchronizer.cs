using System;
using System.Threading.Tasks;

namespace Expedition.Core
{
    public sealed class CloudRead
    {
        public CloudCheckpoint Checkpoint;
        public string WriteLock;
    }
    public sealed class CloudWriteConflictException : Exception { }
    public interface ICloudSaveBackend
    {
        Task<CloudRead> ReadAsync();
        Task WriteAsync(CloudCheckpoint value, CloudRead previous);
    }
    public interface ICloudLocalSave
    {
        string ReadPayload();
        bool HasProgress { get; }
        CloudSyncReceipt ReadReceipt();
        void WriteReceipt(CloudSyncReceipt receipt);
        void Validate(string payload);
        // Must archive the displaced local payload before committing a restore.
        void Restore(string payload);
    }
    public sealed class CloudSaveSynchronizer
    {
        readonly string scope, playerId;
        readonly ICloudLocalSave local;
        readonly ICloudSaveBackend remote;
        public CloudRead Pending { get; private set; }
        public string PendingLocalHash { get; private set; }
        public bool Busy { get; private set; }

        public CloudSaveSynchronizer(string scope, string playerId, ICloudLocalSave local, ICloudSaveBackend remote)
        { this.scope = scope; this.playerId = playerId; this.local = local; this.remote = remote; }

        public async Task<CloudSyncDecision> SyncAsync()
        {
            if (Busy) throw new InvalidOperationException("Sync already active.");
            Busy = true;
            try
            {
                var read = await remote.ReadAsync();
                if (read == null) throw new InvalidOperationException("No server response.");
                if (read.Checkpoint != null) read.Checkpoint.Validate(playerId, local.Validate);
                string payload = local.ReadPayload(); local.Validate(payload);
                var decision = CloudSavePolicy.Decide(scope, playerId, payload, local.HasProgress, local.ReadReceipt(), read.Checkpoint);
                Pending = null; PendingLocalHash = null;
                if (decision == CloudSyncDecision.Unchanged) local.WriteReceipt(CloudSavePolicy.Receipt(scope, read.Checkpoint));
                else if (decision == CloudSyncDecision.Upload) await Upload(payload, read);
                else
                {
                    // Even an unambiguous newer cloud save is reviewed before replacing the live local run.
                    Pending = read; PendingLocalHash = CloudSavePolicy.Hash(payload);
                }
                return decision;
            }
            finally { Busy = false; }
        }
        async Task Upload(string payload, CloudRead previous)
        {
            var checkpoint = new CloudCheckpoint { PlayerId = playerId, Revision = Guid.NewGuid().ToString("N"),
                Payload = payload, SavedUtcTicks = DateTime.UtcNow.Ticks };
            checkpoint.Validate(playerId, local.Validate);
            await remote.WriteAsync(checkpoint, previous);
            // A failed request never advances ancestry. If the server accepted a lost response,
            // the next read recognizes the identical payload and safely recovers the receipt.
            local.WriteReceipt(CloudSavePolicy.Receipt(scope, checkpoint));
        }
        public async Task ResolveAsync(bool useCloud)
        {
            if (Busy || Pending == null) throw new InvalidOperationException("No pending choice.");
            Busy = true;
            try
            {
                if (CloudSavePolicy.Hash(local.ReadPayload()) != PendingLocalHash) throw new CloudWriteConflictException();
                var latest = await remote.ReadAsync();
                string expected = Pending.Checkpoint == null ? null : Pending.Checkpoint.Revision;
                string actual = latest.Checkpoint == null ? null : latest.Checkpoint.Revision;
                if (expected != actual || latest.WriteLock != Pending.WriteLock) throw new CloudWriteConflictException();
                if (CloudSavePolicy.Hash(local.ReadPayload()) != PendingLocalHash) throw new CloudWriteConflictException();
                if (useCloud)
                {
                    if (latest.Checkpoint == null) throw new InvalidOperationException("No cloud save to restore.");
                    latest.Checkpoint.Validate(playerId, local.Validate);
                    local.Restore(latest.Checkpoint.Payload);
                    local.WriteReceipt(CloudSavePolicy.Receipt(scope, latest.Checkpoint));
                }
                else await Upload(local.ReadPayload(), latest);
                Pending = null; PendingLocalHash = null;
            }
            finally { Busy = false; }
        }
    }
}
