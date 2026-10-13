// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlValueComparisons;
internal sealed partial class SqlNumericValueOrchestrationService
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

    private static void ValidateText(SqlValueComparisonContext sqlValueComparisonContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlValueComparisonContext);
        ArgumentNullException.ThrowIfNull(argument: sqlValueComparisonContext.Text);
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: sqlValueComparisonContext.Text), nameof(sqlValueComparisonContext.Text))]);
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