// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.ScopedStreams;
internal sealed class AsyncStreamResourceDependency<T> : IAsyncEnumerable<T>, IAsyncEnumerator<T>
{
    private readonly ConcurrentDictionary<Guid, IAsyncDisposable> sources = new();
    private readonly object sync = new();
    private readonly HashSet<IAsyncDisposable> readers = new();
    private readonly SemaphoreSlim operation = new(initialCount: 1, maxCount: 1);
    private readonly IAsyncEnumerable<T>? source;
    private readonly IAsyncEnumerator<T>? inner;
    private readonly Action? validateOwner;
    private readonly Action? onClosed;
    private bool disposed;
    private Task? disposal;
    public AsyncStreamResourceDependency()
    {
    }

    private AsyncStreamResourceDependency(IAsyncEnumerable<T> source, Action onClosed)
    {
        this.source = source;
        this.onClosed = onClosed;
    }

    private AsyncStreamResourceDependency(IAsyncEnumerator<T> inner, Action validateOwner, Action onClosed)
    {
        this.inner = inner;
        this.validateOwner = validateOwner;
        this.onClosed = onClosed;
    }

    public T Current => inner!.Current;

    public (Guid LeaseId, IAsyncEnumerable<T> Source, IAsyncDisposable Resource) Open(IAsyncEnumerable<T> source)
    {
        var id = Guid.NewGuid();
        var resource = new AsyncStreamResourceDependency<T>(source: source, onClosed: () => sources.TryRemove(key: id, value: out _));
        sources[id] = resource;
        return (id, resource, resource);
    }

    IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        lock (sync)
        {
            ValidateActive();
            IAsyncDisposable? reader = null;
            reader = new AsyncStreamResourceDependency<T>(inner: source!.GetAsyncEnumerator(cancellationToken: cancellationToken), validateOwner: ValidateActive, onClosed: () => RemoveReader(reader: reader!));
            readers.Add(item: reader);
            return (IAsyncEnumerator<T>)reader;
        }
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        if (!operation.Wait(millisecondsTimeout: 0))
        {
            throw new InvalidOperationException(message: "A stream operation is already active.");
        }

        try
        {
            ValidateActive();
            validateOwner!();

            return await inner!.MoveNextAsync()
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            operation.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (sync)
        {
            if (inner is not null && disposal?.IsCompleted == true)
            {
                return ValueTask.CompletedTask;
            }

            if (disposal is not null)
            {
                return new ValueTask(task: disposal);
            }

            if (inner is null)
            {
                disposed = true;
                disposal = DrainAsync(resources: source is null ? sources.Values.ToArray() : readers.ToArray());
            }
            else
            {
                disposal = CloseReaderAsync();
            }

            return new ValueTask(task: disposal);
        }
    }

    private async Task CloseReaderAsync()
    {
        await operation.WaitAsync()
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            disposed = true;

            await inner!.DisposeAsync()
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            operation.Release();
            onClosed!();
        }
    }

    private async Task DrainAsync(IAsyncDisposable[] resources)
    {
        List<Exception>? failures = null;

        try
        {
            foreach (var resource in resources)
            {
                try
                {
                    await resource.DisposeAsync()
            .ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (Exception exception)
                {
                    failures ??= new List<Exception>();
                    failures.Add(item: exception);
                }
            }

            if (failures is not null)
            {
                throw new AggregateException(innerExceptions: failures);
            }
        }
        finally
        {
            onClosed?.Invoke();
        }
    }

    private void RemoveReader(IAsyncDisposable reader)
    {
        lock (sync)
        {
            readers.Remove(item: reader);
        }
    }

    private void ValidateActive()
    {
        lock (sync)
        {
            if (disposed)
            {
                throw new InvalidOperationException(message: "The statement scope has completed.");
            }
        }
    }
}