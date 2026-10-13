// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.BackupDurability;
internal sealed class BackupDurabilityDependency(Func<ulong>? current) : IDisposable
{
    private Func<ulong>? currentSource = current;
    private bool disposed;
    public ulong ReadCurrent()
    {
        ObjectDisposedException.ThrowIf(condition: disposed, instance: this);
        return currentSource?.Invoke() ?? throw new InvalidOperationException(message: "Online backup requires a durable WAL position provider.");
    }

    public ValueTask InvokeAsync(Func<CancellationToken, ValueTask>? callback, CancellationToken cancellationToken) =>
        callback is null ? ValueTask.CompletedTask : callback(arg: cancellationToken);

    public void Dispose()
    {
        currentSource = null;
        disposed = true;
    }
}