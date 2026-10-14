// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.MemoryPages;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Foundations.PageHeaders;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.MemoryPages;
internal sealed partial class PageHeaderMemoryPageIdProcessingService : IPageHeaderMemoryPageIdProcessingService
{
    private static readonly IPageHeaderService pageHeaders = new PageHeaderService();
    private readonly MemoryPageState state;
    public PageHeaderMemoryPageIdProcessingService(MemoryPageConfiguration configuration)
    {
        state = CreateMemoryPageState(configuration: configuration);
    }

    public int ReadMemoryPageSize() =>
        TryCatch(operation: () =>
    {
        ValidateMemoryPageSize(pageSize: state.PageSize);
        return state.PageSize;
    });

    public ValueTask<PageId> AllocateMemoryPageIdAsync(PageType pageType, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        lock (state.Sync)
        {
            ValidateMemoryPageOnAllocate(pageType: pageType, cancellationToken: cancellationToken);
            return AllocateMemoryPageCore(pageType: pageType, cancellationToken: cancellationToken);
        }
    });

    public ValueTask FreeMemoryPageAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        lock (state.Sync)
        {
            ValidateMemoryPageOnFree(pageId: pageId, cancellationToken: cancellationToken);
            return FreeMemoryPageCore(pageId: new PageId { Value = pageId }, cancellationToken: cancellationToken);
        }
    });

    public ValueTask ReadMemoryPageAsync(ulong pageId, Memory<byte> destination, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        lock (state.Sync)
        {
            ValidateMemoryPageOnRead(pageId: pageId, destination: destination, cancellationToken: cancellationToken);
            return ReadMemoryPageCore(pageId: new PageId { Value = pageId }, destination: destination, cancellationToken: cancellationToken);
        }
    });

    public ValueTask WriteMemoryPageAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        lock (state.Sync)
        {
            ValidateMemoryPageOnWrite(pageId: pageId, source: source, cancellationToken: cancellationToken);
            return WriteMemoryPageCore(pageId: new PageId { Value = pageId }, source: source, cancellationToken: cancellationToken);
        }
    });

    public ValueTask FlushMemoryPagesAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        lock (state.Sync)
        {
            ValidateMemoryPageOnFlush(cancellationToken: cancellationToken);
            return FlushMemoryPagesCore(cancellationToken: cancellationToken);
        }
    });

    public ValueTask CloseMemoryPagesAsync() =>
        TryCatch(operation: () =>
    {
        return CloseMemoryPagesCore();
    });

    private static MemoryPageState CreateMemoryPageState(MemoryPageConfiguration configuration) =>
        TryCatch(operation: () =>
    {
        ValidateMemoryPageConfiguration(configuration: configuration);

        return new MemoryPageState
        {
            Sync = new object (),
            Pages = new Dictionary<PageId, byte[]>(),
            Free = new Stack<PageId>(),
            PageSize = configuration.PageSize,
            NextPageId = configuration.ReservePageZero ? 1UL : 0UL
        };
    });

    private ValueTask<PageId> AllocateMemoryPageCore(PageType pageType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (state.Sync)
        {
            ThrowIfDisposed();

            if (!Enum.IsDefined(value: pageType) || pageType == PageType.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(pageType));
            }

            var id = state.Free.TryPop(result: out var reused) ? reused : new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = checked(state.NextPageId++)
            };

            var page = new byte[state.PageSize];
            pageHeaders.WritePageHeader(destination: page, pageHeader: new PageHeader { PageId = id, PageType = pageType, FormatVersion = new sql_storage_engine.Models.Pages.PageFormatVersion { Value = sql_storage_engine.Models.Pages.PageHeaderFormat.CurrentVersion }, PageLogSequenceNumber = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 0 }, ChecksumAlgorithm = PageChecksumAlgorithm.Crc32, Checksum = 0 });
            pageHeaders.WriteChecksum(page: page, pageSize: state.PageSize);
            state.Pages.Add(key: id, value: page);
            return ValueTask.FromResult(result: id);
        }
    }

    private ValueTask FreeMemoryPageCore(PageId pageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (state.Sync)
        {
            ThrowIfDisposed();

            if (!state.Pages.Remove(key: pageId))
            {
                throw new StorageResourceException($"Cannot free unknown or already freed {pageId}.", new KeyNotFoundException());
            }

            state.Free.Push(item: pageId);
            return ValueTask.CompletedTask;
        }
    }

    private ValueTask ReadMemoryPageCore(PageId pageId, Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        ValidateBuffer(length: destination.Length, parameterName: nameof(destination));
        cancellationToken.ThrowIfCancellationRequested();

        lock (state.Sync)
        {
            ThrowIfDisposed();

            if (!state.Pages.TryGetValue(key: pageId, value: out var page))
            {
                throw new StorageResourceException($"Unknown or freed {pageId}.", new KeyNotFoundException());
            }

            page.CopyTo(destination: destination.Span);
            return ValueTask.CompletedTask;
        }
    }

    private ValueTask WriteMemoryPageCore(PageId pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        ValidateBuffer(length: source.Length, parameterName: nameof(source));
        cancellationToken.ThrowIfCancellationRequested();

        lock (state.Sync)
        {
            ThrowIfDisposed();

            if (!state.Pages.TryGetValue(key: pageId, value: out var page))
            {
                throw new StorageResourceException($"Unknown or freed {pageId}.", new KeyNotFoundException());
            }

            source.Span.CopyTo(destination: page);
            return ValueTask.CompletedTask;
        }
    }

    private ValueTask FlushMemoryPagesCore(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (state.Sync)
        {
            ThrowIfDisposed();
        }

        return ValueTask.CompletedTask;
    }

    private ValueTask CloseMemoryPagesCore()
    {
        lock (state.Sync)
        {
            state.IsClosed = true;
            state.Pages.Clear();
            state.Free.Clear();
        }

        return ValueTask.CompletedTask;
    }

    private void ValidateBuffer(int length, string parameterName)
    {
        if (length != state.PageSize)
        {
            throw new ArgumentException($"Buffer must be exactly {state.PageSize} bytes.", parameterName);
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(condition: state.IsClosed, instance: this);
}