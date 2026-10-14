// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Logging;
public static class WalFormatConstants
{
    public const ushort Version = 1;
    public const int SegmentHeaderLength = 48;
    public const int RecordHeaderLength = 40;
    public const int MaximumPayloadLength = 16 * 1024 * 1024;
    internal const uint SegmentMagic = 0x314C4157;
}