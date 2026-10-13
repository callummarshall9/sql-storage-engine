// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogCodecs;
internal sealed partial class CatalogTableShapesOrchestrationService : ICatalogTableShapesOrchestrationService
{
    private readonly sql_storage_engine.Services.Processings.CatalogMetadata.CatalogCheckConstraintProcessingService checks;
    private readonly sql_storage_engine.Services.Processings.CatalogMetadata.CatalogTableProcessingService tables;
    private readonly sql_storage_engine.Services.Processings.CatalogGraphs.CatalogGraphTableProcessingService graphs;
    public CatalogTableShapesOrchestrationService(sql_storage_engine.Services.Processings.CatalogMetadata.CatalogCheckConstraintProcessingService checks, sql_storage_engine.Services.Processings.CatalogMetadata.CatalogTableProcessingService tables, sql_storage_engine.Services.Processings.CatalogGraphs.CatalogGraphTableProcessingService graphs)
    {
        this.checks = checks;
        this.tables = tables;
        this.graphs = graphs;
    }

    public CatalogCodecContext ProcessCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogCodecContext(catalogCodecContext: catalogCodecContext);

        switch (catalogCodecContext.Operation)
        {
            case 11:
                catalogCodecContext.PrepareCatalogGraphTableResult = graphs.PrepareCatalogGraphTable(catalogGraphTable: catalogCodecContext.CatalogGraphTable!);
                return catalogCodecContext;
            case 12:
                catalogCodecContext.PrepareCatalogTableResult = tables.PrepareCatalogTable(catalogTable: catalogCodecContext.CatalogTable!);
                return catalogCodecContext;
            case 9:
                catalogCodecContext.PrepareCatalogCheckConstraintResult = checks.PrepareCatalogCheckConstraint(catalogCheckConstraint: catalogCodecContext.CatalogCheckConstraint!);
                return catalogCodecContext;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(catalogCodecContext.Operation));
        }
    });

    internal static CatalogTableShapesOrchestrationService CreateCatalogTableShapesOrchestrationService() =>
        new CatalogTableShapesOrchestrationService(checks: new sql_storage_engine.Services.Processings.CatalogMetadata.CatalogCheckConstraintProcessingService(), tables: new sql_storage_engine.Services.Processings.CatalogMetadata.CatalogTableProcessingService(), graphs: new sql_storage_engine.Services.Processings.CatalogGraphs.CatalogGraphTableProcessingService());
}