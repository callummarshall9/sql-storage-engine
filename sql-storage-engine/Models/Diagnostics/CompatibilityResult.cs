// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.Models.Diagnostics;
public sealed record CompatibilityResult
{
    public DatabaseHeader Header { get => _header!; init => _header = value; }
    public IntegrityReport Integrity { get => _integrity!; init => _integrity = value; }
    public IReadOnlyList<string> Rows { get => _rows!; init => _rows = value; }
    public IReadOnlyList<WalRecord> WalRecords { get => _walRecords!; init => _walRecords = value; }

    private DatabaseHeader? _header;
    private IntegrityReport? _integrity;
    private IReadOnlyList<string>? _rows;
    private IReadOnlyList<WalRecord>? _walRecords;
}