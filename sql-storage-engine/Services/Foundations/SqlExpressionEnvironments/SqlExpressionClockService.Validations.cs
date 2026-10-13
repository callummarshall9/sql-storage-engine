// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlExpressionEnvironments;
internal sealed partial class SqlExpressionClockService
{
    private static void ValidateContext(SqlExpressionContext sqlExpressionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlExpressionContext);
    }
}