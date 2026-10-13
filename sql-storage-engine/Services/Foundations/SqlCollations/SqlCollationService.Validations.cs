// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlCollations;
internal sealed partial class SqlCollationService
{
    private static void ValidateSqlCollationName(string name) =>
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: name) || name.Length > 128, "SQL collation names must contain between 1 and 128 characters.")]);

    private static void ValidateSqlCollationCreation(string name, int lcid, byte version, SqlCollationComparisonFlags comparisonFlags, byte sortId, int codePage, string cultureName)
    {
        ValidateSqlCollationName(name: name);
        Validate(inputs: [(lcid is < 0 or > ushort.MaxValue, "Invalid locale identifier."), (codePage is < 1 or > ushort.MaxValue, "Invalid code page.")]);
        host.CheckHostCollation(cultureName: cultureName, codePage: codePage);
    }

    private static void ValidateSqlCollationMetadata(SqlCollation sqlCollation)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlCollation);
        ValidateSqlCollationCreation(name: sqlCollation.Name, lcid: sqlCollation.Lcid, version: sqlCollation.Version, comparisonFlags: sqlCollation.ComparisonFlags, sortId: sqlCollation.SortId, codePage: sqlCollation.CodePage, cultureName: sqlCollation.CultureName);
    }

    private static void ValidateSqlCollationText(SqlCollation sqlCollation, string value)
    {
        ValidateSqlCollationMetadata(sqlCollation: sqlCollation);
        ArgumentNullException.ThrowIfNull(argument: value);
    }

    private static void ValidateSqlCollationSortKey(SqlCollation sqlCollation, string value, bool unicode) =>
        ValidateSqlCollationText(sqlCollation: sqlCollation, value: value);

    private static void ValidateSqlCollationBytes(SqlCollation sqlCollation, ReadOnlySpan<byte> value) =>
        ValidateSqlCollationMetadata(sqlCollation: sqlCollation);

    private static void ValidateSqlCollationRuntime(string collationName, Func<string, bool, byte[]> runtime)
    {
        ValidateSqlCollationName(name: collationName);
        ArgumentNullException.ThrowIfNull(argument: runtime);
    }

    private static void ValidateSqlCollationComparison(SqlCollation[] sqlCollations)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlCollations);
        Validate(inputs: [(sqlCollations.Length != 2, "Exactly two collations must be compared.")]);
        ValidateSqlCollationMetadata(sqlCollation: sqlCollations[0]);
        ValidateSqlCollationMetadata(sqlCollation: sqlCollations[1]);
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: input.Message);
            }
        }
    }

    private static void ValidateSqlCollationUtf8(string value) =>
        ArgumentNullException.ThrowIfNull(argument: value);

    private static void ValidateSqlCollationUtf8Destination(Span<byte> destination, string value)
    {
        ValidateSqlCollationUtf8(value: value);
        Validate(inputs: [(destination.Length < host.CountCodePageBytes(codePage: 65001, value: value), "UTF-8 destination is too short.")]);
    }

    private static void ValidateSqlCollationUtf8Source(ReadOnlySpan<byte> value)
    {
        if (!value.IsEmpty)
        {
            _ = host.DecodeCodePage(codePage: 65001, value: value);
        }
    }
}