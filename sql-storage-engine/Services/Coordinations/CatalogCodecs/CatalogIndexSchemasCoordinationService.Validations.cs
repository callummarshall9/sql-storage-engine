// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Coordinations.CatalogCodecs;
internal sealed partial class CatalogIndexSchemasCoordinationService
{
    private static void ValidateCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        ArgumentNullException.ThrowIfNull(argument: catalogCodecContext);
}