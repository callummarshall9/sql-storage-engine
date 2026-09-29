using System.Diagnostics;
using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

/// <summary>Host mechanisms only. The shared suite owns operations, process killing and all outcome assertions.</summary>
public abstract class BackendTestDriver
{
    public abstract IBackendFactory CreateFactory(BackendTestSettings settings,
        Func<BackendTestCheckpoint, ValueTask>? checkpoint = null);

    /// <summary>Create a child that writes key [1], value [42] to collection with operation, then emits
    /// CHECKPOINT and waits indefinitely at the requested boundary. Do not start it here.</summary>
    public abstract ProcessStartInfo CreateCrashProcess(ByteString location, BackendTestCheckpoint checkpoint,
        OperationId operation, CollectionId collection);

    /// <summary>Cause a real filesystem I/O failure at publication. Disposal restores access for recovery.</summary>
    public abstract ValueTask<IAsyncDisposable> InjectIoFailureAsync(ByteString location, CancellationToken token);
}
