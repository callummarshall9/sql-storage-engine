using System.Buffers.Binary;
using Microsoft.Win32.SafeHandles;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Pages;

/// <summary>An open database's page store and persistent free-page allocator.</summary>
public sealed class PageDatabase : IPageStore, IPageAllocator
{
    private const int FreeNextOffset = PageHeaderCodec.EncodedLength;
    private readonly FilePageStore _store;
    private readonly SemaphoreSlim _allocationLock = new(1, 1);
    private DatabaseHeader _header;
    private bool _disposed;
    private readonly DatabaseOpenMode _openMode;

    private PageDatabase(FilePageStore store, DatabaseHeader header, DatabaseOpenMode openMode)
    {
        _store = store;
        _header = header;
        _openMode = openMode;
    }

    public int PageSize => _store.PageSize;
    public DatabaseHeader Header => _header;

    /// <summary>Atomically publishes the root of the logical catalog in page zero.</summary>
    internal async ValueTask PublishCatalogRootAsync(PageId rootPageId,
        CancellationToken cancellationToken = default)
    {
        if (rootPageId.Value == 0) throw new ArgumentOutOfRangeException(nameof(rootPageId));
        await _allocationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_openMode == DatabaseOpenMode.ReadOnly)
                throw new InvalidOperationException("Read-only databases cannot publish a catalog.");
            _header = _header with { CatalogRootPageId = rootPageId };
            await PersistHeaderAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _allocationLock.Release(); }
    }

    public static async Task<PageDatabase> CreateAsync(string path, int pageSize = PageConstants.DefaultSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!PageConstants.IsSupportedSize(pageSize)) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath)) throw new IOException($"Database already exists: '{fullPath}'.");
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        var header = new DatabaseHeader(DatabaseId.New(), pageSize, DatabaseHeader.CurrentFormatVersion,
            null, null, new TableId(1), new IndexId(1), new TransactionId(1), new PageId(1), true);
        try
        {
            await using (var temporaryStore = FilePageStore.CreateNew(temporaryPath, pageSize))
            {
                var page = new byte[pageSize];
                DatabaseHeaderCodec.Write(page, header);
                await temporaryStore.WriteAsync(new PageId(0), page, cancellationToken).ConfigureAwait(false);
                await temporaryStore.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporaryPath, fullPath, false);
            return await OpenAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    public static async Task<PageDatabase> OpenAsync(string path, CancellationToken cancellationToken = default)
        => await OpenAsync(path, DatabaseOpenMode.Writer, cancellationToken).ConfigureAwait(false);

    public static async Task<PageDatabase> OpenAsync(string path, DatabaseOpenMode openMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var pageSize = await ProbePageSizeAsync(fullPath, cancellationToken).ConfigureAwait(false);
        if (!Enum.IsDefined(openMode)) throw new ArgumentOutOfRangeException(nameof(openMode));
        var store = FilePageStore.OpenExisting(fullPath, pageSize, openMode == DatabaseOpenMode.ReadOnly);
        try
        {
            var page = new byte[pageSize];
            await store.ReadAsync(new PageId(0), page, cancellationToken).ConfigureAwait(false);
            var header = DatabaseHeaderCodec.Read(page);
            if (openMode == DatabaseOpenMode.ReadOnly && !header.IsCleanShutdown) throw new RecoveryRequiredException();
            var database = new PageDatabase(store, header, openMode);
            await database.ValidateFreeListAsync(cancellationToken).ConfigureAwait(false);
            if (openMode == DatabaseOpenMode.Writer)
            {
                database._header = database._header with { IsCleanShutdown = false };
                await database.PersistHeaderAsync(cancellationToken).ConfigureAwait(false);
            }
            return database;
        }
        catch { await store.DisposeAsync().ConfigureAwait(false); throw; }
    }

    public ValueTask ReadAsync(PageId pageId, Memory<byte> destination, CancellationToken cancellationToken = default) =>
        _store.ReadAsync(pageId, destination, cancellationToken);

    public ValueTask WriteAsync(PageId pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) =>
        _openMode == DatabaseOpenMode.ReadOnly
            ? ValueTask.FromException(new InvalidOperationException("Read-only databases cannot be modified."))
            : _store.WriteAsync(pageId, source, cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken = default) => _store.FlushAsync(cancellationToken);

    public async ValueTask<PageId> AllocateAsync(PageType pageType, CancellationToken cancellationToken = default)
    {
        if (pageType is PageType.Unknown or PageType.DatabaseHeader or PageType.Free || !Enum.IsDefined(pageType))
            throw new ArgumentOutOfRangeException(nameof(pageType));
        await _allocationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            PageId id;
            if (_header.FreeListHeadPageId is { } freeId)
            {
                var freePage = await ReadValidatedPageAsync(freeId, PageType.Free, cancellationToken).ConfigureAwait(false);
                _header = _header with { FreeListHeadPageId = ReadFreeNext(freePage) };
                id = freeId;
            }
            else
            {
                id = _header.NextPageId;
                _header = _header with { NextPageId = new PageId(checked(id.Value + 1)) };
            }
            var page = CreatePage(id, pageType);
            await _store.WriteAsync(id, page, cancellationToken).ConfigureAwait(false);
            await PersistHeaderAsync(cancellationToken).ConfigureAwait(false);
            return id;
        }
        finally { _allocationLock.Release(); }
    }

    public async ValueTask FreeAsync(PageId pageId, CancellationToken cancellationToken = default)
    {
        if (pageId.Value == 0) throw new ArgumentOutOfRangeException(nameof(pageId), "Page zero cannot be freed.");
        await _allocationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (pageId.Value >= _header.NextPageId.Value) throw new StorageResourceException($"Cannot free unallocated {pageId}.", new KeyNotFoundException());
            var freeIds = await ReadFreeListIdsAsync(cancellationToken).ConfigureAwait(false);
            if (freeIds.Contains(pageId)) throw new StorageResourceException($"Cannot free {pageId} twice.", new InvalidOperationException());
            var page = CreatePage(pageId, PageType.Free);
            WriteFreeNext(page, _header.FreeListHeadPageId);
            PageChecksum.WriteChecksum(page, PageSize);
            await _store.WriteAsync(pageId, page, cancellationToken).ConfigureAwait(false);
            _header = _header with { FreeListHeadPageId = pageId };
            await PersistHeaderAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _allocationLock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _allocationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            await _store.FlushAsync().ConfigureAwait(false);
            if (_openMode == DatabaseOpenMode.Writer)
            {
                _header = _header with { IsCleanShutdown = true };
                await PersistHeaderAsync(CancellationToken.None).ConfigureAwait(false);
            }
            _disposed = true;
            await _store.DisposeAsync().ConfigureAwait(false);
        }
        finally { _allocationLock.Release(); _allocationLock.Dispose(); }
    }

    private async Task PersistHeaderAsync(CancellationToken cancellationToken)
    {
        var page = new byte[PageSize];
        DatabaseHeaderCodec.Write(page, _header);
        await _store.WriteAsync(new PageId(0), page, cancellationToken).ConfigureAwait(false);
        await _store.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private byte[] CreatePage(PageId id, PageType type)
    {
        var page = new byte[PageSize];
        PageHeaderCodec.Write(page, new PageHeader(id, type, PageFormatVersion.Current,
            new LogSequenceNumber(0), PageChecksumAlgorithm.Crc32, 0));
        PageChecksum.WriteChecksum(page, PageSize);
        return page;
    }

    private async Task<byte[]> ReadValidatedPageAsync(PageId id, PageType type, CancellationToken cancellationToken)
    {
        if (id.Value == 0 || id.Value >= _header.NextPageId.Value)
            throw new StorageCorruptionException($"Free list references out-of-range {id}.");
        var page = new byte[PageSize];
        await _store.ReadAsync(id, page, cancellationToken).ConfigureAwait(false);
        PageChecksum.ValidateChecksum(page, PageSize);
        PageHeaderCodec.Read(page).Validate(id, type);
        return page;
    }

    private async Task<HashSet<PageId>> ReadFreeListIdsAsync(CancellationToken cancellationToken)
    {
        HashSet<PageId> seen = [];
        var current = _header.FreeListHeadPageId;
        while (current is { } id)
        {
            if (!seen.Add(id)) throw new StorageCorruptionException($"Cycle detected in free list at {id}.");
            current = ReadFreeNext(await ReadValidatedPageAsync(id, PageType.Free, cancellationToken).ConfigureAwait(false));
        }
        return seen;
    }

    private async Task ValidateFreeListAsync(CancellationToken cancellationToken) =>
        _ = await ReadFreeListIdsAsync(cancellationToken).ConfigureAwait(false);

    private static void WriteFreeNext(Span<byte> page, PageId? next)
    {
        page[FreeNextOffset] = next.HasValue ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt64LittleEndian(page[(FreeNextOffset + 1)..], next.GetValueOrDefault().Value);
    }

    private static PageId? ReadFreeNext(ReadOnlySpan<byte> page) => page[FreeNextOffset] switch
    {
        0 when BinaryPrimitives.ReadUInt64LittleEndian(page[(FreeNextOffset + 1)..]) == 0 => null,
        1 => new PageId(BinaryPrimitives.ReadUInt64LittleEndian(page[(FreeNextOffset + 1)..])),
        _ => throw new StorageCorruptionException("Invalid free-list link encoding.")
    };

    private static async Task<int> ProbePageSizeAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                FileOptions.Asynchronous | FileOptions.RandomAccess);
            var prefix = new byte[64];
            var total = 0;
            while (total < prefix.Length)
            {
                var read = await RandomAccess.ReadAsync(handle, prefix.AsMemory(total), total, cancellationToken).ConfigureAwait(false);
                if (read == 0) throw new StorageFormatException("Database file is too short to contain a header.");
                total += read;
            }
            if (!prefix.AsSpan(32, 8).SequenceEqual("SQLSTORE"u8)) throw new InvalidDatabaseMagicException();
            var pageSize = BinaryPrimitives.ReadInt32LittleEndian(prefix.AsSpan(60, 4));
            if (!PageConstants.IsSupportedSize(pageSize)) throw new InvalidPageSizeException(pageSize);
            return pageSize;
        }
        catch (IOException exception) { throw new StorageResourceException($"Failed to inspect database '{path}'.", exception); }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>Selects mutable writer ownership or non-modifying diagnostic access.</summary>
public enum DatabaseOpenMode { Writer = 1, ReadOnly = 2 }
