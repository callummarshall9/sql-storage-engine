using System.Security.Cryptography;
using System.Text.Json;
using sql_storage_engine.Transactions;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Security;

internal static class StorageSecurityEvents
{
    private const int MaximumEvents = 4096;
    private const int MaximumBytes = 4 * 1024 * 1024;
    internal static async ValueTask AppendAsync(StorageEngine owner, StorageAuditRecord record, CancellationToken token)
    {
        owner.ThrowIfDisposed(); StorageSecurityState.ValidateRecord(record);
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Durable security events require Linux.");
        if (record.Kind is not (StorageAuditKind.Denied or StorageAuditKind.ReadAdmission)) throw new ArgumentException("Event requires transaction-coupled audit.");
        if (record.Kind == StorageAuditKind.Denied && owner.HasActiveSecurityTransaction)
            throw new InvalidOperationException("Abort the user transaction before recording denial.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        await owner.SecurityEventsGate.WaitAsync(deadline.Token).ConfigureAwait(false);
        try
        {
            owner.ThrowIfDisposed();
            if (owner.SecurityEventsUncertain) throw new StorageAuditException(record.OperationId);
            var records = Read(owner);
            var existing = records.SingleOrDefault(a => a.OperationId == record.OperationId);
            if (existing is not null)
            {
                if (existing != record) throw new ArgumentException("Operation identity has conflicting audit content.");
                try
                {
                    // A prior caller may have lost its acknowledgement after rename. Re-establish durability.
                    using var stream = new FileStream(owner.TransactionPath + ".security-events", FileMode.Open, FileAccess.Write, FileShare.Read);
                    stream.Flush(true);
                    TransactionDirectory.Flush(owner.TransactionPath + ".security-events");
                }
                catch { owner.SecurityEventsUncertain = true; throw new StorageAuditException(record.OperationId); }
                return;
            }
            if (records.Length >= MaximumEvents) throw new InvalidOperationException("Security event retention quota exceeded.");
            var path = owner.TransactionPath + ".security-events";
            var temporary = path + ".pending";
            var payload = JsonSerializer.SerializeToUtf8Bytes(records.Append(record).ToArray());
            if (payload.Length > MaximumBytes - 48) throw new InvalidOperationException("Security event byte quota exceeded.");
            deadline.Token.ThrowIfCancellationRequested();
            try
            {
                owner.TransactionObserver?.Invoke("security-event-before-write");
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(owner.DatabaseId.Value.ToByteArray());
                    stream.Write(SHA256.HashData(payload)); stream.Write(payload); stream.Flush(true);
                }
                deadline.Token.ThrowIfCancellationRequested();
                owner.TransactionObserver?.Invoke("security-event-before-publish");
                // After publication starts, finish durability without caller cancellation.
                File.Move(temporary, path, true);
                owner.TransactionObserver?.Invoke("security-event-after-publish");
                TransactionDirectory.Flush(path);
                owner.TransactionObserver?.Invoke("security-event-durable");
            }
            catch (OperationCanceledException) { File.Delete(temporary); throw; }
            catch
            {
                owner.SecurityEventsUncertain = true;
                throw new StorageAuditException(record.OperationId);
            }
        }
        finally { owner.SecurityEventsGate.Release(); }
    }
    internal static async ValueTask<IReadOnlyList<StorageAuditRecord>> ReadAsync(StorageEngine owner, CancellationToken token)
    {
        owner.ThrowIfDisposed();
        await owner.SecurityEventsGate.WaitAsync(token).ConfigureAwait(false);
        try { owner.ThrowIfDisposed(); return Array.AsReadOnly(Read(owner)); }
        finally { owner.SecurityEventsGate.Release(); }
    }
    private static StorageAuditRecord[] Read(StorageEngine owner)
    {
        var path = owner.TransactionPath + ".security-events";
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length is < 48 or > MaximumBytes) throw new StorageFormatException("Invalid security event file length.");
        var bytes = File.ReadAllBytes(path);
        if (new Guid(bytes.AsSpan(0, 16)) != owner.DatabaseId.Value ||
            !CryptographicOperations.FixedTimeEquals(bytes.AsSpan(16, 32), SHA256.HashData(bytes.AsSpan(48))))
            throw new StorageFormatException("Invalid security event identity or checksum.");
        try
        {
            var records = JsonSerializer.Deserialize<StorageAuditRecord[]>(bytes.AsSpan(48)) ?? throw new ArgumentException();
            if (records.Length > MaximumEvents || records.Select(a => a.OperationId).Distinct().Count() != records.Length) throw new ArgumentException();
            foreach (var record in records)
            {
                StorageSecurityState.ValidateRecord(record);
                if (record.Kind is not (StorageAuditKind.Denied or StorageAuditKind.ReadAdmission)) throw new ArgumentException();
            }
            return records;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or NullReferenceException)
        { throw new StorageFormatException("Invalid security event records."); }
    }
}
