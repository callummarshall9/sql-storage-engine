using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;

namespace sql_storage_engine.Transactions;

/// <summary>Identifies one encoded key in an index.</summary>
public sealed record IndexKeyLockResource : LockResource
{
    public IndexKeyLockResource(IndexId indexId, IndexKey key)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        IndexId = indexId;
    }

    public IndexId IndexId { get; }
    public IndexKey Key { get; }
}
