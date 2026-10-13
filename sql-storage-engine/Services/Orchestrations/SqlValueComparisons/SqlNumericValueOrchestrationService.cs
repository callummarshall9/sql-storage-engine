// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Numerics;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Foundations.SqlDecimals;
using sql_storage_engine.Services.Foundations.SqlHierarchies;
using sql_storage_engine.Services.Foundations.SqlGuidOrders;

namespace sql_storage_engine.Services.Orchestrations.SqlValueComparisons;
internal sealed partial class SqlNumericValueOrchestrationService : ISqlNumericValueOrchestrationService
{
    private readonly SqlDecimalService decimals;
    private readonly SqlHierarchyIdService hierarchies;
    private readonly SqlGuidOrderService identifiers;
    public SqlNumericValueOrchestrationService(SqlDecimalService decimals, SqlHierarchyIdService hierarchies, SqlGuidOrderService identifiers)
    {
        this.decimals = decimals;
        this.hierarchies = hierarchies;
        this.identifiers = identifiers;
    }

    internal static SqlNumericValueOrchestrationService CreateSqlNumericValueOrchestrationService() =>
        new(decimals: new SqlDecimalService(), hierarchies: new SqlHierarchyIdService(), identifiers: SqlGuidOrderService.CreateSqlGuidOrderService());

