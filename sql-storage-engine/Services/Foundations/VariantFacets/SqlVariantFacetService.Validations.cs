// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.VariantFacets;
internal sealed partial class SqlVariantFacetService
{
    private static void ValidateValues(SqlValueComparisonContext sqlValueComparisonContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlValueComparisonContext);
        ArgumentNullException.ThrowIfNull(argument: sqlValueComparisonContext.Left);
        ArgumentNullException.ThrowIfNull(argument: sqlValueComparisonContext.Right);
        Validate(inputs: [(sqlValueComparisonContext.Left is not VariantSqlValue, nameof(sqlValueComparisonContext.Left)), (sqlValueComparisonContext.Right is not VariantSqlValue, nameof(sqlValueComparisonContext.Right))]);
        ArgumentNullException.ThrowIfNull(argument: ((VariantSqlValue)sqlValueComparisonContext.Left).DeclaredType);
        ArgumentNullException.ThrowIfNull(argument: ((VariantSqlValue)sqlValueComparisonContext.Left).Value);
        ArgumentNullException.ThrowIfNull(argument: ((VariantSqlValue)sqlValueComparisonContext.Right).DeclaredType);
        ArgumentNullException.ThrowIfNull(argument: ((VariantSqlValue)sqlValueComparisonContext.Right).Value);
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