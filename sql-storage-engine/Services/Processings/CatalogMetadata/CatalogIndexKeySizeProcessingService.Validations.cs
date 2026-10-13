// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogIndexKeySizeProcessingService
{
    private static void ValidateSqlType(SqlType type)
    {
        ArgumentNullException.ThrowIfNull(argument: type);
    }
}