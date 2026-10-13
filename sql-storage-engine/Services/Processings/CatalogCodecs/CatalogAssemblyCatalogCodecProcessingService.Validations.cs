// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal sealed partial class CatalogAssemblyCatalogCodecProcessingService
{
    private static void ValidateCatalogAssembly(CatalogAssembly catalogAssembly)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogAssembly);
    }

    private static void ValidateCatalogAssemblyImage(CatalogAssembly catalogAssembly, byte[] image)
    {
        ValidateCatalogAssembly(catalogAssembly: catalogAssembly);
        ArgumentNullException.ThrowIfNull(argument: image);
    }
}