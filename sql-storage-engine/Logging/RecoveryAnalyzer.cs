using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Logging;

public sealed record RecoveryAnalysis(IReadOnlyDictionary<TransactionId, TransactionState> Transactions,
    IReadOnlyDictionary<PageId, LogSequenceNumber> DirtyPages, IReadOnlyList<WalRecord> Records,
    int ValidLength, bool TruncatedTail);

/// <summary>Validates WAL identity and builds deterministic startup transaction/page recovery state.</summary>
public static class RecoveryAnalyzer
{
    public static RecoveryAnalysis ResumeFromCheckpoint(CheckpointState checkpoint,
        IReadOnlyList<WalRecord> recordsAfterCheckpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(recordsAfterCheckpoint);
        Dictionary<TransactionId, TransactionState> transactions = checkpoint.ActiveTransactions.Keys
            .ToDictionary(id => id, _ => TransactionState.Active);
        Dictionary<PageId, LogSequenceNumber> dirty = new(checkpoint.DirtyPages);
        ApplyRecords(recordsAfterCheckpoint, transactions, dirty);
        return new RecoveryAnalysis(transactions, dirty, recordsAfterCheckpoint, 0, false);
    }

    public static RecoveryAnalysis Analyze(WalSegmentHeader segment, DatabaseId expectedDatabaseId,
        ulong expectedTimeline, ReadOnlySpan<byte> recordBytes)
    {
        if (segment.DatabaseId != expectedDatabaseId)
            throw new StorageCorruptionException("WAL belongs to a different database.");
        if (segment.Timeline != expectedTimeline)
            throw new StorageCorruptionException("WAL belongs to a different timeline.");
        WalReadResult read;
        try { read = WalFormat.ReadRecords(recordBytes); }
        catch (StorageFormatException exception) { throw new StorageCorruptionException("Malformed record within WAL.", exception); }
        Dictionary<TransactionId, TransactionState> transactions = [];
        Dictionary<PageId, LogSequenceNumber> dirty = [];
        ApplyRecords(read.Records, transactions, dirty);
        return new RecoveryAnalysis(transactions, dirty, read.Records, read.ValidLength, read.HasIncompleteTail);
    }

    private static void ApplyRecords(IEnumerable<WalRecord> records,
        Dictionary<TransactionId, TransactionState> transactions,
        Dictionary<PageId, LogSequenceNumber> dirty)
    {
        foreach (var record in records)
        {
            if (record.Type == WalRecordType.Checkpoint) continue;
            transactions[record.TransactionId] = record.Type switch
            {
                WalRecordType.Commit => TransactionState.Committed,
                WalRecordType.Rollback => TransactionState.RolledBack,
                _ => transactions.GetValueOrDefault(record.TransactionId, TransactionState.Active)
            };
            if (record.Type == WalRecordType.PageChange)
            {
                if (record.Payload.Length < sizeof(ulong)) throw new StorageCorruptionException("Page-change WAL payload is truncated.");
                var pageId = new PageId(BinaryPrimitives.ReadUInt64LittleEndian(record.Payload.Span));
                dirty.TryAdd(pageId, record.Lsn);
            }
        }
    }
}
