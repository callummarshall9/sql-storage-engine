// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Transactions;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Logging;
internal sealed record WalAppendContext
{
    public WalRecord? Record { get; init; }
    public long Length { get; set; }
    public int SegmentBytes { get; set; }
    public ulong SegmentNumber { get; set; }
    public ulong DurableLsn { get; set; }
    public int MaximumSegmentBytes { get; init; }
    public bool IsOpen { get; set; }
    public TransactionId TransactionId { get; init; }
    public TransactionState TransactionState { get; set; }
    public LogSequenceNumber PreviousLsn { get; set; }
    public byte[]? Source { get; init; }
    public byte[]? EncodedRecord { get; init; }
    public WalReadResult? ReadResult { get; init; }
    public CheckpointState? Checkpoint { get; init; }
    public object? CheckpointProvider { get; init; }
    public Func<ulong, CancellationToken, ValueTask>? PublishCheckpoint { get; init; }
    public RecoveryPageContext? Recovery { get; init; }
}