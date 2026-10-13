// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Managements.CatalogCodecs;
internal sealed partial class CatalogCodecManagementService
{
    private static void ValidateCatalog(CatalogDefinition catalog)
    {
        ArgumentNullException.ThrowIfNull(argument: catalog);
    }

    private static void ValidateSource(byte[] source)
    {
        ArgumentNullException.ThrowIfNull(argument: source);
    }

    private static void ValidateCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        ArgumentNullException.ThrowIfNull(argument: catalogCodecContext);
}