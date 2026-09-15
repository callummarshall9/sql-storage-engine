namespace sql_storage_engine.Transactions;

internal sealed class StorageSavepointStore(StorageEngine owner, StorageTransactionIdentity identity, long maximumBytes)
{
    private readonly List<StorageSavepointEntry> _entries = [];
    private long _bytes;

    internal static void ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length is 0 or > StorageSavepoint.MaximumNameLength)
            throw new ArgumentException("Savepoint names must contain 1 to 32 UTF-16 code units.", nameof(name));
    }

    internal async ValueTask<StorageSavepoint> CreateAsync(string name, CancellationToken token)
    {
        ValidateName(name);
        token.ThrowIfCancellationRequested();
        if (_entries.Count == StorageSavepoint.MaximumCount) throw new InvalidOperationException("Savepoint count quota exceeded.");
        await owner.FlushTransactionAsync(token).ConfigureAwait(false);
        var bytes = checked(new FileInfo(owner.TransactionPath).Length + 64);
        if (bytes > maximumBytes - _bytes) throw new InvalidOperationException("Savepoint image quota exceeded.");
        owner.TransactionObserver?.Invoke("BeforeSavepointCreate");
        var journal = await CreateImageAsync(token).ConfigureAwait(false);
        try
        {
            owner.TransactionObserver?.Invoke("AfterSavepointCreate");
            token.ThrowIfCancellationRequested();
            var point = new StorageSavepoint(identity, Guid.NewGuid(), name);
            _entries.Add(new(point, journal, bytes));
            _bytes += bytes;
            return point;
        }
        catch
        {
            DeleteImage(journal);
            throw;
        }
    }

    private async ValueTask<StatementJournal> CreateImageAsync(CancellationToken token)
    {
        var path = StorageSavepointFiles.NewPath(owner.TransactionPath);
        try { return await StatementJournal.CreateAsync(owner.TransactionPath, token, path).ConfigureAwait(false); }
        catch (Exception error)
        {
            try { TransactionDirectory.Delete(path); }
            catch (Exception cleanup) { throw new AggregateException("Savepoint image cleanup failed.", error, cleanup); }
            throw;
        }
    }

    private static void DeleteImage(StatementJournal journal)
    {
        try { journal.Delete(); }
        catch (Exception error) { throw new AggregateException("Savepoint image cleanup failed; root rollback is required.", error); }
    }

    internal StorageSavepoint Find(string name)
    {
        ValidateName(name);
        return _entries.LastOrDefault(entry => StringComparer.Ordinal.Equals(entry.Token.Name, name))?.Token
            ?? throw new ArgumentException("Unknown savepoint name.", nameof(name));
    }

    internal async ValueTask RollbackAsync(StorageSavepoint point, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(point);
        var index = _entries.FindIndex(entry => entry.Token == point);
        if (point.Transaction != identity || index < 0) throw new ArgumentException("Unknown, foreign or invalidated savepoint.", nameof(point));
        token.ThrowIfCancellationRequested();
        await owner.FlushTransactionAsync(token).ConfigureAwait(false);
        var guard = await CreateImageAsync(token).ConfigureAwait(false);
        try
        {
            owner.TransactionObserver?.Invoke("BeforeSavepointRestore");
            token.ThrowIfCancellationRequested();
            // Once restoration starts, caller cancellation cannot interrupt the database image rewrite.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await owner.RestoreTransactionAsync(_entries[index].Journal, cleanup.Token).ConfigureAwait(false);
                owner.TransactionObserver?.Invoke("AfterSavepointRestore");
            }
            catch (Exception restoreError)
            {
                try
                {
                    using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    owner.TransactionObserver?.Invoke("BeforeSavepointRecovery");
                    await owner.RestoreTransactionAsync(guard, recovery.Token).ConfigureAwait(false);
                }
                catch (Exception recoveryError)
                {
                    throw new AggregateException("Savepoint recovery failed; root rollback is required.", restoreError, recoveryError);
                }
                throw new IOException("Savepoint restoration failed; the pre-operation image was recovered.");
            }
            var invalidated = _entries.Skip(index + 1).ToArray();
            _entries.RemoveRange(index + 1, _entries.Count - index - 1);
            foreach (var entry in invalidated) { _bytes -= entry.Bytes; DeleteImage(entry.Journal); }
        }
        finally { DeleteImage(guard); }
    }
}
