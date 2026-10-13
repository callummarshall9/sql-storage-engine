// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace sql_storage_engine.Dependencies.PageFileSessions;
internal sealed class PageFileSessionDependency : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, (SafeFileHandle Handle, SemaphoreSlim Gate, StrongBox<int> Users, StrongBox<bool> Closed)> handles = new();
    private readonly object sync = new();
    private bool disposed;
    public Guid OpenFile(string path, bool readOnly, bool createNew)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(condition: disposed, instance: this);
            var handle = File.OpenHandle(path: path, mode: createNew ? FileMode.CreateNew : FileMode.Open, access: readOnly ? FileAccess.Read : FileAccess.ReadWrite, share: readOnly ? FileShare.ReadWrite : FileShare.Read, options: FileOptions.Asynchronous | FileOptions.RandomAccess);
            var id = Guid.NewGuid();

            if (!handles.TryAdd(key: id, value: (handle, new SemaphoreSlim(initialCount: 1, maxCount: 1), new StrongBox<int>(0), new StrongBox<bool>(false))))
            {
                handle.Dispose();
                throw new IOException(message: "Cannot register a page-file session.");
            }

            return id;
        }
    }

    public async ValueTask ReadFileAsync(Guid sessionId, long offset, Memory<byte> destination, CancellationToken cancellationToken)
    {
        var handle = RequireHandle(sessionId: sessionId);
        var total = 0;

        while (total < destination.Length)
        {
            var read = await RandomAccess.ReadAsync(handle: handle, buffer: destination[total..], fileOffset: checked(offset + total), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (read == 0)
            {
                throw new EndOfStreamException(message: $"Short read at file offset {offset}: received {total} of {destination.Length} bytes.");
            }

            total += read;
        }
    }

    public ValueTask WriteFileAsync(Guid sessionId, long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        RandomAccess.WriteAsync(handle: RequireHandle(sessionId: sessionId), buffer: source, fileOffset: offset, cancellationToken: cancellationToken);

    public ValueTask FlushFileAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RandomAccess.FlushToDisk(handle: RequireHandle(sessionId: sessionId));
        return ValueTask.CompletedTask;
    }

    public void RestoreFileLength(Guid sessionId, long length) =>
        RandomAccess.SetLength(handle: RequireHandle(sessionId: sessionId), length: length);

    public ValueTask CloseFileAsync(Guid sessionId)
    {
        lock (sync)
        {
            if (handles.TryGetValue(key: sessionId, value: out var resource))
            {
                resource.Closed.Value = true;
                resource.Handle.Dispose();
                RetireClosedSession(sessionId: sessionId);
            }
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask EnterFileGateAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        SemaphoreSlim gate;

        lock (sync)
        {
            ObjectDisposedException.ThrowIf(condition: disposed, instance: this);

            if (!handles.TryGetValue(key: sessionId, value: out var resource) || resource.Closed.Value)
            {
                throw new ObjectDisposedException(objectName: "PageFileSession");
            }

            resource.Users.Value++;
            gate = resource.Gate;
        }

        try
        {
            await gate.WaitAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch
        {
            lock (sync)
            {
                if (handles.TryGetValue(key: sessionId, value: out var resource))
                {
                    resource.Users.Value--;
                    RetireClosedSession(sessionId: sessionId);
                }
            }

            throw;
        }
    }

    public void ExitFileGate(Guid sessionId)
    {
        lock (sync)
        {
            if (!handles.TryGetValue(key: sessionId, value: out var resource) || resource.Users.Value <= 0)
            {
                throw new ObjectDisposedException(objectName: "PageFileSession");
            }

            resource.Gate.Release();
            resource.Users.Value--;
            RetireClosedSession(sessionId: sessionId);
        }
    }

    private void RetireClosedSession(Guid sessionId)
    {
        if (handles.TryGetValue(key: sessionId, value: out var resource) && resource.Closed.Value && resource.Users.Value == 0 && handles.TryRemove(key: sessionId, value: out var retired))
        {
            retired.Gate.Dispose();
        }
    }

    private SafeFileHandle RequireHandle(Guid sessionId)
    {
        if (!handles.TryGetValue(key: sessionId, value: out var handle) || handle.Handle.IsClosed)
        {
            throw new ObjectDisposedException(objectName: "PageFileSession");
        }

        return handle.Handle;
    }

    public ValueTask DisposeAsync()
    {
        lock (sync)
        {
            disposed = true;

            foreach (var pair in handles)
            {
                pair.Value.Closed.Value = true;
                pair.Value.Handle.Dispose();
                RetireClosedSession(sessionId: pair.Key);
            }
        }

        return ValueTask.CompletedTask;
    }
}