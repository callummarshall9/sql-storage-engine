using sql_storage_engine.Security;
using sql_storage_engine.Catalog;

namespace sql_storage_engine;

public sealed partial class StorageEngine
{
    private readonly SemaphoreSlim _securityEventsGate = new(1, 1);
    private bool _securityEventsUncertain;
    public IStorageSecurity Security => new StorageSecurity(this);
    internal bool HasActiveSecurityTransaction => _activeTransaction is not null;
    internal CatalogService SecurityCatalog => _catalog;
    internal void RequireSecurityTransaction()
    {
        ThrowIfDisposed();
        if (_activeTransaction is null || !IsInStorageScope)
            throw new InvalidOperationException("Security catalog access requires an explicit transaction callback.");
    }
    internal SemaphoreSlim SecurityEventsGate => _securityEventsGate;
    internal bool SecurityEventsUncertain { get => _securityEventsUncertain; set => _securityEventsUncertain = value; }
}
