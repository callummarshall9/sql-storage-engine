// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Coordinations.CatalogIndexes;
internal sealed partial class CatalogIndexCoordinationService
{
    private static void ValidateCatalogIndex(CatalogIndex catalogIndex) =>
        ArgumentNullException.ThrowIfNull(argument: catalogIndex);
}