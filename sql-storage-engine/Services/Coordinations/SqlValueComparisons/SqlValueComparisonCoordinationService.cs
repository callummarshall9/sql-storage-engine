// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Orchestrations.SqlValueComparisons;

namespace sql_storage_engine.Services.Coordinations.SqlValueComparisons;
internal sealed partial class SqlValueComparisonCoordinationService : ISqlValueComparisonCoordinationService
{
    private readonly SqlNumericValueOrchestrationService numeric;
    private readonly SqlTextVariantOrchestrationService text;
    public SqlValueComparisonCoordinationService(SqlNumericValueOrchestrationService numeric, SqlTextVariantOrchestrationService text)
    {
        this.numeric = numeric;
        this.text = text;
    }

    internal static SqlValueComparisonCoordinationService CreateSqlValueComparisonCoordinationService() =>
        new(numeric: SqlNumericValueOrchestrationService.CreateSqlNumericValueOrchestrationService(), text: SqlTextVariantOrchestrationService.CreateSqlTextVariantOrchestrationService());

    public SqlValueComparisonContext CompareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext) =>
        TryCatch(operation: () =>
    {
        ValidateValues(sqlValueComparisonContext: sqlValueComparisonContext);
        var left = sqlValueComparisonContext.Left!;
        var right = sqlValueComparisonContext.Right!;

        if (left.IsNull || right.IsNull)
        {
            return sqlValueComparisonContext with
            {
                Comparison = SqlComparison.Unknown
            };
        }

        if (left.StorageFamily != right.StorageFamily)
        {
            throw new ArgumentException(message: "SQL values of different storage families cannot be compared.");
        }

        var input = sqlValueComparisonContext;

        if (left is VariantSqlValue)
        {
            input = text.PrepareSqlValueComparisonContext(sqlValueComparisonContext: input);

            if (input.Order != 0 || input.VariantFamily == 6)
            {
                return Complete(sqlValueComparisonContext: input);
            }

            if (input.VariantFamily == 3)
            {
                return Complete(sqlValueComparisonContext: text.CompareSqlValueComparisonContext(sqlValueComparisonContext: sqlValueComparisonContext));
            }
        }

        input = input.Left is TextSqlValue or BinarySqlValue ? text.CompareSqlValueComparisonContext(sqlValueComparisonContext: input) : numeric.CompareSqlValueComparisonContext(sqlValueComparisonContext: input);
        return Complete(sqlValueComparisonContext: input);
    });

    private static SqlValueComparisonContext Complete(SqlValueComparisonContext sqlValueComparisonContext) =>
        sqlValueComparisonContext with
    {
        Comparison = sqlValueComparisonContext.Order switch
        {
            < 0 => SqlComparison.Less,
            > 0 => SqlComparison.Greater,
            _ => SqlComparison.Equal
        }
    };

    public SqlValueComparisonContext ParseSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext) =>
        TryCatch(operation: () =>
    {
        ValidateText(sqlValueComparisonContext: sqlValueComparisonContext);
        return numeric.ParseSqlValueComparisonContext(sqlValueComparisonContext: sqlValueComparisonContext);
    });

    public SqlValueComparisonContext PrepareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext) =>
        TryCatch(operation: () =>
    {
        ValidateLeft(sqlValueComparisonContext: sqlValueComparisonContext);
        return numeric.PrepareSqlValueComparisonContext(sqlValueComparisonContext: sqlValueComparisonContext);
    });

    public SqlValueComparisonContext CalculateSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext) =>
        TryCatch(operation: () =>
    {
        ValidateValues(sqlValueComparisonContext: sqlValueComparisonContext);
        return numeric.CalculateSqlValueComparisonContext(sqlValueComparisonContext: sqlValueComparisonContext);
    });
}