// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Numerics;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlDecimals;
internal interface ISqlDecimalService
{
    SqlDecimal CreateSqlDecimal(BigInteger coefficient, byte scale);

    SqlDecimal CreateSqlDecimalFromDecimal(decimal value);

    SqlDecimal ParseSqlDecimal(string value);

    (bool Exact, BigInteger Coefficient) RescaleSqlDecimal(SqlDecimal sqlDecimal, byte scale);

    BigInteger RoundSqlDecimal(SqlDecimal sqlDecimal, byte scale);

    int CompareSqlDecimals(SqlDecimal[] sqlDecimals);

    int HashSqlDecimal(SqlDecimal sqlDecimal);

    string FormatSqlDecimal(SqlDecimal sqlDecimal);

    int CountSqlDecimalDigits(BigInteger value);
}