using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;

namespace sql_storage_engine;

/// <summary>A statement-scoped view whose mutations commit or roll back as one durable unit.</summary>
public interface IStorageStatement
{
    /// <summary>Opens a graph node handle valid only during this statement callback.</summary>
    ValueTask<IStorageGraphNodeTable> OpenGraphNodeTableAsync(TableId tableId,
        CancellationToken cancellationToken = default);
    /// <summary>Opens a graph edge handle valid only during this statement callback.</summary>
    ValueTask<IStorageGraphEdgeTable> OpenGraphEdgeTableAsync(TableId tableId,
        CancellationToken cancellationToken = default);
    ValueTask<CatalogTable> CreateTableAsync(CatalogTableName name, IEnumerable<CatalogColumn> columns,
        IEnumerable<CatalogCheckConstraint>? checkConstraints = null,
        CancellationToken cancellationToken = default);
    ValueTask<IStorageTable> OpenTableAsync(TableId tableId, CancellationToken cancellationToken = default);
}
