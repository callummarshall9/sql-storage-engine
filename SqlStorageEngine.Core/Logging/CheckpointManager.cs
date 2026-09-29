using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

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
