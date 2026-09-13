using sql_storage_engine.Identifiers;

namespace sql_storage_engine;

/// <summary>Read/write access valid only inside one awaited transaction callback.</summary>
public interface IStorageTransactionContext : IStorageStatement
{
    IStorageCatalog Catalog { get; }
    ValueTask<IStorageIndex> OpenIndexAsync(IndexId indexId, CancellationToken cancellationToken = default);
}
