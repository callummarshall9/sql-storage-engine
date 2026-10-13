// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace sql_storage_engine.Dependencies.PageFiles;
internal sealed class PageFileDependency : IAsyncDisposable
{
    private readonly SafeFileHandle handle;
    public PageFileDependency(string path, bool readOnly, bool createNew)
    {
        handle = File.OpenHandle(path: path, mode: createNew ? FileMode.CreateNew : FileMode.Open, access: readOnly ? FileAccess.Read : FileAccess.ReadWrite, share: readOnly ? FileShare.ReadWrite : FileShare.Read, options: FileOptions.Asynchronous | FileOptions.RandomAccess);
    }

    public async ValueTask ReadFileAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(condition: handle.IsClosed, instance: this);
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

    public ValueTask WriteFileAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(condition: handle.IsClosed, instance: this);
        return RandomAccess.WriteAsync(handle: handle, buffer: source, fileOffset: offset, cancellationToken: cancellationToken);
    }

    public ValueTask FlushFileAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(condition: handle.IsClosed, instance: this);
        cancellationToken.ThrowIfCancellationRequested();
        RandomAccess.FlushToDisk(handle: handle);
        return ValueTask.CompletedTask;
    }

    public void RestoreFileLength(long length)
    {
        ObjectDisposedException.ThrowIf(condition: handle.IsClosed, instance: this);
        RandomAccess.SetLength(handle: handle, length: length);
    }

    public ValueTask DisposeAsync()
    {
        handle.Dispose();
        return ValueTask.CompletedTask;
    }
}