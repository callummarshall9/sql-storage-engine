namespace sql_storage_engine;

/// <summary>An opaque position owned by one live transaction. Duplicate names identify distinct positions.</summary>
public sealed record StorageSavepoint(StorageTransactionIdentity Transaction, Guid Value, string Name)
{
    public const int MaximumNameLength = 32;
    public const int MaximumCount = 32;
}
