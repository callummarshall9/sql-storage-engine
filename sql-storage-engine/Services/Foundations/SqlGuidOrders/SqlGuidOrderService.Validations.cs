// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlGuidOrders;
internal sealed partial class SqlGuidOrderService
{
    private static void ValidateValues(SqlValueComparisonContext sqlValueComparisonContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlValueComparisonContext);
        ArgumentNullException.ThrowIfNull(argument: sqlValueComparisonContext.Left);
        ArgumentNullException.ThrowIfNull(argument: sqlValueComparisonContext.Right);
        Validate(inputs: [(sqlValueComparisonContext.Left is not UniqueIdentifierSqlValue, nameof(sqlValueComparisonContext.Left)), (sqlValueComparisonContext.Right is not UniqueIdentifierSqlValue, nameof(sqlValueComparisonContext.Right))]);
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName);
            }
        }
    }
}