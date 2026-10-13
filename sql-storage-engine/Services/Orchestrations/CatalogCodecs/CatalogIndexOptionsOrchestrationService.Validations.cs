// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogCodecs;
internal sealed partial class CatalogIndexOptionsOrchestrationService
{
    private static void ValidateCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        ArgumentNullException.ThrowIfNull(argument: catalogCodecContext);
}