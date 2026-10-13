// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Overflow;
public static class OverflowReferenceFormat
{
    public const int EncodedLength = 16;
    public const long MaximumValueLength = int.MaxValue;
    public const int MaximumChainLength = 530_505;
}