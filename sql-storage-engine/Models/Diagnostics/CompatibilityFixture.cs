// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Diagnostics;
public sealed record CompatibilityFixture
{
    public string Name { get => _name!; init => _name = value; }

    private string? _name;
    public ushort FormatVersion { get; init; }
    public Guid DatabaseId { get; init; }
    public int PageSize { get; init; }
    public ulong Timeline { get; init; }
    public IReadOnlyList<string> ExpectedRows { get => _expectedRows!; init => _expectedRows = value; }
    public IReadOnlyList<CompatibilityWalRecord> WalRecords { get => _walRecords!; init => _walRecords = value; }

    private IReadOnlyList<string>? _expectedRows;
    private IReadOnlyList<CompatibilityWalRecord>? _walRecords;
}