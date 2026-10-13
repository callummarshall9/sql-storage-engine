// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Numerics;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Foundations.SqlDecimals;

namespace sql_storage_engine.Exposures.Rows;
public static class SqlDecimalExposure
{
    private static readonly ISqlDecimalService service = new SqlDecimalService();
    public static SqlDecimal CreateSqlDecimal(BigInteger coefficient, byte scale) =>
        service.CreateSqlDecimal(coefficient: coefficient, scale: scale);

    public static SqlDecimal CreateSqlDecimalFromDecimal(decimal value) =>
        service.CreateSqlDecimalFromDecimal(value: value);

    public static SqlDecimal ParseSqlDecimal(string value) =>
        service.ParseSqlDecimal(value: value);

    public static bool TryRescaleSqlDecimal(SqlDecimal sqlDecimal, byte scale, out BigInteger coefficient)
    {
        var result = service.RescaleSqlDecimal(sqlDecimal: sqlDecimal, scale: scale);
        coefficient = result.Coefficient;
        return result.Exact;
    }

    public static void RoundSqlDecimalToScale(SqlDecimal sqlDecimal, byte scale, out BigInteger coefficient)
    {
        coefficient = service.RoundSqlDecimal(sqlDecimal: sqlDecimal, scale: scale);
    }

    public static int CompareSqlDecimals(SqlDecimal[] sqlDecimals) =>
        service.CompareSqlDecimals(sqlDecimals: sqlDecimals);

    public static bool CheckEquivalentSqlDecimals(SqlDecimal[] sqlDecimals) =>
        service.CompareSqlDecimals(sqlDecimals: sqlDecimals) == 0;

    public static int HashSqlDecimal(SqlDecimal sqlDecimal) =>
        service.HashSqlDecimal(sqlDecimal: sqlDecimal);

    public static string FormatSqlDecimal(SqlDecimal sqlDecimal) =>
        service.FormatSqlDecimal(sqlDecimal: sqlDecimal);

    public static int CountSqlDecimalDigits(BigInteger value) =>
        service.CountSqlDecimalDigits(value: value);
}