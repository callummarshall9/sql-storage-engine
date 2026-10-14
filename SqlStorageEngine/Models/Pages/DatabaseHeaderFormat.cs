// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Pages;
public static class DatabaseHeaderFormat
{
    public const ushort CurrentVersion = 5;
    public const int PayloadOffset = PageHeaderFormat.EncodedLength;
    public const int EncodedMetadataLength = 228;
}