// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers;
using System.Linq;
using System;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Services.Coordinations.CatalogCodecs;
internal sealed partial class CatalogIndexSchemasCoordinationService : ICatalogIndexSchemasCoordinationService
{
    private readonly sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogIndexShapesOrchestrationService catalogIndexShapes;
    private readonly sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogIndexOptionsOrchestrationService catalogIndexOptions;
    public CatalogIndexSchemasCoordinationService(sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogIndexShapesOrchestrationService catalogIndexShapes, sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogIndexOptionsOrchestrationService catalogIndexOptions)
    {
        this.catalogIndexShapes = catalogIndexShapes;
        this.catalogIndexOptions = catalogIndexOptions;
    }

    public CatalogCodecContext ProcessCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogCodecContext(catalogCodecContext: catalogCodecContext);

        switch (catalogCodecContext.Operation)
        {
            case 24:
                var index = catalogCodecContext.CatalogIndex!;
                ArgumentNullException.ThrowIfNull(argument: index);
                ArgumentNullException.ThrowIfNull(argument: index.ColumnSource);
                var columns = index.ColumnSource.Select(selector: column => catalogIndexShapes.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 13, CatalogIndexedColumn = column })
            .PrepareCatalogIndexedColumnResult!)
            .ToArray();
                var included = (index.IncludedColumnSource ?? Array.Empty<ColumnId>()).ToArray();
                var options = index.SpecializedOptions is null ? null : catalogIndexOptions.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 22, CatalogSpecializedIndexOptions = index.SpecializedOptions })
            .PrepareCatalogSpecializedIndexOptionsResult;
                catalogCodecContext.CatalogIndex = index with
                {
                    Columns = Array.AsReadOnly(array: columns),
                    IncludedColumns = Array.AsReadOnly(array: included),
                    SpecializedOptions = options
                };
                return catalogIndexShapes.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
            case 13:
                return catalogIndexShapes.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
            case 7:
            case 8:
            case 22:
            case 23:
                return catalogIndexOptions.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(catalogCodecContext.Operation));
        }
    });

    internal static CatalogIndexSchemasCoordinationService CreateCatalogIndexSchemasCoordinationService() =>
        new CatalogIndexSchemasCoordinationService(catalogIndexShapes: sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogIndexShapesOrchestrationService.CreateCatalogIndexShapesOrchestrationService(), catalogIndexOptions: sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogIndexOptionsOrchestrationService.CreateCatalogIndexOptionsOrchestrationService());
}