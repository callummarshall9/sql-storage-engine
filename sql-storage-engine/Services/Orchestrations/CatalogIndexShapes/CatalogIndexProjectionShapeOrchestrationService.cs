// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogIndexShapes;
internal sealed partial class CatalogIndexProjectionShapeOrchestrationService : ICatalogIndexProjectionShapeOrchestrationService
{
    private readonly sql_storage_engine.Services.Processings.FullTextTokens.FullTextTokenProcessingService tokens;
    private readonly sql_storage_engine.Services.Processings.SpecializedIndexOptions.SpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService paths;
    private readonly sql_storage_engine.Services.Processings.TableDefinitions.TableDefinitionProcessingService tables;
    public CatalogIndexProjectionShapeOrchestrationService(sql_storage_engine.Services.Processings.FullTextTokens.FullTextTokenProcessingService tokens, sql_storage_engine.Services.Processings.SpecializedIndexOptions.SpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService paths, sql_storage_engine.Services.Processings.TableDefinitions.TableDefinitionProcessingService tables)
    {
        this.tokens = tokens;
        this.paths = paths;
        this.tables = tables;
    }

    public CatalogIndexProjectionContext ProcessCatalogIndexProjectionContext(CatalogIndexProjectionContext catalogIndexProjectionContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogIndexProjectionContext(catalogIndexProjectionContext: catalogIndexProjectionContext);

        switch (catalogIndexProjectionContext.Operation)
        {
            case 17:
                catalogIndexProjectionContext.TableDefinition = tables.PrepareTableDefinition(tableDefinition: catalogIndexProjectionContext.TableDefinition!);
                break;
            case 18:
                tables.CheckTableDefinitionRow(tableDefinition: catalogIndexProjectionContext.TableDefinition!);
                break;
            case 6:
                catalogIndexProjectionContext.Tokens = tokens.TokenizeCatalogFullTextIndexOptionsDocument(document: catalogIndexProjectionContext.Text!, catalogFullTextIndexOptions: catalogIndexProjectionContext.FullTextOptions!);
                break;
            case 7:
                catalogIndexProjectionContext.Text = tokens.ParseCatalogFullTextIndexOptionsExactTerm(term: catalogIndexProjectionContext.Text!, catalogFullTextIndexOptions: catalogIndexProjectionContext.FullTextOptions!);
                break;
            case 8:
                catalogIndexProjectionContext.SpecializedOptions = paths.PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: catalogIndexProjectionContext.SpecializedOptions!);
                break;
        }

        return catalogIndexProjectionContext;
    });

    internal static CatalogIndexProjectionShapeOrchestrationService CreateCatalogIndexProjectionShapeOrchestrationService() =>
        new CatalogIndexProjectionShapeOrchestrationService(tokens: new sql_storage_engine.Services.Processings.FullTextTokens.FullTextTokenProcessingService(), paths: sql_storage_engine.Services.Processings.SpecializedIndexOptions.SpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService.CreateSpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService(), tables: new sql_storage_engine.Services.Processings.TableDefinitions.TableDefinitionProcessingService());
}