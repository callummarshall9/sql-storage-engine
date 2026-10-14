// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Pages;
public static class PageHeaderFormat
{
    public const ushort CurrentVersion = 1;
    public const int EncodedLength = 32;
    public const int ChecksumOffset = 28;
}