using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

public sealed record CheckpointState(LogSequenceNumber SafeRecoveryLsn,
    IReadOnlyDictionary<TransactionId, LogSequenceNumber> ActiveTransactions,
    IReadOnlyDictionary<PageId, LogSequenceNumber> DirtyPages);

public static class CheckpointCodec
{
    public const ushort Version = 1;
    public static byte[] Write(CheckpointState state)
    {
        var bytes = new byte[checked(24 + (state.ActiveTransactions.Count + state.DirtyPages.Count) * 16)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, Version);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8), state.SafeRecoveryLsn.Value);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), checked((uint)state.ActiveTransactions.Count));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), checked((uint)state.DirtyPages.Count));
        var offset = 24;
        foreach (var pair in state.ActiveTransactions.OrderBy(pair => pair.Key.Value))
        { BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), pair.Key.Value); BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset + 8), pair.Value.Value); offset += 16; }
        foreach (var pair in state.DirtyPages.OrderBy(pair => pair.Key.Value))
        { BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), pair.Key.Value); BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset + 8), pair.Value.Value); offset += 16; }
        return bytes;
    }

    public static CheckpointState Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < 24) throw new StorageFormatException("Checkpoint is truncated.");
        if (BinaryPrimitives.ReadUInt16LittleEndian(source) != Version) throw new StorageFormatException("Unsupported checkpoint version.");
        if (source.Slice(2, 6).IndexOfAnyExcept((byte)0) >= 0) throw new StorageFormatException("Reserved checkpoint bytes must be zero.");
        var active = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source[16..]));
        var dirty = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source[20..]));
        if (active > 65_535 || dirty > 65_535 || source.Length != 24 + checked((active + dirty) * 16))
            throw new StorageFormatException("Checkpoint counts or length are invalid.");
        Dictionary<TransactionId, LogSequenceNumber> transactions = [];
        Dictionary<PageId, LogSequenceNumber> pages = [];
        var offset = 24;
        for (var index = 0; index < active; index++, offset += 16)
            if (!transactions.TryAdd(new TransactionId(BinaryPrimitives.ReadUInt64LittleEndian(source[offset..])),
                    new LogSequenceNumber(BinaryPrimitives.ReadUInt64LittleEndian(source[(offset + 8)..]))))
                throw new StorageFormatException("Duplicate checkpoint transaction.");
        for (var index = 0; index < dirty; index++, offset += 16)
            if (!pages.TryAdd(new PageId(BinaryPrimitives.ReadUInt64LittleEndian(source[offset..])),
                    new LogSequenceNumber(BinaryPrimitives.ReadUInt64LittleEndian(source[(offset + 8)..]))))
                throw new StorageFormatException("Duplicate checkpoint page.");
        return new CheckpointState(new LogSequenceNumber(BinaryPrimitives.ReadUInt64LittleEndian(source[8..])), transactions, pages);
    }
}

public interface ICheckpointReference
{
    LogSequenceNumber? LatestCheckpointLsn { get; }
    ValueTask PublishAsync(LogSequenceNumber checkpointLsn, CancellationToken cancellationToken = default);
}

public sealed class MemoryCheckpointReference : ICheckpointReference
{
    public LogSequenceNumber? LatestCheckpointLsn { get; private set; }
    public bool FailPublish { get; set; }
    public ValueTask PublishAsync(LogSequenceNumber checkpointLsn, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); if (FailPublish) return ValueTask.FromException(new IOException("checkpoint publication")); LatestCheckpointLsn = checkpointLsn; return ValueTask.CompletedTask; }
}

/// <summary>Creates durable checkpoints and atomically publishes their discovery LSN.</summary>
public sealed class CheckpointManager(WriteAheadLog wal, ICheckpointReference reference)
{
    public async ValueTask<LogSequenceNumber> CreateAsync(CheckpointState state,
        CancellationToken cancellationToken = default)
    {
        var record = await wal.AppendAsync(new TransactionId(1), WalRecordType.Checkpoint, default,
            CheckpointCodec.Write(state), cancellationToken).ConfigureAwait(false);
        await wal.FlushThroughAsync(record.Lsn, cancellationToken).ConfigureAwait(false);
        await reference.PublishAsync(record.Lsn, cancellationToken).ConfigureAwait(false);
        return record.Lsn;
    }

    public static LogSequenceNumber GetRetentionLsn(CheckpointState state)
    {
        var result = state.SafeRecoveryLsn.Value;
        foreach (var lsn in state.ActiveTransactions.Values)
            if (result == 0 || lsn.Value < result) result = lsn.Value;
        return new LogSequenceNumber(result);
    }
}
