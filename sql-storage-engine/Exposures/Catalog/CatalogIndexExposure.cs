// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Services.Coordinations.CatalogIndexes;
using sql_storage_engine.Services.Orchestrations.CatalogIndexShapes;
using sql_storage_engine.Services.Orchestrations.SpecializedIndexOptions;
using sql_storage_engine.Services.Processings.CatalogIndexShapes;
using sql_storage_engine.Services.Processings.CatalogMetadata;
using sql_storage_engine.Services.Processings.SpecializedIndexOptions;
using sql_storage_engine.Services.Processings.CatalogFullTexts;
using sql_storage_engine.Services.Foundations.SpecializedIndexPaths;
using sql_storage_engine.Services.Foundations.SqlCollations;
using sql_storage_engine.Brokers.SpecializedIndexPaths;

namespace sql_storage_engine.Exposures.Catalog;
public sealed class CatalogIndexExposure : ICompositionExposure
{
    private CatalogIndexExposure()
    {
    }

    private static readonly ICatalogIndexCoordinationService service = new CatalogIndexCoordinationService(shapes: new CatalogIndexShapeOrchestrationService(columns: new CatalogIndexedColumnProcessingService(), shapes: new CatalogIndexShapeProcessingService()), specialized: new CatalogSpecializedIndexOptionsOrchestrationService(specialized: new SpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService(paths: new SpecializedIndexPathService(broker: new SpecializedIndexPathBroker())), fullTexts: new SqlCollationCatalogFullTextIndexOptionsProcessingService(collations: new SqlCollationService())));
    public static CatalogIndex CreateCatalogIndex(IndexId indexId, string name, TableId tableId, PageId pageId, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CatalogSpecializedIndexOptions? catalogSpecializedIndexOptions = null, CatalogIndexStorageKind storageKind = CatalogIndexStorageKind.NonClustered, bool isPrimaryKey = false, IEnumerable<ColumnId>? includedColumns = null, bool ignoreDuplicateKey = false) =>
        service.PrepareCatalogIndex(catalogIndex: new CatalogIndex { Id = indexId, Name = name, TableId = tableId, RootPageId = pageId, IsUnique = isUnique, ColumnSource = columns, SpecializedOptions = catalogSpecializedIndexOptions, StorageKind = storageKind, IsPrimaryKey = isPrimaryKey, IncludedColumnSource = includedColumns, IgnoreDuplicateKey = ignoreDuplicateKey });
}