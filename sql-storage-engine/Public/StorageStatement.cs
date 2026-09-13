using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;

namespace sql_storage_engine;

internal sealed class StorageStatement(StorageEngine owner) : IStorageTransactionContext
{
    public IStorageCatalog Catalog => new StorageScopedCatalog(owner, () => _active);
    public ValueTask<IStorageIndex> OpenIndexAsync(IndexId indexId, CancellationToken cancellationToken = default)
    {
        if (!_active) throw new InvalidOperationException("The statement scope has completed.");
        return owner.OpenScopedIndexAsync(indexId, () => _active, cancellationToken);
    }
    private volatile bool _active = true;
    private bool _ddlFailed;
    internal void ThrowIfDdlFailed()
    {
        if (_ddlFailed) throw new InvalidOperationException("Graph DDL failed; the statement must roll back.");
    }
    public ValueTask<CatalogTable> CreateGraphNodeTableAsync(CatalogTableName name,
        IEnumerable<CatalogColumn> payloadColumns, CancellationToken cancellationToken = default) =>
        CreateGraphAsync(name, payloadColumns, null, null, cancellationToken);
    public ValueTask<CatalogTable> CreateGraphEdgeTableAsync(CatalogTableName name,
        IEnumerable<CatalogColumn> payloadColumns, TableId fromNodeTableId, TableId toNodeTableId,
        CancellationToken cancellationToken = default) =>
        CreateGraphAsync(name, payloadColumns, fromNodeTableId, toNodeTableId, cancellationToken);
    private async ValueTask<CatalogTable> CreateGraphAsync(CatalogTableName name,
        IEnumerable<CatalogColumn> payloadColumns, TableId? from, TableId? to, CancellationToken token)
    {
        if (!_active) throw new InvalidOperationException("The statement scope has completed.");
        ThrowIfDdlFailed();
        try { return await StorageGraphDdl.CreateAsync(owner, name, payloadColumns, from, to, token).ConfigureAwait(false); }
        catch { _ddlFailed = true; throw; }
    }
    public async ValueTask<CatalogTable> CreateTableAsync(CatalogTableName name,
        IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null,
        CancellationToken cancellationToken = default)
    {
        if (!_active) throw new InvalidOperationException("The statement scope has completed.");
        return await owner.CreateTableAsync(name, columns, checkConstraints, cancellationToken)
            .ConfigureAwait(false);
    }
    public async ValueTask<IStorageTable> OpenTableAsync(TableId tableId,
        CancellationToken cancellationToken = default)
    {
        if (!_active) throw new InvalidOperationException("The statement scope has completed.");
        var storage = await owner.OpenTableStorageAsync(tableId, cancellationToken).ConfigureAwait(false);
        return owner.CreateStatementTable(storage, () => _active);
    }
    public void Complete() => _active = false;
    public async ValueTask<IStorageGraphNodeTable> OpenGraphNodeTableAsync(TableId tableId,
        CancellationToken cancellationToken = default)
    {
        if (!_active) throw new InvalidOperationException("The statement scope has completed.");
        var storage = await owner.OpenTableStorageAsync(tableId, cancellationToken).ConfigureAwait(false);
        if (storage.Definition.Graph?.Kind != GraphTableKind.Node)
            throw new InvalidOperationException("The requested table is not a registered graph node table.");
        return new StorageGraphNodeTable(storage, owner, owner.HandleGeneration, () => _active);
    }

    public async ValueTask<IStorageGraphEdgeTable> OpenGraphEdgeTableAsync(TableId tableId,
        CancellationToken cancellationToken = default)
    {
        if (!_active) throw new InvalidOperationException("The statement scope has completed.");
        var storage = await owner.OpenTableStorageAsync(tableId, cancellationToken).ConfigureAwait(false);
        if (storage.Definition.Graph?.Kind != GraphTableKind.Edge)
            throw new InvalidOperationException("The requested table is not a registered graph edge table.");
        return new StorageGraphEdgeTable(storage, owner, owner.HandleGeneration, () => _active);
    }
}
