// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogGraphs;
internal interface ICatalogGraphTableProcessingService
{
    CatalogGraphTable PrepareCatalogGraphTable(CatalogGraphTable catalogGraphTable);

    bool ReadCatalogGraphTableSupportsOutgoingTraversal(CatalogGraphTable catalogGraphTable);

    bool ReadCatalogGraphTableSupportsIncomingTraversal(CatalogGraphTable catalogGraphTable);

    GraphTraversalOrder ReadCatalogGraphTableTraversalOrder(CatalogGraphTable catalogGraphTable);

    GraphDuplicateSemantics ReadCatalogGraphTableDuplicateSemantics(CatalogGraphTable catalogGraphTable);

    int ReadCatalogGraphTableMaximumEdgesPerTraversal(CatalogGraphTable catalogGraphTable);
}