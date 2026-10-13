// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.SqlExpressions;
internal sealed partial class SqlExpressionRuntimeCoordinationService
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

    private static void ValidateTokens(SqlExpressionContext sqlExpressionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlExpressionContext);
        ArgumentNullException.ThrowIfNull(argument: sqlExpressionContext.Tokens);
        Validate(inputs: [(sqlExpressionContext.Position < 0 || sqlExpressionContext.Position >= sqlExpressionContext.Tokens.Length, nameof(sqlExpressionContext.Position))]);
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