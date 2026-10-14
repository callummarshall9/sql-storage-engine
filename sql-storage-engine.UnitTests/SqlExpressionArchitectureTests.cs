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
    [TestCase("NULL", "AND", "NULL", null)]
    [TestCase("NULL", "AND", "0", false)]
    [TestCase("NULL", "AND", "1", null)]
    [TestCase("0", "AND", "NULL", false)]
    [TestCase("0", "AND", "0", false)]
    [TestCase("0", "AND", "1", false)]
    [TestCase("1", "AND", "NULL", null)]
    [TestCase("1", "AND", "0", false)]
    [TestCase("1", "AND", "1", true)]
    [TestCase("NULL", "OR", "NULL", null)]
    [TestCase("NULL", "OR", "0", null)]
    [TestCase("NULL", "OR", "1", true)]
    [TestCase("0", "OR", "NULL", null)]
    [TestCase("0", "OR", "0", false)]
    [TestCase("0", "OR", "1", true)]
    [TestCase("1", "OR", "NULL", true)]
    [TestCase("1", "OR", "0", true)]
    [TestCase("1", "OR", "1", true)]
    [TestCase("0", "AND", "'not a bit'", false)]
    [TestCase("1", "OR", "'not a bit'", true)]
    public void ExpressionManagement_PreservesThreeValuedLogicAndConversionShortCircuit(string left, string operation, string right, bool? expected)
    {
        var result = SqlExpressionExposure.Evaluate(expression: $"{left} {operation} {right}");

        if (expected is { } boolean)
        {
            result.Should().Be(SqlValueExposure.CreateBooleanSqlValue(value: boolean));
        }
        else
        {
            result.Should().BeOfType<NullSqlValue>();
        }
    }

    [TestCase("AND", "0")]
    [TestCase("OR", "1")]
    public void ExpressionManagement_ConvertsLeftBeforeDecisiveRight(string operation, string right)
    {
        ((Action)(() => SqlExpressionExposure.Evaluate(expression: $"'not a bit' {operation} {right}")))
            .Should().Throw<sql_storage_engine.Models.Exceptions.StorageModelServiceException>()
            .Which.GetBaseException().Should().BeOfType<FormatException>();
    }

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