using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;

namespace sql_storage_engine.Transactions;

/// <summary>Appends transaction changes and defines successful commit at the WAL flush boundary.</summary>
public sealed class DurableTransaction
{
    private readonly WriteAheadLog _wal;
    private LogSequenceNumber _previousLsn;

    public DurableTransaction(TransactionId id, WriteAheadLog wal)
    {
        if (id.Value == 0) throw new ArgumentOutOfRangeException(nameof(id));
        _wal = wal ?? throw new ArgumentNullException(nameof(wal));
        Id = id;
        State = TransactionState.Active;
    }

    public TransactionId Id { get; }
    public TransactionState State { get; private set; }

    public async ValueTask<LogSequenceNumber> AppendChangeAsync(ReadOnlyMemory<byte> physicalChange,
        CancellationToken cancellationToken = default)
    {
        EnsureActive();
        var record = await _wal.AppendAsync(Id, WalRecordType.PageChange, _previousLsn,
            physicalChange, cancellationToken).ConfigureAwait(false);
        _previousLsn = record.Lsn;
        return record.Lsn;
    }

    /// <summary>Returns only after commit is durable; response failure after that point is intentionally ambiguous to the caller.</summary>
    public async ValueTask CommitAsync(Func<ValueTask>? beforeResponse = null,
        CancellationToken cancellationToken = default)
    {
        EnsureActive();
        try
        {
            var commit = await _wal.AppendAsync(Id, WalRecordType.Commit, _previousLsn,
                ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
            await _wal.FlushThroughAsync(commit.Lsn, cancellationToken).ConfigureAwait(false);
            State = TransactionState.Committed;
            if (beforeResponse is not null) await beforeResponse().ConfigureAwait(false);
        }
        catch
        {
            if (State != TransactionState.Committed) State = TransactionState.Failed;
            throw;
        }
    }

    public void EnsureActive()
    {
        if (State != TransactionState.Active)
            throw new InvalidOperationException($"Durable transaction {Id} is {State}.");
    }
}
