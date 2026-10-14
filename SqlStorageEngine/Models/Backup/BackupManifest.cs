// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Backup;
/// <summary>Describes the identity, format, recovery interval, and independently verifiable files in a backup.</summary>
public sealed record BackupManifest
{
    public DatabaseId DatabaseId { get; init; }
    public ushort FormatVersion { get; init; }
    public int PageSize { get; init; }
    public LogSequenceNumber StartLsn { get; init; }
    public LogSequenceNumber EndLsn { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public string EngineVersion { get => _engineVersion!; init => _engineVersion = value; }
    public IReadOnlyList<BackupFile> Files { get => _files!; init => _files = value; }

    private string? _engineVersion;
    private IReadOnlyList<BackupFile>? _files;
}