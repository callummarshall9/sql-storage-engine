namespace sql_storage_engine.Security;

public interface IStorageSecurity
{
    ValueTask<StorageSecuritySnapshot> ReadAsync(CancellationToken cancellationToken = default);
    /// <summary>Append only denied or read-admission events independently of the user transaction.</summary>
    ValueTask AppendEventAsync(StorageAuditRecord record, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<StorageAuditRecord>> ReadEventsAsync(CancellationToken cancellationToken = default);
}
