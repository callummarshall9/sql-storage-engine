// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Rows;
/// <summary>Optional host binding for byte-exact SQL Server collation weights.</summary>
public interface ISqlCollationRuntime
{
    byte[] GetSortKey(string value, bool unicode);
}

[Flags]
public enum SqlCollationComparisonFlags : ushort
{
    None = 0,
    IgnoreCase = 1,
    IgnoreNonSpace = 2,
    IgnoreKanaType = 4,
    IgnoreWidth = 8,
    Binary = 16,
    Binary2 = 32,
    SupplementaryCharacters = 64,
    VariationSelectors = 128,
    Utf8 = 256
}