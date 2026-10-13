// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Backup;
public sealed record PointInTimeRestoreResult
{
    public string DatabasePath { get => _databasePath!; init => _databasePath = value; }

    private string? _databasePath;
    public ulong Timeline { get; init; }
    public LogSequenceNumber ReplayedThrough { get; init; }
    public IReadOnlyList<TransactionId> CommittedTransactions { get => _committedTransactions!; init => _committedTransactions = value; }

    private IReadOnlyList<TransactionId>? _committedTransactions;
}