// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlExpressionEnvironments;
internal sealed partial class SqlExpressionEnvironmentOrchestrationService
{
    private static void ValidateSource(SqlExpressionContext sqlExpressionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlExpressionContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: sqlExpressionContext.Source);
    }

    private static void ValidateContext(SqlExpressionContext sqlExpressionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlExpressionContext);
    }
}