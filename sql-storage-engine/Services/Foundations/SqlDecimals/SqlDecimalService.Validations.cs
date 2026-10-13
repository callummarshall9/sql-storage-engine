// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Numerics;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlDecimals;
internal sealed partial class SqlDecimalService
{
    private static void ValidateCreation(BigInteger coefficient, byte scale)
    {
        if (scale > 38)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(scale));
        }

        if (Digits(value: BigInteger.Abs(value: coefficient)) > 38)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(coefficient));
        }
    }

    private static void ValidateSqlDecimal(SqlDecimal sqlDecimal)
    {
        ValidateCreation(coefficient: sqlDecimal.Coefficient, scale: sqlDecimal.Scale);
    }

    private static void ValidateRescale(SqlDecimal sqlDecimal, byte scale)
    {
        ValidateSqlDecimal(sqlDecimal: sqlDecimal);
    }

    private static void ValidatePair(SqlDecimal[] sqlDecimals)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlDecimals);

        if (sqlDecimals.Length != 2)
        {
            throw new ArgumentException(message: "Decimal comparison requires two values.", paramName: nameof(sqlDecimals));
        }

        foreach (var value in sqlDecimals)
        {
            ValidateSqlDecimal(sqlDecimal: value);
        }
    }

    private static void ValidateLiteral(string value)
    {
        ArgumentNullException.ThrowIfNull(argument: value);
    }

    private static void ValidateDecimal(decimal value)
    {
    }

    private static void ValidateMagnitude(BigInteger value)
    {
    }
}