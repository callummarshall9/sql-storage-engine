// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal sealed partial class SqlCollationCatalogTextEncodingProcessingService
{
    private static void ValidateText(CatalogTextEncodingContext catalogTextEncodingContext)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogTextEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: catalogTextEncodingContext.Text);
    }

    private static void ValidateBytes(CatalogTextEncodingContext catalogTextEncodingContext)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogTextEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: catalogTextEncodingContext.Bytes);
    }
}