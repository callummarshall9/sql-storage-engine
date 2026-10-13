// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Processings.CatalogMetadata;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogBTreeIndexOptionsExposure
{
    private static readonly ICatalogBTreeIndexOptionsProcessingService service = new CatalogBTreeIndexOptionsProcessingService();
    public static CatalogBTreeIndexOptions CreateCatalogBTreeIndexOptions(CatalogIndexStorageKind storageKind = CatalogIndexStorageKind.NonClustered, bool isPrimaryKey = false, IEnumerable<ColumnId>? columnIds = null, bool ignoreDuplicateKey = false) =>
        service.PrepareCatalogBTreeIndexOptions(catalogBTreeIndexOptions: new CatalogBTreeIndexOptions { StorageKind = storageKind, IsPrimaryKey = isPrimaryKey, IncludedColumnSource = columnIds, IgnoreDuplicateKey = ignoreDuplicateKey });
}