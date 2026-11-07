using System;
using System.Threading.Tasks;
using Expedition.Core;

static class Program
{
    const string Scope = "test-project/development", Owner = "player-a";
    static int checks;
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static CloudCheckpoint Point(string payload, string owner = Owner)
    { return new CloudCheckpoint { PlayerId = owner, Payload = payload, Revision = Guid.NewGuid().ToString("N"), SavedUtcTicks = DateTime.UtcNow.Ticks }; }
    sealed class Local : ICloudLocalSave
    {
        public string Payload = "fresh", Backup;
        public bool Progress, FailRestore, FailReceipt;
        public CloudSyncReceipt Receipt;
        public bool HasProgress { get { return Progress; } }
        public string ReadPayload() { return Payload; }
        public CloudSyncReceipt ReadReceipt() { return Receipt; }
        public void WriteReceipt(CloudSyncReceipt value) { if (FailReceipt) throw new Exception("disk full"); Receipt = value; }
        public void Validate(string value) { if (value == "corrupt" || string.IsNullOrEmpty(value)) throw new ArgumentException("corrupt"); }
        public void Restore(string value) { Validate(value); if (FailRestore) throw new Exception("disk full"); Backup = Payload; Payload = value; }
    }
    sealed class Server : ICloudSaveBackend
    {
        public CloudCheckpoint Value;
        public int Writes, Reads, Lock;
        public bool FailRead, FailWrite, LoseAck;
        public Action DuringRead, DuringWrite;
        public Task<CloudRead> ReadAsync()
        {
            Reads++; if (FailRead) throw new Exception("offline");
            if (DuringRead != null) { var action = DuringRead; DuringRead = null; action(); }
            return Task.FromResult(new CloudRead { Checkpoint = Value, WriteLock = Value == null ? null : Lock.ToString() });
        }
        public Task WriteAsync(CloudCheckpoint value, CloudRead previous)
        {
            if (DuringWrite != null) DuringWrite();
            if (FailWrite) throw new Exception("offline");
            if (previous.WriteLock != (Value == null ? null : Lock.ToString())) throw new CloudWriteConflictException();
            Value = value; Lock++; Writes++;
            if (LoseAck) throw new Exception("response lost");
            return Task.FromResult(0);
        }
    }
    static async Task Throws(Func<Task> action, string label)
    { bool failed = false; try { await action(); } catch { failed = true; } Check(failed, label); }
    static async Task Run()
    {
        var a = Point("base"); var receipt = CloudSavePolicy.Receipt(Scope, a);
        Check(CloudSavePolicy.Decide(Scope, Owner, "fresh", false, null, null) == CloudSyncDecision.Upload, "guest first backup");
        Check(CloudSavePolicy.Decide(Scope, Owner, "run", true, null, null) == CloudSyncDecision.Upload, "guest progress adoption");
        Check(CloudSavePolicy.Decide(Scope, Owner, "fresh", false, null, a) == CloudSyncDecision.Restore, "reinstall restore");
        Check(CloudSavePolicy.Decide(Scope, Owner, "run", true, null, a) == CloudSyncDecision.Conflict, "existing account vs guest");
        Check(CloudSavePolicy.Decide(Scope, Owner, "base", true, receipt, a) == CloudSyncDecision.Unchanged, "no writes for identical save");
        Check(CloudSavePolicy.Decide(Scope, Owner, "run", true, receipt, a) == CloudSyncDecision.Upload, "local advance");
        var b = Point("remote-new"); b.SavedUtcTicks = 0;
        Check(CloudSavePolicy.Decide(Scope, Owner, "base", true, receipt, b) == CloudSyncDecision.Restore, "clock skew ignored");
        Check(CloudSavePolicy.Decide(Scope, Owner, "run", true, receipt, b) == CloudSyncDecision.Conflict, "two-device divergence");
        Check(CloudSavePolicy.Decide(Scope, Owner, "run", true, receipt, null) == CloudSyncDecision.MissingRemote, "deleted cloud is not uploaded silently");
        Check(CloudSavePolicy.Decide("production", Owner, "run", true, receipt, null) == CloudSyncDecision.Conflict, "environment isolation");
        Check(CloudSavePolicy.Decide(Scope, "other", "run", true, receipt, null) == CloudSyncDecision.Conflict, "account switch into empty cloud");
        Check(CloudSavePolicy.Decide(Scope, Owner, "run", true, new CloudSyncReceipt { Version = 0 }, null) == CloudSyncDecision.Conflict, "damaged ancestry");
        var local = new Local { Progress = true }; var server = new Server(); var sync = new CloudSaveSynchronizer(Scope, Owner, local, server);
        await sync.SyncAsync(); Check(server.Writes == 1 && local.Receipt != null, "upload then receipt");
        await sync.SyncAsync(); Check(server.Writes == 1, "identical snapshot deduplicated");
        local.Payload = "new-run"; server.FailWrite = true; var oldReceipt = local.Receipt;
        await Throws(async () => { await sync.SyncAsync(); }, "offline upload");
        Check(local.Receipt == oldReceipt && local.Payload == "new-run" && !sync.Busy, "failed upload preserves local and ancestry");
        server.FailWrite = false; server.LoseAck = true;
        await Throws(async () => { await sync.SyncAsync(); }, "lost upload acknowledgement");
        server.LoseAck = false; int writes = server.Writes;
        await sync.SyncAsync(); Check(server.Writes == writes && local.Receipt.PayloadHash == CloudSavePolicy.Hash(local.Payload), "lost ack recovered without duplicate write");
        server.Value = Point("another-device"); server.Lock++;
        await sync.SyncAsync(); Check(sync.Pending != null && local.Payload == "new-run", "remote advance awaits player review");
        await sync.ResolveAsync(true); Check(local.Payload == "another-device" && local.Backup == "new-run", "restore archives displaced local");
        local.Payload = "ours"; server.Value = Point("theirs"); server.Lock++;
        Check(await sync.SyncAsync() == CloudSyncDecision.Conflict, "conflict flow");
        server.Value = Point("theirs2"); server.Lock++;
        await Throws(() => sync.ResolveAsync(false), "cloud changed during confirmation");
        Check(server.Value.Payload == "theirs2" && local.Payload == "ours", "stale choice preserves both");
        await sync.SyncAsync(); local.Payload = "ours2";
        await Throws(() => sync.ResolveAsync(true), "local changed during confirmation");
        Check(local.Payload == "ours2", "changed local not overwritten");
        await sync.SyncAsync(); await sync.ResolveAsync(false);
        Check(server.Value.Payload == "ours2" && sync.Pending == null, "explicit device choice");
        server.Value = Point("corrupt"); server.Lock++; writes = server.Writes;
        await Throws(async () => { await sync.SyncAsync(); }, "corrupt cloud");
        Check(local.Payload == "ours2" && server.Writes == writes, "corrupt cloud never erased");
        server.Value = Point("foreign", "other");
        await Throws(async () => { await sync.SyncAsync(); }, "foreign cloud owner rejected");
        server.Value = Point("future"); server.Value.Version = 2;
        await Throws(async () => { await sync.SyncAsync(); }, "future schema protected");
        server.Value = Point("valid"); server.FailRead = true;
        await Throws(async () => { await sync.SyncAsync(); }, "read failure");
        Check(server.Writes == writes && !sync.Busy, "read error cannot become missing save");
        server.FailRead = false; await sync.SyncAsync(); local.FailRestore = true; oldReceipt = local.Receipt;
        await Throws(() => sync.ResolveAsync(true), "restore disk failure");
        Check(local.Payload == "ours2" && local.Receipt == oldReceipt, "restore failure keeps ancestry");
        local.FailRestore = false; await sync.ResolveAsync(true);
        local.Payload = "edit"; server.DuringWrite = () => { server.Value = Point("raced"); server.Lock++; };
        await Throws(async () => { await sync.SyncAsync(); }, "CAS race");
        Check(server.Value.Payload == "raced" && local.Payload == "edit", "no forced overwrite after CAS failure");
        server.DuringWrite = null; await sync.SyncAsync();
        server.DuringRead = () => local.Payload = "edit-during-fetch";
        await Throws(() => sync.ResolveAsync(true), "local edit while refetching");
        Check(local.Payload == "edit-during-fetch", "async local stale guard");
        server.Value = Point(local.Payload); server.Lock++; local.FailReceipt = true;
        await Throws(async () => { await sync.SyncAsync(); }, "receipt disk failure");
        Check(local.Payload == server.Value.Payload && !sync.Busy, "receipt error preserves both copies");
        local.FailReceipt = false; await sync.SyncAsync();
        Check(local.Receipt.Revision == server.Value.Revision, "receipt repaired by equal payload");
        var huge = Point(new string('x', CloudSavePolicy.MaxPayloadBytes + 1));
        await Throws(() => { huge.Validate(Owner, local.Validate); return Task.FromResult(0); }, "size cap");
        // Every combination of ancestry, local edits and remote revisions must keep divergent copies for review.
        for (int i = 0; i < 100; i++)
        {
            var start = Point("start-" + i); var changed = Point("cloud-" + i);
            var result = CloudSavePolicy.Decide(Scope, Owner, "local-" + i, true, CloudSavePolicy.Receipt(Scope, start), changed);
            Check(result == CloudSyncDecision.Conflict, "divergence matrix " + i);
        }
        Console.WriteLine("PASS cloud account: " + checks + " checks; guest adoption, reinstall, conflicts, identity isolation, offline, lost ACK, stale writes, corruption and restore failures.");
    }
    static int Main() { try { Run().GetAwaiter().GetResult(); return 0; } catch (Exception e) { Console.Error.WriteLine(e); return 1; } }
}
