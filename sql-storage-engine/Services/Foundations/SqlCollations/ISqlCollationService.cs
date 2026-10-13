// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Text;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlCollations;
internal interface ISqlCollationService
{
    SqlCollation CreateSqlCollation(string name, int lcid, byte version, SqlCollationComparisonFlags comparisonFlags, byte sortId, int codePage, string cultureName);

    SqlCollation ParseSqlCollation(string name);

    SqlCollation ReadDefaultSqlCollation();

    void RegisterSqlCollation(SqlCollation sqlCollation);

    void RegisterSqlCollationRuntime(string collationName, Func<string, bool, byte[]> runtime);

    Encoding ReadSqlCollationEncoding(SqlCollation sqlCollation);

    int CountSqlCollationBytes(SqlCollation sqlCollation, string value);

    byte[] EncodeSqlCollation(SqlCollation sqlCollation, string value);

    string DecodeSqlCollation(SqlCollation sqlCollation, ReadOnlySpan<byte> value);

    byte[] CalculateSqlCollationSortKey(SqlCollation sqlCollation, string value, bool unicode);

    int CompareSqlCollationMetadata(SqlCollation[] sqlCollations);

    bool CheckSqlCollationIsUtf8(SqlCollation sqlCollation);

    bool CheckSqlCollationIsBinary(SqlCollation sqlCollation);

    bool CheckSqlCollationIsBinary2(SqlCollation sqlCollation);

    int CountSqlCollationUtf8(string value);

    void WriteSqlCollationUtf8(Span<byte> destination, string value);

    string ReadSqlCollationUtf8(ReadOnlySpan<byte> value);

    void CheckSqlCollationName(string name);
}