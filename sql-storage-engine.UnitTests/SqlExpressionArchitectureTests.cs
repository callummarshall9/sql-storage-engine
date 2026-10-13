// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Managements.SqlExpressions;
using sql_storage_engine.Services.Coordinations.SqlValueComparisons;

namespace sql_storage_engine.UnitTests;
public sealed class SqlExpressionArchitectureTests
{
    [Test]
    public void ExpressionManagement_PreservesNestedNamesAndSqlNullLogic()
    {
        var columns = new Dictionary<string, SqlValue>(comparer: StringComparer.OrdinalIgnoreCase)
        {
            ["order value"] = SqlValueExposure.CreateIntegerSqlValue(value: 7)
        };

        SqlExpressionExposure.Evaluate(expression: "([order value] * 3) + LEN(N'a''b  ')", columns: columns)
            .Should()
            .Be(SqlValueExposure.CreateIntegerSqlValue(value: 24));

        SqlExpressionExposure.Evaluate(expression: "NULL AND 0")
            .Should()
            .Be(SqlValueExposure.CreateBooleanSqlValue(value: false));

        SqlExpressionExposure.Evaluate(expression: "NULL OR 1")
            .Should()
            .Be(SqlValueExposure.CreateBooleanSqlValue(value: true));

        SqlExpressionExposure.Evaluate(expression: "0x0010 < 0x0020")
            .Should()
            .Be(SqlValueExposure.CreateBooleanSqlValue(value: true));
    }

    [Test]
    public void ExpressionManagement_CanReplaceAndClearHostRuntime()
    {
        var service = SqlExpressionContextManagementService.CreateSqlExpressionContextManagementService();
        service.RegisterSqlExpressionContext(sqlExpressionContext: new SqlExpressionContext { Runtime = (source, columns) => SqlValueExposure.CreateTextSqlValue(value: source) });

        service.EvaluateSqlExpressionContext(sqlExpressionContext: new SqlExpressionContext { Source = "host function" })
            .Result.Should()
            .Be(SqlValueExposure.CreateTextSqlValue(value: "host function"));

        service.RegisterSqlExpressionContext(sqlExpressionContext: new SqlExpressionContext());

        service.EvaluateSqlExpressionContext(sqlExpressionContext: new SqlExpressionContext { Source = "1 + 2" })
            .Result.Should()
            .Be(SqlValueExposure.CreateIntegerSqlValue(value: 3));
    }

    [Test]
    public void ValueComparison_UsesSqlGuidByteOrderAndVariantNumericPromotion()
    {
        var service = SqlValueComparisonCoordinationService.CreateSqlValueComparisonCoordinationService();
        var left = Guid.Parse(input: "ffffffff-ffff-ffff-ffff-000000000000");
        var right = Guid.Parse(input: "00000000-0000-0000-0000-000000000001");

        left.CompareTo(value: right)
            .Should()
            .BePositive();

        service.CompareSqlValueComparisonContext(sqlValueComparisonContext: new SqlValueComparisonContext { Left = SqlValueExposure.CreateUniqueIdentifierSqlValue(value: left), Right = SqlValueExposure.CreateUniqueIdentifierSqlValue(value: right) })
            .Comparison.Should()
            .Be(SqlComparison.Less);

        service.CompareSqlValueComparisonContext(sqlValueComparisonContext: new SqlValueComparisonContext { Left = SqlValueExposure.CreateVariantSqlValue(sqlType: SqlTypeExposure.ReadSqlTypeBigInt(), sqlValue: SqlValueExposure.CreateIntegerSqlValue(value: 2)), Right = SqlValueExposure.CreateVariantSqlValue(sqlType: SqlTypeExposure.CreateDecimalSqlType(precision: 3, scale: 1), sqlValue: SqlValueExposure.CreateDecimalSqlValue(sqlDecimal: SqlDecimalExposure.CreateSqlDecimal(coefficient: 20, scale: 1))) })
            .Comparison.Should()
            .Be(SqlComparison.Equal);
    }
}