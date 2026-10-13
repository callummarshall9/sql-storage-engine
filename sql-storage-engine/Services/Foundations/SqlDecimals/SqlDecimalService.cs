// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Numerics;
using System.Globalization;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlDecimals;
internal sealed partial class SqlDecimalService : ISqlDecimalService
{
    public SqlDecimal CreateSqlDecimal(BigInteger coefficient, byte scale) =>
        TryCatch(operation: () =>
    {
        ValidateCreation(coefficient: coefficient, scale: scale);

        return new SqlDecimal
        {
            Coefficient = coefficient,
            Scale = scale
        };
    });

    public SqlDecimal CreateSqlDecimalFromDecimal(decimal value) =>
        TryCatch(operation: () =>
    {
        ValidateDecimal(value: value);
        var bits = decimal.GetBits(d: value);
        var magnitude = ((BigInteger)(uint)bits[2] << 64) | ((BigInteger)(uint)bits[1] << 32) | (uint)bits[0];

        if ((bits[3] & int.MinValue) != 0)
        {
            magnitude = -magnitude;
        }

        return new SqlDecimal
        {
            Coefficient = magnitude,
            Scale = checked((byte)((bits[3] >> 16) & 255))
        };
    });

    public SqlDecimal ParseSqlDecimal(string value) =>
        TryCatch(operation: () =>
    {
        ValidateLiteral(value: value);
        var negative = value.StartsWith(value: "-", comparisonType: StringComparison.Ordinal);
        var unsigned = value.TrimStart(trimChars: ['+', '-']);
        var point = unsigned.IndexOf(value: '.');
        var scale = point < 0 ? 0 : unsigned.Length - point - 1;
        var digits = point < 0 ? unsigned : unsigned.Remove(startIndex: point, count: 1);

        if (scale > 38 || !BigInteger.TryParse(value: digits, style: NumberStyles.None, provider: CultureInfo.InvariantCulture, result: out var coefficient))
        {
            throw new FormatException(message: "Value is not a SQL decimal literal.");
        }

        coefficient = negative ? -coefficient : coefficient;
        ValidateCreation(coefficient: coefficient, scale: checked((byte)scale));

        return new SqlDecimal
        {
            Coefficient = coefficient,
            Scale = checked((byte)scale)
        };
    });

    public (bool Exact, BigInteger Coefficient) RescaleSqlDecimal(SqlDecimal sqlDecimal, byte scale) =>
        TryCatch(operation: () =>
    {
        ValidateRescale(sqlDecimal: sqlDecimal, scale: scale);
        return Rescale(sqlDecimal: sqlDecimal, scale: scale);
    });

    public BigInteger RoundSqlDecimal(SqlDecimal sqlDecimal, byte scale) =>
        TryCatch(operation: () =>
    {
        ValidateRescale(sqlDecimal: sqlDecimal, scale: scale);
        var result = Rescale(sqlDecimal: sqlDecimal, scale: scale);

        if (scale >= sqlDecimal.Scale)
        {
            return result.Coefficient;
        }

        var divisor = BigInteger.Pow(value: 10, exponent: sqlDecimal.Scale - scale);
        var remainder = sqlDecimal.Coefficient % divisor;
        return BigInteger.Abs(value: remainder) * 2 >= divisor ? result.Coefficient + sqlDecimal.Coefficient.Sign : result.Coefficient;
    });

    public int CompareSqlDecimals(SqlDecimal[] sqlDecimals) =>
        TryCatch(operation: () =>
    {
        ValidatePair(sqlDecimals: sqlDecimals);
        var scale = Math.Max(val1: sqlDecimals[0].Scale, val2: sqlDecimals[1].Scale);

        var left = Rescale(sqlDecimal: sqlDecimals[0], scale: scale)
            .Coefficient;

        var right = Rescale(sqlDecimal: sqlDecimals[1], scale: scale)
            .Coefficient;

        return left.CompareTo(other: right);
    });

    public int HashSqlDecimal(SqlDecimal sqlDecimal) =>
        TryCatch(operation: () =>
    {
        ValidateSqlDecimal(sqlDecimal: sqlDecimal);
        var coefficient = sqlDecimal.Coefficient;
        var scale = sqlDecimal.Scale;

        while (scale > 0 && coefficient % 10 == 0)
        {
            coefficient /= 10;
            scale--;
        }

        return HashCode.Combine(value1: coefficient, value2: scale);
    });

    public string FormatSqlDecimal(SqlDecimal sqlDecimal) =>
        TryCatch(operation: () =>
    {
        ValidateSqlDecimal(sqlDecimal: sqlDecimal);

        var digits = BigInteger.Abs(value: sqlDecimal.Coefficient)
            .ToString(provider: CultureInfo.InvariantCulture)
            .PadLeft(totalWidth: sqlDecimal.Scale + 1, paddingChar: '0');

        if (sqlDecimal.Scale > 0)
        {
            digits = digits.Insert(startIndex: digits.Length - sqlDecimal.Scale, value: ".");
        }

        return sqlDecimal.Coefficient.Sign < 0 ? "-" + digits : digits;
    });

    public int CountSqlDecimalDigits(BigInteger value) =>
        TryCatch(operation: () =>
    {
        ValidateMagnitude(value: value);
        return Digits(value: value);
    });

    private static (bool Exact, BigInteger Coefficient) Rescale(SqlDecimal sqlDecimal, byte scale)
    {
        if (scale >= sqlDecimal.Scale)
        {
            return (true, sqlDecimal.Coefficient * BigInteger.Pow(value: 10, exponent: scale - sqlDecimal.Scale));
        }

        var divisor = BigInteger.Pow(value: 10, exponent: sqlDecimal.Scale - scale);
        var coefficient = BigInteger.DivRem(dividend: sqlDecimal.Coefficient, divisor: divisor, remainder: out var remainder);
        return (remainder.IsZero, coefficient);
    }

    private static int Digits(BigInteger value) =>
        value.IsZero ? 1 : value.ToString(provider: CultureInfo.InvariantCulture)
            .Length;
}