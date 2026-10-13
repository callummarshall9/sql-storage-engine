// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Security;
internal sealed record SecurityEventFile
{
    public string? Path { get; set; }
    public string? Destination { get; set; }
    public ReadOnlyMemory<byte> Bytes { get; set; }
    public StorageAuditRecord[]? Records { get; set; }
    public Guid DatabaseId { get; set; }
}