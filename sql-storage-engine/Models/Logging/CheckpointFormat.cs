// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Logging;
public static class CheckpointFormat
{
    public const ushort Version = 1;
    public const int HeaderLength = 24;
    public const int MaximumEntries = 65_535;
}