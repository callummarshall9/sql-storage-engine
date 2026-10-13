// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Rows;
public static class RowFormat
{
    public const ushort FormatVersion = 4;
    public const int HeaderLength = 32;
    public const int MaximumEncodedLength = 16 * 1024 * 1024;
    public const int MaximumInlineValueLength = 1024 * 1024;
    public const int VariableEntryLength = 12;
}