// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.SqlExpressionSyntax;
internal sealed partial class SqlExpressionTokenProcessingService
{
    private static void ValidateSource(SqlExpressionContext sqlExpressionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlExpressionContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: sqlExpressionContext.Source);
    }
}