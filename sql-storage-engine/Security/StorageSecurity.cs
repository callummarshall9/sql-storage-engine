namespace sql_storage_engine.Security;

internal sealed class StorageSecurity(StorageEngine owner) : IStorageSecurity
{
    public async ValueTask<StorageSecuritySnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (owner.IsInStorageScope) throw new InvalidOperationException("Use the callback security snapshot.");
        using var lease = await owner.EnterStatementGateAsync(cancellationToken).ConfigureAwait(false);
        return new StorageSecuritySnapshot(owner.SecurityCatalog.SecurityState);
    }
    public ValueTask AppendEventAsync(StorageAuditRecord record, CancellationToken cancellationToken = default) =>
        StorageSecurityEvents.AppendAsync(owner, record, cancellationToken);
    public ValueTask<IReadOnlyList<StorageAuditRecord>> ReadEventsAsync(CancellationToken cancellationToken = default) =>
        StorageSecurityEvents.ReadAsync(owner, cancellationToken);
}
