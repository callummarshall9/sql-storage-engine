// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Backup;
internal static class BackupMetadataFormat
{
    public const int MaximumBytes = 1024 * 1024;
    public const int MaximumStringBytes = 65536;
    public const int MaximumFiles = 1024;
    public const int HeaderBytes = 16;
    public const int ChecksumBytes = 32;
    public const ulong Magic = 0x3030504B42455353UL;
    public const ushort Version = 1;
}