// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.SqlExpressionSyntax;
internal sealed partial class SqlExpressionCursorProcessingService
{
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
}