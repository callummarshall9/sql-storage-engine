// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Diagnostics;
/// <summary>Capacity and traversal settings and their published engine hard maxima.</summary>
public sealed record StorageLimits
{
    public const int MaximumBufferFrames = 1_000_000;
    public const int MaximumRowBytes = 16 * 1024 * 1024;
    public const int MaximumKeyBytes = ushort.MaxValue;
    public const long MaximumValueBytes = int.MaxValue;
    public const int MaximumOverflowPages = sql_storage_engine.Models.Overflow.OverflowReferenceFormat.MaximumChainLength;
    public const int MaximumTransactionSeconds = 86400;
    public const long MaximumUndoBytes = 256L * 1024 * 1024;
    public const int MaximumPinsPerTransaction = 65536;
    public const int MaximumScanPages = 1_000_000;
    public const int MaximumConcurrentTransactions = 65536;
    public int BufferFrames { get; init; }
    public int RowBytes { get; init; }
    public int KeyBytes { get; init; }
    public long ValueBytes { get; init; }
    public int OverflowPages { get; init; }
    public TimeSpan TransactionDuration { get; init; }
    public long UndoBytes { get; init; }
    public int PinsPerTransaction { get; init; }
    public int ScanPages { get; init; }
    public int ConcurrentTransactions { get; init; }
}