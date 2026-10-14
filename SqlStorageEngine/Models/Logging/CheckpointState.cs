// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Logging;
public sealed record CheckpointState
{
    public LogSequenceNumber SafeRecoveryLsn { get; init; }
    public IReadOnlyDictionary<TransactionId, LogSequenceNumber> ActiveTransactions { get => _activeTransactions!; init => _activeTransactions = value; }
    public IReadOnlyDictionary<PageId, LogSequenceNumber> DirtyPages { get => _dirtyPages!; init => _dirtyPages = value; }

    private IReadOnlyDictionary<TransactionId, LogSequenceNumber>? _activeTransactions;
    private IReadOnlyDictionary<PageId, LogSequenceNumber>? _dirtyPages;
}