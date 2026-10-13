// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Managements.SqlExpressions;
internal sealed partial class SqlExpressionContextManagementService
{
    private static void ValidateContext(SqlExpressionContext sqlExpressionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlExpressionContext);
    }

    private static void ValidateExpression(SqlExpressionContext sqlExpressionContext)
    {
        ValidateContext(sqlExpressionContext: sqlExpressionContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: sqlExpressionContext.Source);

        if (sqlExpressionContext.Columns is not null)
        {
            Validate(inputs: [(sqlExpressionContext.Columns.Any(predicate: pair => pair.Key is null || pair.Value is null), nameof(sqlExpressionContext.Columns))]);
        }
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

    private static void ValidateColumnDefinitions(SqlExpressionContext sqlExpressionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlExpressionContext);
        ArgumentNullException.ThrowIfNull(argument: sqlExpressionContext.ColumnDefinitions);
        Validate(inputs: [(sqlExpressionContext.ColumnDefinitions.Any(predicate: column => column is null), nameof(sqlExpressionContext.ColumnDefinitions))]);
    }

    private static void ValidateColumnReference(SqlExpressionContext sqlExpressionContext)
    {
        ValidateColumnDefinitions(sqlExpressionContext: sqlExpressionContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: sqlExpressionContext.Source);
    }
}