// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Foundations.CatalogIndexDocuments;
internal sealed partial class CatalogIndexDocumentService
{
    private static void ValidateCatalogIndexDocumentContext(CatalogIndexDocumentContext catalogIndexDocumentContext)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogIndexDocumentContext);

        if (catalogIndexDocumentContext.Operation is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(catalogIndexDocumentContext));
        }

        if (catalogIndexDocumentContext.Operation is 1 or 2)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogIndexDocumentContext.Json);
        }

        if (catalogIndexDocumentContext.Operation is 1 or 2 or 3)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogIndexDocumentContext.Path);
        }

        if (catalogIndexDocumentContext.Operation == 2)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogIndexDocumentContext.NormalizedPath);
        }

        if (catalogIndexDocumentContext.Operation == 4)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogIndexDocumentContext.Text);
        }

        if (catalogIndexDocumentContext.Operation == 5)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogIndexDocumentContext.Bytes);
        }
    }
}