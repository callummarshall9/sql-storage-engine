// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Processings.SpecializedIndexOptions;
using sql_storage_engine.Services.Processings.CatalogFullTexts;

namespace sql_storage_engine.Services.Orchestrations.SpecializedIndexOptions;
internal sealed partial class CatalogSpecializedIndexOptionsOrchestrationService(ISpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService specialized, ISqlCollationCatalogFullTextIndexOptionsProcessingService fullTexts) : ICatalogSpecializedIndexOptionsOrchestrationService
{
    public CatalogSpecializedIndexOptions PrepareCatalogSpecializedIndexOptions(CatalogSpecializedIndexOptions catalogSpecializedIndexOptions) =>
        TryCatch(operation: () =>
    {
        ValidateOptions(catalogSpecializedIndexOptions: catalogSpecializedIndexOptions);
        var prepared = specialized.PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: catalogSpecializedIndexOptions);

        if (prepared.Method == CatalogIndexMethod.FullText)
        {
            prepared = prepared with
            {
                FullTextOptions = fullTexts.PrepareCatalogFullTextIndexOptions(catalogFullTextIndexOptions: prepared.FullTextOptions!)
            };
        }

        return prepared;
    });
}