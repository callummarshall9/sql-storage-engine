// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Strategies;
internal sealed record StorageObjectSegment
{
    public int Index { get; init; }
    public int Length { get; init; }
    public string? Key { get; init; }
    public byte[]? Checksum { get; init; }
}