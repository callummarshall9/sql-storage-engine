// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogCodecs;
internal sealed partial class CatalogIndexOptionsOrchestrationService : ICatalogIndexOptionsOrchestrationService
{
    private readonly sql_storage_engine.Services.Processings.SpecializedIndexOptions.SpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService paths;
    private readonly sql_storage_engine.Services.Processings.CatalogFullTexts.SqlCollationCatalogFullTextIndexOptionsProcessingService fullTexts;
    public CatalogIndexOptionsOrchestrationService(sql_storage_engine.Services.Processings.SpecializedIndexOptions.SpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService paths, sql_storage_engine.Services.Processings.CatalogFullTexts.SqlCollationCatalogFullTextIndexOptionsProcessingService fullTexts)
    {
        this.paths = paths;
        this.fullTexts = fullTexts;
    }

    public CatalogCodecContext ProcessCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogCodecContext(catalogCodecContext: catalogCodecContext);

        switch (catalogCodecContext.Operation)
        {
            case 7:
                catalogCodecContext.EncodeCatalogFullTextIndexOptionsResult = fullTexts.EncodeCatalogFullTextIndexOptions(catalogFullTextIndexOptions: catalogCodecContext.CatalogFullTextIndexOptions!);
                return catalogCodecContext;
            case 8:
                catalogCodecContext.DecodeCatalogFullTextIndexOptionsResult = fullTexts.DecodeCatalogFullTextIndexOptions(values: catalogCodecContext.Values!);
                return catalogCodecContext;
            case 22:
                var options = paths.PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: catalogCodecContext.CatalogSpecializedIndexOptions!);
                if (options.Method == CatalogIndexMethod.FullText)
                {
                    options = options with
                    {
                        FullTextOptions = fullTexts.PrepareCatalogFullTextIndexOptions(catalogFullTextIndexOptions: options.FullTextOptions!)
                    };
                }

                catalogCodecContext.PrepareCatalogSpecializedIndexOptionsResult = options;
                return catalogCodecContext;
            case 23:
                catalogCodecContext.PrepareCatalogFullTextIndexOptionsResult = fullTexts.PrepareCatalogFullTextIndexOptions(catalogFullTextIndexOptions: catalogCodecContext.CatalogFullTextIndexOptions!);
                return catalogCodecContext;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(catalogCodecContext.Operation));
        }
    });

    internal static CatalogIndexOptionsOrchestrationService CreateCatalogIndexOptionsOrchestrationService() =>
        new CatalogIndexOptionsOrchestrationService(paths: sql_storage_engine.Services.Processings.SpecializedIndexOptions.SpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService.CreateSpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService(), fullTexts: sql_storage_engine.Services.Processings.CatalogFullTexts.SqlCollationCatalogFullTextIndexOptionsProcessingService.CreateSqlCollationCatalogFullTextIndexOptionsProcessingService());
}