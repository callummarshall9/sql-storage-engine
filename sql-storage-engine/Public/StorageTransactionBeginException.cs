namespace sql_storage_engine;

/// <summary>Begin failed and cleanup could not confirm an outcome. Reopen and resolve Identity.</summary>
public sealed class StorageTransactionBeginException(StorageTransactionIdentity identity)
    : IOException("Transaction acquisition cleanup is indeterminate; reopen storage and resolve the transaction identity.")
{
    public StorageTransactionIdentity Identity { get; } = identity;
}
