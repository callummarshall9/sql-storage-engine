// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Orchestrations.CatalogIndexShapes;
using sql_storage_engine.Services.Orchestrations.SpecializedIndexOptions;

namespace sql_storage_engine.Services.Coordinations.CatalogIndexes;
internal sealed partial class CatalogIndexCoordinationService(ICatalogIndexShapeOrchestrationService shapes, ICatalogSpecializedIndexOptionsOrchestrationService specialized) : ICatalogIndexCoordinationService
{
    public CatalogIndex PrepareCatalogIndex(CatalogIndex catalogIndex) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogIndex(catalogIndex: catalogIndex);
        var options = catalogIndex.SpecializedOptions;

        if (options is not null)
        {
            options = specialized.PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: options);
        }

        return shapes.PrepareCatalogIndex(catalogIndex: catalogIndex with { SpecializedOptions = options });
    });
}