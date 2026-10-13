// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Graph;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogGraphs;
internal sealed partial class CatalogGraphTableProcessingService : ICatalogGraphTableProcessingService
{
    public CatalogGraphTable PrepareCatalogGraphTable(CatalogGraphTable catalogGraphTable) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogGraphTable(catalogGraphTable: catalogGraphTable);
        return catalogGraphTable;
    });

    public bool ReadCatalogGraphTableSupportsOutgoingTraversal(CatalogGraphTable catalogGraphTable) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogGraphTable(catalogGraphTable: catalogGraphTable);
        return catalogGraphTable.Kind == GraphTableKind.Edge;
    });

    public bool ReadCatalogGraphTableSupportsIncomingTraversal(CatalogGraphTable catalogGraphTable) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogGraphTable(catalogGraphTable: catalogGraphTable);
        return catalogGraphTable.Kind == GraphTableKind.Edge;
    });

    public GraphTraversalOrder ReadCatalogGraphTableTraversalOrder(CatalogGraphTable catalogGraphTable) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogGraphTable(catalogGraphTable: catalogGraphTable);
        return GraphTraversalOrder.EdgePhysicalIdentity;
    });

    public GraphDuplicateSemantics ReadCatalogGraphTableDuplicateSemantics(CatalogGraphTable catalogGraphTable) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogGraphTable(catalogGraphTable: catalogGraphTable);
        return GraphDuplicateSemantics.PreserveParallelEdgesAndDeduplicateSelfLoopForBoth;
    });

    public int ReadCatalogGraphTableMaximumEdgesPerTraversal(CatalogGraphTable catalogGraphTable) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogGraphTable(catalogGraphTable: catalogGraphTable);
        return GraphTraversalOptions.MaximumSupportedEdges;
    });
}