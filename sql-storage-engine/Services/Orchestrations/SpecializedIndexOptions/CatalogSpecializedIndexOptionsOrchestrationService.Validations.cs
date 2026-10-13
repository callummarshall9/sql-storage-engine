// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.SpecializedIndexOptions;
internal sealed partial class CatalogSpecializedIndexOptionsOrchestrationService
{
    private static void ValidateOptions(CatalogSpecializedIndexOptions catalogSpecializedIndexOptions) =>
        ArgumentNullException.ThrowIfNull(argument: catalogSpecializedIndexOptions);
}