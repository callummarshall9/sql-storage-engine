// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Overflow;
public static class OverflowPageFormat
{
    public const int HeaderLength = 48;
    public const int NextPageOffset = 32;
    public const int UsedLengthOffset = 41;
}