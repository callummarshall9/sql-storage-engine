using System.Diagnostics;
using System.Runtime.CompilerServices;
using SqlExecutionEngine.Storage.Abstractions;

namespace SqlStorageEngine.Core;

internal sealed class PageBackendSnapshot : IBackendSnapshot
{
    private readonly PageBackendStore owner;
    private readonly PageBackendState state;
    private readonly TimeSpan lifetime;
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly object gate = new();
    private bool disposed;

    internal PageBackendSnapshot(PageBackendStore owner, PageBackendState state, StoreGeneration generation, TimeSpan lifetime)
    {
        this.owner = owner; this.state = state; this.lifetime = lifetime;
        Store = state.Id; Generation = generation;
    }

    public StoreId Store { get; }
    public StoreGeneration Generation { get; }

    public ValueTask<ByteString?> ReadAsync(CollectionId collection, ByteString key, CancellationToken cancellationToken = default)
    {
        using var operation = owner.EnterSnapshotRead();
        lock (gate)
        {
            Check(collection, cancellationToken);
            ArgumentNullException.ThrowIfNull(key);
            if (key.Length is < 1 or > 256) throw new ArgumentException("Invalid key.", nameof(key));
            return ValueTask.FromResult(state.Records.GetValueOrDefault(new(collection, key)));
        }
    }

    public async IAsyncEnumerable<BackendEntry> ScanAsync(CollectionId collection, KeyRange range,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        KeyValuePair<PageRecordKey, ByteString>[] rows;
        using (var operation = owner.EnterSnapshotRead())
        {
            lock (gate)
            {
                Check(collection, cancellationToken); ArgumentNullException.ThrowIfNull(range);
                rows = state.Records.Where(p => p.Key.Collection == collection && range.Contains(p.Key.Key))
                    .OrderBy(p => p.Key.Key).ToArray();
            }
        }
        foreach (var row in rows)
        {
            using (var operation = owner.EnterSnapshotRead())
                lock (gate) Check(collection, cancellationToken);
            yield return new(row.Key.Key, row.Value);
        }
        await Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        lock (gate) disposed = true;
        owner.ReleaseSnapshot(this);
        return ValueTask.CompletedTask;
    }

    private void Check(CollectionId collection, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (collection.Value == Guid.Empty) throw new ArgumentException("Invalid collection.", nameof(collection));
        if (Stopwatch.GetElapsedTime(started) >= lifetime) throw new BackendResourceException();
        token.ThrowIfCancellationRequested();
    }
}
