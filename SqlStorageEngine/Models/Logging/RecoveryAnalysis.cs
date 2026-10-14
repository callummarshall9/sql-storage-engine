// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Transactions;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Logging;
public sealed record RecoveryAnalysis
{
    public IReadOnlyDictionary<TransactionId, TransactionState> Transactions { get => _transactions!; init => _transactions = value; }
    public IReadOnlyDictionary<PageId, LogSequenceNumber> DirtyPages { get => _dirtyPages!; init => _dirtyPages = value; }
    public IReadOnlyList<WalRecord> Records { get => _records!; init => _records = value; }

    private IReadOnlyDictionary<TransactionId, TransactionState>? _transactions;
    private IReadOnlyDictionary<PageId, LogSequenceNumber>? _dirtyPages;
    private IReadOnlyList<WalRecord>? _records;
    public int ValidLength { get; init; }
    public bool TruncatedTail { get; init; }
    public CheckpointState? Checkpoint { get; init; }
    public WalSegmentHeader? Segment { get; init; }
}