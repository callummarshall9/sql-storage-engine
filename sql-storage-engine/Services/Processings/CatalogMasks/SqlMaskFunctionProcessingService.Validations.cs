// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogMasks;
internal sealed partial class SqlMaskFunctionProcessingService
{
    private static void ValidateSqlMaskContext(SqlMaskContext sqlMaskContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlMaskContext);
        ArgumentNullException.ThrowIfNull(argument: sqlMaskContext.Function);
    }
}