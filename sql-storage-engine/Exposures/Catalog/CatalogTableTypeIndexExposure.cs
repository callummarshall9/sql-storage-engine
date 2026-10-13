// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Catalog;
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Processings.CatalogMetadata;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogTableTypeIndexExposure
{
    private static readonly ICatalogTableTypeIndexProcessingService service = new CatalogTableTypeIndexProcessingService();
    public static CatalogTableTypeIndex CreateCatalogTableTypeIndex(string name, bool isPrimaryKey, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CatalogIndexStorageKind storageKind = CatalogIndexStorageKind.NonClustered, IEnumerable<ColumnId>? includedColumns = null, bool ignoreDuplicateKey = false, long? bucketCount = null) =>
        service.PrepareCatalogTableTypeIndex(catalogTableTypeIndex: new CatalogTableTypeIndex { NameSource = name, IsPrimaryKeySource = isPrimaryKey, IsUniqueSource = isUnique, ColumnsSource = columns, StorageKindSource = storageKind, IncludedColumnsSource = includedColumns, IgnoreDuplicateKeySource = ignoreDuplicateKey, BucketCountSource = bucketCount });

    public static CatalogTableTypeIndex PrepareCatalogTableTypeIndex(CatalogTableTypeIndex catalogTableTypeIndex) =>
        service.PrepareCatalogTableTypeIndex(catalogTableTypeIndex: catalogTableTypeIndex);
}