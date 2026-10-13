// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Logging;
public sealed record WalReadResult
{
    public IReadOnlyList<WalRecord> Records { get => _records!; init => _records = value; }

    private IReadOnlyList<WalRecord>? _records;
    public bool HasIncompleteTail { get; init; }
    public int ValidLength { get; init; }
}