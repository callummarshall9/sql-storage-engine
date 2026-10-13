// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogCodecs;
internal sealed partial class CatalogIndexShapesOrchestrationService : ICatalogIndexShapesOrchestrationService
{
    private readonly sql_storage_engine.Services.Processings.CatalogIndexShapes.CatalogIndexShapeProcessingService indexes;
    private readonly sql_storage_engine.Services.Processings.CatalogMetadata.CatalogIndexedColumnProcessingService indexedColumns;
    public CatalogIndexShapesOrchestrationService(sql_storage_engine.Services.Processings.CatalogIndexShapes.CatalogIndexShapeProcessingService indexes, sql_storage_engine.Services.Processings.CatalogMetadata.CatalogIndexedColumnProcessingService indexedColumns)
    {
        this.indexes = indexes;
        this.indexedColumns = indexedColumns;
    }

    public CatalogCodecContext ProcessCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogCodecContext(catalogCodecContext: catalogCodecContext);

        switch (catalogCodecContext.Operation)
        {
            case 13:
                catalogCodecContext.PrepareCatalogIndexedColumnResult = indexedColumns.PrepareCatalogIndexedColumn(catalogIndexedColumn: catalogCodecContext.CatalogIndexedColumn!);
                return catalogCodecContext;
            case 24:
                catalogCodecContext.PrepareCatalogIndexResult = indexes.PrepareCatalogIndex(catalogIndex: catalogCodecContext.CatalogIndex!);
                return catalogCodecContext;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(catalogCodecContext.Operation));
        }
    });

    internal static CatalogIndexShapesOrchestrationService CreateCatalogIndexShapesOrchestrationService() =>
        new CatalogIndexShapesOrchestrationService(indexes: new sql_storage_engine.Services.Processings.CatalogIndexShapes.CatalogIndexShapeProcessingService(), indexedColumns: new sql_storage_engine.Services.Processings.CatalogMetadata.CatalogIndexedColumnProcessingService());
}