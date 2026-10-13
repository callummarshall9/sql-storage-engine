// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlValueComparisons;
internal sealed partial class SqlTextVariantOrchestrationService
{
    private static void ValidateLeft(SqlValueComparisonContext sqlValueComparisonContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlValueComparisonContext);
        ArgumentNullException.ThrowIfNull(argument: sqlValueComparisonContext.Left);
    }

    private static void ValidateValues(SqlValueComparisonContext sqlValueComparisonContext)
    {
        ValidateLeft(sqlValueComparisonContext: sqlValueComparisonContext);
        ArgumentNullException.ThrowIfNull(argument: sqlValueComparisonContext.Right);
    }
}