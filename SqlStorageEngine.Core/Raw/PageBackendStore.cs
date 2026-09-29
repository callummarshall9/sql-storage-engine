using SqlExecutionEngine.Storage.Abstractions;
namespace SqlStorageEngine.Core;

internal sealed class PageBackendStore : IBackendStore
{
    private readonly AtomicPageImage image;
    private readonly SemaphoreSlim operations = new(1);
    private readonly object lifetime = new();
    private readonly HashSet<PageBackendSnapshot> snapshots = [];
    private PageBackendState state;
    private StoreGeneration generation = NewGeneration();
    private volatile bool disposed;
    private volatile bool quarantined;
    private readonly Func<string, ValueTask>? checkpoint;
    private Task? disposal;

    private PageBackendStore(AtomicPageImage image, PageBackendState state,
        PageBackendOptions options, Func<string, ValueTask>? checkpoint)
    {
        this.image = image; this.state = state;
        Descriptor = options.Descriptor; this.checkpoint = checkpoint;
    }

    public BackendDescriptor Descriptor { get; }
    public StoreId Id => state.Id;

    internal static async ValueTask<PageBackendStore> OpenAsync(string path, BackendOpenMode mode,
        PageBackendOptions options, Func<string, ValueTask>? checkpoint, CancellationToken token,
        Action<string>? directoryFlushed = null)
    {
        var image = AtomicPageImage.Acquire(path, mode == BackendOpenMode.CreateNew, directoryFlushed);
        try
        {
            var state = mode == BackendOpenMode.CreateNew
                ? new PageBackendState(new(Guid.NewGuid()))
                : PageStateCodec.Decode(await image.ReadAsync(token).ConfigureAwait(false));
            if (state.RecordBytes > options.Limits.StoreBytes || state.Receipts.Count > options.Limits.Receipts)
                throw new BackendResourceException();
            var result = new PageBackendStore(image, state, options, checkpoint);
            var pending = state.Receipts.Where(p => p.Value.Outcome.Status == CommitStatus.Unknown).ToArray();
            foreach (var pair in pending)
                state.Receipts[pair.Key] = pair.Value with { Outcome = new(state.Id, pair.Key, CommitStatus.Conflict) };
            // Reconcile durable admission before admitting generation-dependent writes.
            if (mode == BackendOpenMode.CreateNew || pending.Length != 0)
                await result.PersistAsync(state, token).ConfigureAwait(false);
            return result;
        }
        catch { await image.DisposeAsync().ConfigureAwait(false); throw; }
    }

    public async ValueTask<IBackendSnapshot> OpenSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (lifetime)
            {
                CheckAvailable();
                if (snapshots.Count >= Descriptor.Limits.Snapshots) throw new BackendResourceException();
                var snapshot = new PageBackendSnapshot(this, state, generation, Descriptor.Limits.SnapshotLifetime);
                snapshots.Add(snapshot);
                return snapshot;
            }
        }
        finally { operations.Release(); }
    }

    public async ValueTask<CommitReceipt> CommitAsync(BackendBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        CheckAvailable();
        if (batch.Store != Id) throw new ArgumentException("Foreign store identity.", nameof(batch));
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var fingerprint = batch.Fingerprint();
            if (state.Receipts.TryGetValue(batch.Operation, out var previous))
            {
                if (!previous.Fingerprint.Equals(fingerprint)) throw new ArgumentException("Operation identity was reused.", nameof(batch));
                return previous.Outcome;
            }
            if (state.Receipts.Count >= Descriptor.Limits.Receipts) throw new BackendResourceException();
            var next = state.Copy();
            var matches = batch.ExpectedGeneration == generation;
            var nextGeneration = matches ? NewGeneration() : null;
            if (matches)
            {
                foreach (var mutation in batch.Mutations)
                {
                    var key = new PageRecordKey(mutation.Collection, mutation.Key);
                    if (mutation.Value is null) next.Records.Remove(key);
                    else next.Records[key] = mutation.Value;
                }
                if (next.RecordBytes > Descriptor.Limits.StoreBytes) throw new BackendResourceException();
            }
            var receipt = new CommitReceipt(Id, batch.Operation, matches ? CommitStatus.Committed : CommitStatus.Conflict, nextGeneration);
            next.Receipts.Add(batch.Operation, new(fingerprint, receipt));
            var admission = state.Copy();
            var unknown = new CommitReceipt(Id, batch.Operation, CommitStatus.Unknown);
            admission.Receipts.Add(batch.Operation, new(fingerprint, unknown));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Once durable admission starts, cancellation may not assert rollback or forget the operation ID.
                await PersistAsync(admission, CancellationToken.None, "admission").ConfigureAwait(false);
                state = admission;
                await PersistAsync(next, CancellationToken.None, "publication").ConfigureAwait(false);
                state = next;
                if (nextGeneration is not null) generation = nextGeneration;
                return receipt;
            }
            catch
            {
                quarantined = true;
                return unknown;
            }
        }
        finally { operations.Release(); }
    }

    public async ValueTask<CommitReceipt> ResolveAsync(OperationId operation, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (operation.Value == Guid.Empty) throw new ArgumentException("Invalid operation.", nameof(operation));
        cancellationToken.ThrowIfCancellationRequested();
        if (quarantined) return new(Id, operation, CommitStatus.Unknown);
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try { return state.Receipts.TryGetValue(operation, out var receipt) ? receipt.Outcome : new(Id, operation, CommitStatus.NotSubmitted); }
        finally { operations.Release(); }
    }

    private async ValueTask PersistAsync(PageBackendState value, CancellationToken token, string? phase = null)
    {
        await image.PublishAsync(PageStateCodec.Encode(value), token, phase, checkpoint).ConfigureAwait(false);
    }

    private async ValueTask EnterAsync(CancellationToken token)
    {
        CheckAvailable(); token.ThrowIfCancellationRequested();
        if (!await operations.WaitAsync(0, token).ConfigureAwait(false)) throw new BackendResourceException();
        try { CheckAvailable(); }
        catch { operations.Release(); throw; }
    }

    private void CheckAvailable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (quarantined) throw new InvalidOperationException("Page store requires recovery before further work.");
    }

    internal PageOperationLease EnterSnapshotRead()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!operations.Wait(0)) throw new BackendResourceException();
        if (disposed) { operations.Release(); throw new ObjectDisposedException(nameof(PageBackendStore)); }
        return new(operations);
    }

    internal void ReleaseSnapshot(PageBackendSnapshot snapshot)
    {
        lock (lifetime) snapshots.Remove(snapshot);
    }

    public ValueTask DisposeAsync()
    {
        lock (lifetime)
        {
            if (disposal is not null) return new(disposal);
            disposed = true;
            foreach (var snapshot in snapshots.ToArray()) snapshot.DisposeAsync().GetAwaiter().GetResult();
            disposal = DisposeCoreAsync();
            return new(disposal);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await operations.WaitAsync().ConfigureAwait(false);
        try { await image.DisposeAsync().ConfigureAwait(false); }
        finally { operations.Release(); }
    }

    private static StoreGeneration NewGeneration() => new(new(Guid.NewGuid().ToByteArray()));
}
