// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogCodecs;
internal sealed partial class CatalogTableTypeShapesOrchestrationService : ICatalogTableTypeShapesOrchestrationService
{
    private readonly sql_storage_engine.Services.Processings.CatalogMetadata.CatalogTableTypeProcessingService tableTypes;
    private readonly sql_storage_engine.Services.Processings.CatalogMetadata.CatalogTableTypeIndexProcessingService tableTypeIndexes;
    private readonly sql_storage_engine.Services.Processings.CatalogMetadata.CatalogSystemVersioningProcessingService temporal;
    public CatalogTableTypeShapesOrchestrationService(sql_storage_engine.Services.Processings.CatalogMetadata.CatalogTableTypeProcessingService tableTypes, sql_storage_engine.Services.Processings.CatalogMetadata.CatalogTableTypeIndexProcessingService tableTypeIndexes, sql_storage_engine.Services.Processings.CatalogMetadata.CatalogSystemVersioningProcessingService temporal)
    {
        this.tableTypes = tableTypes;
        this.tableTypeIndexes = tableTypeIndexes;
        this.temporal = temporal;
    }

    public CatalogCodecContext ProcessCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogCodecContext(catalogCodecContext: catalogCodecContext);

        switch (catalogCodecContext.Operation)
        {
            case 10:
                catalogCodecContext.PrepareCatalogSystemVersioningResult = temporal.PrepareCatalogSystemVersioning(catalogSystemVersioning: catalogCodecContext.CatalogSystemVersioning!);
                return catalogCodecContext;
            case 16:
                catalogCodecContext.PrepareCatalogTableTypeIndexResult = tableTypeIndexes.PrepareCatalogTableTypeIndex(catalogTableTypeIndex: catalogCodecContext.CatalogTableTypeIndex!);
                return catalogCodecContext;
            case 17:
                catalogCodecContext.PrepareCatalogTableTypeResult = tableTypes.PrepareCatalogTableType(catalogTableType: catalogCodecContext.CatalogTableType!);
                return catalogCodecContext;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(catalogCodecContext.Operation));
        }
    });

    internal static CatalogTableTypeShapesOrchestrationService CreateCatalogTableTypeShapesOrchestrationService() =>
        new CatalogTableTypeShapesOrchestrationService(tableTypes: new sql_storage_engine.Services.Processings.CatalogMetadata.CatalogTableTypeProcessingService(), tableTypeIndexes: new sql_storage_engine.Services.Processings.CatalogMetadata.CatalogTableTypeIndexProcessingService(), temporal: new sql_storage_engine.Services.Processings.CatalogMetadata.CatalogSystemVersioningProcessingService());
}