// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine;
/// <summary>A statement-scoped view whose mutations commit or roll back as one durable unit.</summary>
public interface IStorageStatement
{
    /// <summary>Creates a new node table, generated identity and unique index within this statement.</summary>
    ValueTask<CatalogTable> CreateCatalogTableGraphNodeAsync(CatalogTableName catalogTableName, IEnumerable<CatalogColumn> payloadColumns, CancellationToken cancellationToken = default);
    /// <summary>Creates a new edge table and all identity/adjacency indexes. One ordered endpoint pair is enforced;
    /// node deletion uses NO ACTION. Endpoints must be registered node tables in this database.</summary>
    ValueTask<CatalogTable> CreateCatalogTableGraphEdgeAsync(CatalogTableName catalogTableName, IEnumerable<CatalogColumn> payloadColumns, GraphTableEndpoints graphTableEndpoints, CancellationToken cancellationToken = default);
    /// <summary>Opens a graph node handle valid only during this statement callback.</summary>
    ValueTask<IStorageGraphNodeTable> OpenGraphNodeTableAsync(TableId tableId, CancellationToken cancellationToken = default);
    /// <summary>Opens a graph edge handle valid only during this statement callback.</summary>
    ValueTask<IStorageGraphEdgeTable> OpenGraphEdgeTableAsync(TableId tableId, CancellationToken cancellationToken = default);

    ValueTask<CatalogTable> CreateCatalogTableAsync(CatalogTableName catalogTableName, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, CancellationToken cancellationToken = default);

    ValueTask<IStorageTable> OpenTableAsync(TableId tableId, CancellationToken cancellationToken = default);
}