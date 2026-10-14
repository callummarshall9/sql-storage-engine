// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Transactions;
internal static class TransactionMetadataFormat
{
    public const ushort Version = 1;
    public const int HeaderLength = 16;
    public const int PayloadLength = 32;
    public const int ChecksumLength = 32;
    public const int EncodedLength = HeaderLength + PayloadLength + ChecksumLength;
}