    public SqlValueComparisonContext CompareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext) =>
        TryCatch(operation: () =>
    {
        ValidateValues(sqlValueComparisonContext: sqlValueComparisonContext);
        var left = sqlValueComparisonContext.Left!;
        var right = sqlValueComparisonContext.Right!;

        var order = (left, right) switch
        {
            (BooleanSqlValue a, BooleanSqlValue b) => a.Value.CompareTo(value: b.Value),
            (IntegerSqlValue a, IntegerSqlValue b) => a.Value.CompareTo(value: b.Value),
            (DecimalSqlValue a, DecimalSqlValue b) => decimals.CompareSqlDecimals(sqlDecimals: [a.Value, b.Value]),
            (HierarchyIdSqlValue a, HierarchyIdSqlValue b) => hierarchies.CompareSqlHierarchyIds(sqlHierarchyIds: [a.Value, b.Value]),
            (UniqueIdentifierSqlValue, UniqueIdentifierSqlValue) => identifiers.CompareSqlValueComparisonContext(sqlValueComparisonContext: sqlValueComparisonContext)
            .Order,
            (FloatSqlValue a, FloatSqlValue b) => a.Value.CompareTo(value: b.Value),
            (DateSqlValue a, DateSqlValue b) => a.Value.CompareTo(value: b.Value),
            (TimeSqlValue a, TimeSqlValue b) => a.Value.CompareTo(value: b.Value),
            (DateTimeSqlValue a, DateTimeSqlValue b) => a.Value.CompareTo(value: b.Value),
            (DateTimeOffsetSqlValue a, DateTimeOffsetSqlValue b) => a.Value.CompareTo(other: b.Value),
            _ => throw new InvalidOperationException(message: "Unknown scalar value representation.")};

        return sqlValueComparisonContext with
        {
            Order = order
        };
    });

    public SqlValueComparisonContext ParseSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext) =>
        TryCatch(operation: () =>
    {
        ValidateText(sqlValueComparisonContext: sqlValueComparisonContext);

        return sqlValueComparisonContext with
        {
            Exact = decimals.ParseSqlDecimal(value: sqlValueComparisonContext.Text!)
        };
    });

    public SqlValueComparisonContext PrepareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext) =>
        TryCatch(operation: () =>
    {
        ValidateLeft(sqlValueComparisonContext: sqlValueComparisonContext);

        return sqlValueComparisonContext with
        {
            Exact = ToExact(value: sqlValueComparisonContext.Left!)
        };
    });

    public SqlValueComparisonContext CalculateSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext) =>
        TryCatch(operation: () =>
    {
        ValidateValues(sqlValueComparisonContext: sqlValueComparisonContext);

        return sqlValueComparisonContext with
        {
            Result = DecimalArithmetic(left: ToExact(value: sqlValueComparisonContext.Left!), right: ToExact(value: sqlValueComparisonContext.Right!), operation: sqlValueComparisonContext.Operator)
        };
    });

    private SqlDecimal ToExact(SqlValue value) =>
        value switch
    {
        DecimalSqlValue exact => decimals.CreateSqlDecimal(coefficient: exact.Value.Coefficient, scale: exact.Value.Scale),
        IntegerSqlValue integer => decimals.CreateSqlDecimal(coefficient: integer.Value, scale: 0),
        BooleanSqlValue boolean => decimals.CreateSqlDecimal(coefficient: boolean.Value ? 1 : 0, scale: 0),
        TextSqlValue text => decimals.ParseSqlDecimal(value: text.Value),
        _ => throw new ArgumentException(message: "Value cannot participate in exact numeric arithmetic.")};

    private SqlValue DecimalArithmetic(SqlDecimal left, SqlDecimal right, ExpressionTokenKind operation)
    {
        var p1 = DecimalPrecision(value: left);
        var s1 = (int)left.Scale;
        var p2 = DecimalPrecision(value: right);
        var s2 = (int)right.Scale;
        int precision;
        int scale;
        BigInteger coefficient;

        switch (operation)
        {
            case ExpressionTokenKind.Plus:
            case ExpressionTokenKind.Minus:
                scale = Math.Max(val1: s1, val2: s2);
                precision = scale + Math.Max(val1: p1 - s1, val2: p2 - s2) + 1;
                var addLeft = decimals.RescaleSqlDecimal(sqlDecimal: left, scale: checked((byte)scale))
            .Coefficient;
                var addRight = decimals.RescaleSqlDecimal(sqlDecimal: right, scale: checked((byte)scale))
            .Coefficient;
                coefficient = operation == ExpressionTokenKind.Plus ? addLeft + addRight : addLeft - addRight;
                ReduceAdditive(precision: ref precision, scale: ref scale, integralDigits: Math.Max(val1: p1 - s1, val2: p2 - s2));
                break;
            case ExpressionTokenKind.Star:
                precision = p1 + p2 + 1;
                scale = s1 + s2;
                coefficient = left.Coefficient * right.Coefficient;
                ReduceMultiplicative(precision: ref precision, scale: ref scale);
                break;
            case ExpressionTokenKind.Slash:
                if (right.Coefficient.IsZero)
                {
                    throw new DivideByZeroException();
                }

                scale = Math.Max(val1: 6, val2: s1 + p2 + 1);
                precision = p1 - s1 + s2 + scale;
                ReduceMultiplicative(precision: ref precision, scale: ref scale);
                coefficient = DivideRounded(numerator: left.Coefficient, denominator: right.Coefficient, decimalShift: s2 + scale - s1);
                return new DecimalSqlValue
                {
                    StorageFamily = SqlStorageFamily.Decimal,
                    Value = decimals.CreateSqlDecimal(coefficient: coefficient, scale: checked((byte)scale))
                };
            case ExpressionTokenKind.Percent:
                scale = Math.Max(val1: s1, val2: s2);
                precision = Math.Min(val1: p1 - s1, val2: p2 - s2) + scale;
                var modLeft = decimals.RescaleSqlDecimal(sqlDecimal: left, scale: checked((byte)scale))
            .Coefficient;
                var modRight = decimals.RescaleSqlDecimal(sqlDecimal: right, scale: checked((byte)scale))
            .Coefficient;
                if (modRight.IsZero)
                {
                    throw new DivideByZeroException();
                }

                coefficient = modLeft % modRight;
                if (precision > 38)
                {
                    scale = Math.Max(val1: 0, val2: scale - (precision - 38));
                    precision = 38;
                }

                break;
            default:
                throw new InvalidOperationException();
        }

        var intermediateScale = operation == ExpressionTokenKind.Star ? s1 + s2 : Math.Max(val1: s1, val2: s2);
        coefficient = RoundCoefficient(coefficient: coefficient, sourceScale: intermediateScale, targetScale: scale);

        return new DecimalSqlValue
        {
            StorageFamily = SqlStorageFamily.Decimal,
            Value = decimals.CreateSqlDecimal(coefficient: coefficient, scale: checked((byte)scale))
        };
    }

    private int DecimalPrecision(SqlDecimal value) =>
        Math.Max(val1: value.Scale, val2: checked((byte)Math.Max(val1: 1, val2: decimals.CountSqlDecimalDigits(value: BigInteger.Abs(value: value.Coefficient)))));

    private static void ReduceAdditive(ref int precision, ref int scale, int integralDigits)
    {
        if (precision <= 38)
        {
            return;
        }

        scale = Math.Max(val1: 0, val2: 38 - integralDigits);
        precision = 38;
    }

    private static void ReduceMultiplicative(ref int precision, ref int scale)
    {
        if (precision <= 38)
        {
            return;
        }

        var integralDigits = precision - scale;

        if (integralDigits < 32)
        {
            scale = Math.Min(val1: scale, val2: 38 - integralDigits);
        }
        else if (scale > 6)
        {
            scale = 6;
        }

        precision = 38;
    }

    private static BigInteger DivideRounded(BigInteger numerator, BigInteger denominator, int decimalShift)
    {
        if (decimalShift >= 0)
        {
            numerator *= BigInteger.Pow(value: 10, exponent: decimalShift);
        }
        else
        {
            denominator *= BigInteger.Pow(value: 10, exponent: -decimalShift);
        }

        var quotient = BigInteger.DivRem(dividend: numerator, divisor: denominator, remainder: out var remainder);

        if (BigInteger.Abs(value: remainder) * 2 >= BigInteger.Abs(value: denominator))
        {
            quotient += numerator.Sign * denominator.Sign;
        }

        return quotient;
    }

    private static BigInteger RoundCoefficient(BigInteger coefficient, int sourceScale, int targetScale)
    {
        if (targetScale >= sourceScale)
        {
            return coefficient * BigInteger.Pow(value: 10, exponent: targetScale - sourceScale);
        }

        var divisor = BigInteger.Pow(value: 10, exponent: sourceScale - targetScale);
        var quotient = BigInteger.DivRem(dividend: coefficient, divisor: divisor, remainder: out var remainder);

        if (BigInteger.Abs(value: remainder) * 2 >= divisor)
        {
            quotient += coefficient.Sign;
        }

        return quotient;
    }
}