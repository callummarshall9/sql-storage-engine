using sql_storage_engine.Transactions;

namespace sql_storage_engine;

internal sealed partial class StorageTransaction : IStorageTransaction
{
    private readonly StorageEngine _owner;
    private readonly StorageGateLease _lease;
    private readonly StatementJournal _journal;
    private readonly StorageSavepointStore _savepoints;
    private readonly CancellationTokenSource _deadline;
    private readonly SemaphoreSlim _operation = new(1, 1);
    private readonly CancellationTokenRegistration _expiry;
    private bool _disposed;
    private bool _released;
    private Task? _timeoutCleanup;
    public StorageTransactionIdentity Identity { get; }
    public StorageTransactionState State { get; private set; } = StorageTransactionState.Committable;

    internal StorageTransaction(StorageEngine owner, StorageGateLease lease, StatementJournal journal,
        StorageTransactionIdentity identity, long maximumSavepointBytes)
    {
        _owner = owner; _lease = lease; _journal = journal; Identity = identity;
        _savepoints = new StorageSavepointStore(owner, identity, maximumSavepointBytes);
        _deadline = new CancellationTokenSource();
        _expiry = _deadline.Token.UnsafeRegister(_ => _timeoutCleanup = ExpireAsync(), null);
    }

    internal void StartDeadline(TimeSpan timeout) => _deadline.CancelAfter(timeout);

    public async ValueTask ExecuteStatementAsync(Func<IStorageTransactionContext, CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        EnterOperation();
        try
        {
            if (State != StorageTransactionState.Committable) throw new InvalidOperationException("Transaction is not committable.");
            using var scope = _lease.Activate();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token, cancellationToken);
            Task? execution = null;
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                execution = _owner.ExecuteStatementAsync((statement, token) => operation((IStorageTransactionContext)statement, token), linked.Token).AsTask();
                await execution.WaitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (execution is { IsCompleted: false })
                {
                    try { await execution.WaitAsync(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false); }
                    catch (TimeoutException) when (!execution.IsCompleted)
                    {
                        State = StorageTransactionState.Indeterminate;
                        _owner.RetainPendingTransaction(FinishPendingCallbackAsync(execution));
                        throw new OperationCanceledException("Transaction callback has not stopped; storage remains quarantined.", linked.Token);
                    }
                    catch { /* The callback has stopped; root rollback resolves its effects. */ }
                }
                await AbortCoreAsync().ConfigureAwait(false);
                throw;
            }
            catch (AggregateException)
            {
                State = StorageTransactionState.Doomed;
                throw new InvalidOperationException("Transaction statement cleanup failed; only rollback is permitted.");
            }
        }
        finally { _operation.Release(); }
    }

    private async Task FinishPendingCallbackAsync(Task execution)
    {
        try { await execution.ConfigureAwait(false); }
        catch { /* Root rollback is required regardless of the callback outcome. */ }
        await _operation.WaitAsync().ConfigureAwait(false);
        try { await AbortCoreAsync().ConfigureAwait(false); }
        finally { _operation.Release(); }
    }

    private void EnterOperation()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_operation.Wait(0)) throw new InvalidOperationException("A transaction operation is already active.");
    }

    private async Task ExpireAsync()
    {
        await _operation.WaitAsync().ConfigureAwait(false);
        try
        {
            if (State is StorageTransactionState.Committable or StorageTransactionState.Doomed)
                await AbortCoreAsync().ConfigureAwait(false);
        }
        catch { MarkIndeterminate(); }
        finally { _operation.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (_owner.IsInStorageScope) throw new InvalidOperationException("Dispose the transaction after its callback returns.");
        _deadline.Cancel();
        await _operation.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            if (State is StorageTransactionState.Committable or StorageTransactionState.Doomed)
                await AbortCoreAsync().ConfigureAwait(false);
            _disposed = true;
            _expiry.Unregister();
        }
        finally { _operation.Release(); }
        if (_timeoutCleanup is not null) await _timeoutCleanup.ConfigureAwait(false);
        _deadline.Dispose();
        if (State == StorageTransactionState.Indeterminate) throw new InvalidOperationException("Transaction outcome is indeterminate; reopen storage to recover.");
    }
}
