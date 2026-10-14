// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;

namespace sql_storage_engine.AzureBlob.Models;
internal sealed class AzureBlobObjectContext
{
    public string? Key { get; set; }
    public Stream? Source { get; set; }
    public Stream? Destination { get; set; }
    public byte[]? Bytes { get; set; }
    public string? ExpectedVersion { get; set; }
    public string? Version { get; set; }
    public int MaximumBytes { get; set; }
    public (byte[] Bytes, string Version)? Metadata { get; set; }
}