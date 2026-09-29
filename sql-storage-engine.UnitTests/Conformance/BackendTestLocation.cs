using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

/// <summary>Provider-specific location ownership; cleanup runs even when an oracle fails.</summary>
public sealed class BackendTestLocation(ByteString location, Func<ValueTask> cleanup) : IAsyncDisposable
{
    public ByteString Location { get; } = location ?? throw new ArgumentNullException(nameof(location));
    private int disposed;
    public ValueTask DisposeAsync() => Interlocked.Exchange(ref disposed, 1) == 0 ? cleanup() : ValueTask.CompletedTask;
}
