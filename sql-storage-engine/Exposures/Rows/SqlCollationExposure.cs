// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Text;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Foundations.SqlCollations;

namespace sql_storage_engine.Exposures.Rows;
public static class SqlCollationExposure
{
    private static readonly ISqlCollationService service = new SqlCollationService();
    public static SqlCollation CreateSqlCollation(string name, int lcid, byte version, SqlCollationComparisonFlags comparisonFlags, byte sortId, int codePage, string cultureName) =>
        service.CreateSqlCollation(name: name, lcid: lcid, version: version, comparisonFlags: comparisonFlags, sortId: sortId, codePage: codePage, cultureName: cultureName);

    public static SqlCollation ParseSqlCollation(string name) =>
        service.ParseSqlCollation(name: name);

    public static SqlCollation ReadDefaultSqlCollation() =>
        service.ReadDefaultSqlCollation();

    public static void RegisterSqlCollation(SqlCollation sqlCollation) =>
        service.RegisterSqlCollation(sqlCollation: sqlCollation);

    public static void RegisterSqlCollationRuntime(string collationName, Func<string, bool, byte[]> runtime) =>
        service.RegisterSqlCollationRuntime(collationName: collationName, runtime: runtime);

    public static Encoding ReadSqlCollationEncoding(SqlCollation sqlCollation) =>
        service.ReadSqlCollationEncoding(sqlCollation: sqlCollation);

    public static int CountSqlCollationBytes(SqlCollation sqlCollation, string value) =>
        service.CountSqlCollationBytes(sqlCollation: sqlCollation, value: value);

    public static byte[] EncodeSqlCollation(SqlCollation sqlCollation, string value) =>
        service.EncodeSqlCollation(sqlCollation: sqlCollation, value: value);

    public static string DecodeSqlCollation(SqlCollation sqlCollation, ReadOnlySpan<byte> value) =>
        service.DecodeSqlCollation(sqlCollation: sqlCollation, value: value);

    public static byte[] CalculateSqlCollationSortKey(SqlCollation sqlCollation, string value, bool unicode) =>
        service.CalculateSqlCollationSortKey(sqlCollation: sqlCollation, value: value, unicode: unicode);

    public static int CompareSqlCollationMetadata(SqlCollation[] sqlCollations) =>
        service.CompareSqlCollationMetadata(sqlCollations: sqlCollations);

    public static bool CheckSqlCollationIsUtf8(SqlCollation sqlCollation) =>
        service.CheckSqlCollationIsUtf8(sqlCollation: sqlCollation);

    public static bool CheckSqlCollationIsBinary(SqlCollation sqlCollation) =>
        service.CheckSqlCollationIsBinary(sqlCollation: sqlCollation);

    public static bool CheckSqlCollationIsBinary2(SqlCollation sqlCollation) =>
        service.CheckSqlCollationIsBinary2(sqlCollation: sqlCollation);

    public static int CountSqlCollationUtf8(string value) =>
        service.CountSqlCollationUtf8(value: value);

    public static void WriteSqlCollationUtf8(Span<byte> destination, string value) =>
        service.WriteSqlCollationUtf8(destination: destination, value: value);

    public static string ReadSqlCollationUtf8(ReadOnlySpan<byte> value) =>
        service.ReadSqlCollationUtf8(value: value);

    public static void CheckSqlCollationName(string name) =>
        service.CheckSqlCollationName(name: name);
}