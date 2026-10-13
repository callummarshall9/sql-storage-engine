// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Brokers.CatalogIndexDocuments;

namespace sql_storage_engine.Services.Foundations.CatalogIndexDocuments;
internal sealed partial class CatalogIndexDocumentService(CatalogIndexDocumentBroker broker) : ICatalogIndexDocumentService
{
    public CatalogIndexDocumentContext ProcessCatalogIndexDocumentContext(CatalogIndexDocumentContext catalogIndexDocumentContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogIndexDocumentContext(catalogIndexDocumentContext: catalogIndexDocumentContext);

        switch (catalogIndexDocumentContext.Operation)
        {
            case 1:
                catalogIndexDocumentContext.Values = broker.ReadPathValues(json: catalogIndexDocumentContext.Json!, path: catalogIndexDocumentContext.Path!);
                break;
            case 2:
                catalogIndexDocumentContext.Descendants = broker.ReadDescendants(json: catalogIndexDocumentContext.Json!, path: catalogIndexDocumentContext.Path!, normalizedPath: catalogIndexDocumentContext.NormalizedPath!);
                break;
            case 3:
                catalogIndexDocumentContext.Text = broker.NormalizePath(path: catalogIndexDocumentContext.Path!);
                break;
            case 4:
                catalogIndexDocumentContext.Bytes = broker.ReadUtf8Bytes(value: catalogIndexDocumentContext.Text!);
                break;
            case 5:
                catalogIndexDocumentContext.Text = broker.ReadUtf8String(value: catalogIndexDocumentContext.Bytes!);
                break;
        }

        return catalogIndexDocumentContext;
    });

    internal static CatalogIndexDocumentService CreateCatalogIndexDocumentService() =>
        new CatalogIndexDocumentService(broker: new sql_storage_engine.Brokers.CatalogIndexDocuments.CatalogIndexDocumentBroker());
}