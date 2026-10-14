// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Configurations;
internal sealed class BufferFrameConfiguration
{
    public ulong PageId { get; set; }
    public byte[]? Bytes { get; set; }
}