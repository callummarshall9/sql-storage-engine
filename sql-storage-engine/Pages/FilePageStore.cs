using Microsoft.Win32.SafeHandles;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Pages;

/// <summary>Performs complete fixed-size page I/O without shared file-position state.</summary>
public sealed class FilePageStore : IPageStore
{
    private readonly string _path;
    private readonly SafeFileHandle _handle;
    private bool _disposed;

    private FilePageStore(string path, int pageSize, SafeFileHandle handle)
    {
        _path = path;
        PageSize = pageSize;
        _handle = handle;
    }

    public int PageSize { get; }

    public static FilePageStore OpenExisting(string path, int pageSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!PageConstants.IsSupportedSize(pageSize)) throw new ArgumentOutOfRangeException(nameof(pageSize));
        try
        {
            return new FilePageStore(path, pageSize,
                File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, FileOptions.Asynchronous | FileOptions.RandomAccess));
        }
        catch (IOException exception)
        {
            throw new StorageResourceException($"Failed to open database '{path}'.", exception);
        }
    }

    internal static FilePageStore CreateNew(string path, int pageSize)
    {
        try
        {
            return new FilePageStore(path, pageSize,
                File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, FileOptions.Asynchronous | FileOptions.RandomAccess));
        }
        catch (IOException exception)
        {
            throw new StorageResourceException($"Failed to create database '{path}'.", exception);
        }
    }

    public async ValueTask ReadAsync(PageId pageId, Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed(); ValidateBuffer(destination.Length, nameof(destination));
        var offset = PageConstants.GetPageOffset(pageId, PageSize);
        var total = 0;
        try
        {
            while (total < PageSize)
            {
                var read = await RandomAccess.ReadAsync(_handle, destination[total..], checked(offset + total), cancellationToken).ConfigureAwait(false);
                if (read == 0) throw new StorageFormatException($"Short read for {pageId}: received {total} of {PageSize} bytes.");
                total += read;
            }
        }
        catch (IOException exception)
        {
            throw new StorageResourceException($"Failed to read {pageId} from database '{_path}'.", exception);
        }
    }

    public async ValueTask WriteAsync(PageId pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed(); ValidateBuffer(source.Length, nameof(source));
        var offset = PageConstants.GetPageOffset(pageId, PageSize);
        try
        {
            await RandomAccess.WriteAsync(_handle, source, offset, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new StorageResourceException($"Failed to write {pageId} to database '{_path}'.", exception);
        }
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed(); cancellationToken.ThrowIfCancellationRequested();
        try { RandomAccess.FlushToDisk(_handle); }
        catch (IOException exception) { throw new StorageResourceException($"Failed to flush database '{_path}'.", exception); }
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed) { _disposed = true; _handle.Dispose(); }
        return ValueTask.CompletedTask;
    }

    private void ValidateBuffer(int length, string name)
    {
        if (length != PageSize) throw new ArgumentException($"Buffer must be exactly {PageSize} bytes.", name);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
