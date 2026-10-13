// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Processings.CatalogGraphs;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogGraphTableExposure
{
    private static readonly ICatalogGraphTableProcessingService service = new CatalogGraphTableProcessingService();
    public static CatalogGraphTable CreateCatalogGraphTableNode(ulong identityColumnId, ulong identityIndexId) =>
        service.PrepareCatalogGraphTable(catalogGraphTable: new CatalogGraphTable { Kind = GraphTableKind.Node, IdentityColumnId = new sql_storage_engine.Models.Identifiers.ColumnId { Value = identityColumnId }, IdentityIndexId = new IndexId { Value = identityIndexId } });

    public static CatalogGraphTable CreateCatalogGraphTableEdge(ulong identityColumnId, ulong identityIndexId, ulong fromNodeTableId, ulong fromNodeColumnId, ulong outgoingIndexId, ulong toNodeTableId, ulong toNodeColumnId, ulong incomingIndexId) =>
        service.PrepareCatalogGraphTable(catalogGraphTable: new CatalogGraphTable { Kind = GraphTableKind.Edge, IdentityColumnId = new sql_storage_engine.Models.Identifiers.ColumnId { Value = identityColumnId }, IdentityIndexId = new IndexId { Value = identityIndexId }, FromNodeTableId = new TableId { Value = fromNodeTableId }, FromNodeColumnId = new sql_storage_engine.Models.Identifiers.ColumnId { Value = fromNodeColumnId }, OutgoingIndexId = new IndexId { Value = outgoingIndexId }, ToNodeTableId = new TableId { Value = toNodeTableId }, ToNodeColumnId = new sql_storage_engine.Models.Identifiers.ColumnId { Value = toNodeColumnId }, IncomingIndexId = new IndexId { Value = incomingIndexId } });

    public static bool ReadSupportsOutgoingTraversal(CatalogGraphTable catalogGraphTable) =>
        service.ReadCatalogGraphTableSupportsOutgoingTraversal(catalogGraphTable: catalogGraphTable);

    public static bool ReadSupportsIncomingTraversal(CatalogGraphTable catalogGraphTable) =>
        service.ReadCatalogGraphTableSupportsIncomingTraversal(catalogGraphTable: catalogGraphTable);

    public static GraphTraversalOrder ReadTraversalOrder(CatalogGraphTable catalogGraphTable) =>
        service.ReadCatalogGraphTableTraversalOrder(catalogGraphTable: catalogGraphTable);

    public static GraphDuplicateSemantics ReadDuplicateSemantics(CatalogGraphTable catalogGraphTable) =>
        service.ReadCatalogGraphTableDuplicateSemantics(catalogGraphTable: catalogGraphTable);

    public static int ReadMaximumEdgesPerTraversal(CatalogGraphTable catalogGraphTable) =>
        service.ReadCatalogGraphTableMaximumEdgesPerTraversal(catalogGraphTable: catalogGraphTable);

    public static CatalogGraphTable PrepareCatalogGraphTable(CatalogGraphTable catalogGraphTable) =>
        service.PrepareCatalogGraphTable(catalogGraphTable: catalogGraphTable);
}