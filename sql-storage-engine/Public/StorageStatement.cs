using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;

namespace sql_storage_engine;

internal sealed class StorageStatement(StorageEngine owner) : IStorageStatement
{
    private volatile bool _active = true;
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
