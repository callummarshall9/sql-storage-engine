namespace sql_storage_engine;

/// <summary>Coordinates root handles while letting scoped handles join the owner's journal.</summary>
internal sealed class GraphHandleScope(StorageEngine owner, long generation, Func<bool>? isActive)
    : IDisposable
{
    public void EnsureCurrent()
    {
        if (isActive is not null && !isActive())
            throw new InvalidOperationException("The statement scope has completed.");
        owner.EnsureGraphHandleCurrent(generation);
    }

    public async ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureCurrent();
        var lease = await owner.EnterStatementGateAsync(cancellationToken, reentrant: isActive is not null).ConfigureAwait(false);
        try
        {
            // A waiting root handle may have been invalidated by statement rollback.
            EnsureCurrent();
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public ValueTask PublishAsync(CancellationToken cancellationToken) =>
        isActive is null ? owner.FlushAndPublishAsync(cancellationToken) : ValueTask.CompletedTask;

    public void Dispose() { }
}
