using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;

namespace sql_storage_engine;

/// <summary>A statement-scoped view whose mutations commit or roll back as one durable unit.</summary>
public interface IStorageStatement
{
    /// <summary>Creates a new node table, generated identity and unique index within this statement.</summary>
    ValueTask<CatalogTable> CreateGraphNodeTableAsync(CatalogTableName name, IEnumerable<CatalogColumn> payloadColumns,
        CancellationToken cancellationToken = default);
    /// <summary>Creates a new edge table and all identity/adjacency indexes. One ordered endpoint pair is enforced;
    /// node deletion uses NO ACTION. Endpoints must be registered node tables in this database.</summary>
    ValueTask<CatalogTable> CreateGraphEdgeTableAsync(CatalogTableName name, IEnumerable<CatalogColumn> payloadColumns,
        TableId fromNodeTableId, TableId toNodeTableId, CancellationToken cancellationToken = default);
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
