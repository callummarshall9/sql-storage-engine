// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;

namespace sql_storage_engine.S3.Models.S3Objects;
internal sealed record S3ObjectContext
{
    public string? Key { get; init; }
    public int MaximumBytes { get; init; }
    public Stream? Stream { get; init; }
    public byte[]? Bytes { get; init; }
    public string? ExpectedVersion { get; init; }
    public string? Version { get; set; }
    public (byte[] Bytes, string Version)? Metadata { get; set; }
}