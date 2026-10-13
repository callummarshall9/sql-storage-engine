// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Coordinations.CatalogIndexProjections;
internal sealed partial class CatalogIndexProjectionContextCoordinationService : ICatalogIndexProjectionContextCoordinationService
{
    private readonly sql_storage_engine.Services.Orchestrations.CatalogIndexDocuments.CatalogIndexDocumentProjectionOrchestrationService documents;
    private readonly sql_storage_engine.Services.Orchestrations.CatalogIndexShapes.CatalogIndexProjectionShapeOrchestrationService shapes;
    public CatalogIndexProjectionContextCoordinationService(sql_storage_engine.Services.Orchestrations.CatalogIndexDocuments.CatalogIndexDocumentProjectionOrchestrationService documents, sql_storage_engine.Services.Orchestrations.CatalogIndexShapes.CatalogIndexProjectionShapeOrchestrationService shapes)
    {
        this.documents = documents;
        this.shapes = shapes;
    }

    public CatalogIndexProjectionContext ProcessCatalogIndexProjectionContext(CatalogIndexProjectionContext catalogIndexProjectionContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogIndexProjectionContext(catalogIndexProjectionContext: catalogIndexProjectionContext);

        if (catalogIndexProjectionContext.Operation <= 5 || catalogIndexProjectionContext.Operation == 16)
        {
            return documents.ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: catalogIndexProjectionContext);
        }

        return shapes.ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: catalogIndexProjectionContext);
    });

    internal static CatalogIndexProjectionContextCoordinationService CreateCatalogIndexProjectionContextCoordinationService() =>
        new CatalogIndexProjectionContextCoordinationService(documents: sql_storage_engine.Services.Orchestrations.CatalogIndexDocuments.CatalogIndexDocumentProjectionOrchestrationService.CreateCatalogIndexDocumentProjectionOrchestrationService(), shapes: sql_storage_engine.Services.Orchestrations.CatalogIndexShapes.CatalogIndexProjectionShapeOrchestrationService.CreateCatalogIndexProjectionShapeOrchestrationService());
}