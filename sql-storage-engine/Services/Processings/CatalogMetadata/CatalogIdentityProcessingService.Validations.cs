// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogIdentityProcessingService
{
    private static void ValidateCatalogIdentity(CatalogIdentity catalogIdentity)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogIdentity);
        var seed = catalogIdentity.Seed;
        var increment = catalogIdentity.Increment;
        var isNotForReplication = catalogIdentity.IsNotForReplication;

        if (increment.IsZero)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(increment));
        }
    }
}