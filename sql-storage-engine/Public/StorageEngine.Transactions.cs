using sql_storage_engine.Catalog;
using sql_storage_engine.Transactions;

namespace sql_storage_engine;

public sealed partial class StorageEngine
{
    internal IAsyncEnumerable<T> TrackStream<T>(IAsyncEnumerable<T> source, bool scoped) =>
        scoped && _accessGate.Current is { } scope ? new StorageScopedEnumerable<T>(source, scope) : source;

    internal Task? PendingTransactionCleanup { get; private set; }
    internal void RetainPendingTransaction(Task cleanup)
    {
        _quarantined = true;
        PendingTransactionCleanup = cleanup;
    }

    private StorageTransaction? _activeTransaction;
    private bool _quarantined;
    internal Action<string>? TransactionObserver { get; set; }
    internal string TransactionPath => _database.DatabasePath;

    public async ValueTask<IStorageTransaction> BeginTransactionAsync(StorageTransactionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Explicit transaction directory durability currently requires Linux.");
        ThrowIfDisposed();
        if (_accessGate.Current is not null) throw new InvalidOperationException("Nested transaction acquisition is not supported.");
        var lease = await _accessGate.EnterAsync(cancellationToken, reentrant: false).ConfigureAwait(false);
        StatementJournal? journal = null;
        var identity = new StorageTransactionIdentity(DatabaseId, Guid.NewGuid());
        try
        {
            using var scope = lease.Activate();
            ThrowIfDisposed();
            await FlushAndPublishAsync(cancellationToken).ConfigureAwait(false);
            await _database.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (new FileInfo(TransactionPath).Length > options.MaximumJournalBytes)
                throw new InvalidOperationException("Database exceeds the transaction journal quota.");
            journal = await StatementJournal.CreateAsync(TransactionPath, cancellationToken, StorageTransactionFiles.JournalPath(TransactionPath)).ConfigureAwait(false);
            await StorageTransactionFiles.WriteAsync(StorageTransactionFiles.ActivePath(TransactionPath), identity, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            _database.MaximumAllocatedBytes = options.MaximumJournalBytes;
            var transaction = new StorageTransaction(this, lease, journal, identity);
            _activeTransaction = transaction;
            transaction.StartDeadline(options.Timeout);
            return transaction;
        }
        catch
        {
            if (journal is not null)
            {
                try
                {
                    await journal.RestoreAsync(_database, CancellationToken.None, retain: true).ConfigureAwait(false);
                    await StorageTransactionFiles.WriteReceiptAsync(TransactionPath, new(identity, StorageTransactionState.Aborted), CancellationToken.None).ConfigureAwait(false);
                    journal.Delete();
                    TransactionDirectory.Delete(StorageTransactionFiles.ActivePath(TransactionPath));
                }
                catch
                {
                    _quarantined = true;
                    lease.Dispose();
                    throw new StorageTransactionBeginException(identity);
                }
            }
            lease.Dispose();
            throw;
        }
    }

    public async ValueTask<StorageTransactionReceipt> ResolveTransactionAsync(StorageTransactionIdentity identity, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateTransactionIdentity(identity);
        return await StorageTransactionFiles.ResolveAsync(TransactionPath, identity, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ReleaseTransactionReceiptAsync(StorageTransactionIdentity identity, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed(); ValidateTransactionIdentity(identity);
        using var lease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var scope = lease.Activate();
        ThrowIfDisposed();
        if ((await StorageTransactionFiles.ResolveAsync(TransactionPath, identity, cancellationToken).ConfigureAwait(false)).State == StorageTransactionState.Indeterminate)
            throw new InvalidOperationException("An unresolved transaction cannot release its receipt.");
        StorageTransactionFiles.ReleaseReceipt(TransactionPath, identity);
    }

    private void ValidateTransactionIdentity(StorageTransactionIdentity identity)
    {
        if (identity.Value == Guid.Empty || identity.DatabaseId != DatabaseId) throw new ArgumentException("Transaction identity belongs to another database.", nameof(identity));
    }

    internal async ValueTask RestoreTransactionAsync(StatementJournal journal, CancellationToken token)
    {
        await _bufferPool.DiscardAllAsync(token).ConfigureAwait(false);
        await journal.RestoreAsync(_database, token, retain: true).ConfigureAwait(false);
        _catalog = _database.Header.CatalogRootPageId is { } root
            ? await CatalogService.OpenAsync(root, _database, _database, _bufferPool, token).ConfigureAwait(false)
            : CatalogService.CreateEmpty(_database, _database, _bufferPool);
        Interlocked.Increment(ref _handleGeneration);
    }

    internal async ValueTask FlushTransactionAsync(CancellationToken token)
    {
        await FlushAndPublishAsync(token).ConfigureAwait(false);
        await _database.FlushAsync(token).ConfigureAwait(false);
    }

    internal void FinishTransaction(StorageTransaction transaction, bool quarantine)
    {
        _quarantined |= quarantine;
        if (ReferenceEquals(_activeTransaction, transaction)) _activeTransaction = null;
        _database.MaximumAllocatedBytes = null;
    }
}
