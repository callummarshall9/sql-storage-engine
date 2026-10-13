// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Catalog;
using sql_storage_engine.Services.Foundations.PageFileSessions;
using sql_storage_engine.Services.Foundations.PageHeaders;

namespace sql_storage_engine.Services.Orchestrations.PageDatabaseAllocations;
internal sealed partial class PageDatabaseContextAllocationOrchestrationService(PageFileSessionService files, PageHeaderService headers) : IPageDatabaseContextAllocationOrchestrationService
{
    private const int FreeNextOffset = PageHeaderFormat.EncodedLength;
    public ValueTask<PageDatabaseContext> AllocatePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, PageType pageType, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateAllocation(pageDatabaseContext: pageDatabaseContext, pageType: pageType, cancellationToken: cancellationToken);

        var pageId = await AllocateAsyncCore(pageDatabaseContext: pageDatabaseContext, pageType: pageType, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return pageDatabaseContext with
        {
            ResultPageId = pageId.Value
        };
    });

    public ValueTask FreePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRelease(pageDatabaseContext: pageDatabaseContext, pageId: pageId, cancellationToken: cancellationToken);

        await FreeAsyncCore(pageDatabaseContext: pageDatabaseContext, pageId: new PageId { Value = pageId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask CheckPageDatabaseContextFreeListAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRuntime(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        await ValidateFreeListAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public PageDatabaseContext RebindPageDatabaseContextCatalog(PageDatabaseContext pageDatabaseContext, ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateCatalog(pageDatabaseContext: pageDatabaseContext, pageId: pageId, cancellationToken: cancellationToken);

        pageDatabaseContext.Runtime!.Header = pageDatabaseContext.Runtime.Header!with
        {
            CatalogRootPageId = new PageId
            {
                Value = pageId
            }
        };

        return pageDatabaseContext;
    });

    public PageDatabaseContext AllocatePageDatabaseContextRowVersion(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateMutation(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        return pageDatabaseContext with
        {
            ResultValue = AllocateRowVersionAsyncCore(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
        };
    });

    public PageDatabaseContext AllocatePageDatabaseContextGeneratedValue(PageDatabaseContext pageDatabaseContext, CatalogGeneratedAlwaysKind kind, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateGeneratedValue(pageDatabaseContext: pageDatabaseContext, kind: kind, cancellationToken: cancellationToken);

        return pageDatabaseContext with
        {
            ResultValue = AllocateGeneratedValueAsyncCore(pageDatabaseContext: pageDatabaseContext, kind: kind, cancellationToken: cancellationToken)
        };
    });

    private SqlValue AllocateRowVersionAsyncCore(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken = default)
    {
        {
            ThrowIfDisposed(pageDatabaseContext: pageDatabaseContext);

            if (pageDatabaseContext.Runtime!.OpenMode == DatabaseOpenMode.ReadOnly)
            {
                throw new InvalidOperationException(message: "Read-only databases cannot allocate rowversion values.");
            }

            var value = pageDatabaseContext.Runtime!.Header!.NextRowVersion;

            pageDatabaseContext.Runtime!.Header = pageDatabaseContext.Runtime!.Header!with
            {
                NextRowVersion = checked(value + 1)
            };

            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(destination: bytes, value: value);
            return CreateBinarySqlValue(value: bytes);
        }
    }

    private SqlValue AllocateGeneratedValueAsyncCore(PageDatabaseContext pageDatabaseContext, Catalog.CatalogGeneratedAlwaysKind kind, CancellationToken cancellationToken = default)
    {
        if (kind == Catalog.CatalogGeneratedAlwaysKind.RowStart)
        {
            return CreateDateTimeSqlValue(value: new DateTime(ticks: DateTime.UtcNow.Ticks, kind: DateTimeKind.Unspecified));
        }

        if (kind == Catalog.CatalogGeneratedAlwaysKind.RowEnd)
        {
            return CreateDateTimeSqlValue(value: DateTime.MaxValue);
        }

        if (kind == Catalog.CatalogGeneratedAlwaysKind.None)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(kind));
        }

        {
            ThrowIfDisposed(pageDatabaseContext: pageDatabaseContext);
            var value = pageDatabaseContext.Runtime!.Header!.NextGeneratedSequence;

            pageDatabaseContext.Runtime!.Header = pageDatabaseContext.Runtime!.Header!with
            {
                NextGeneratedSequence = checked(value + 1)
            };

            return CreateIntegerSqlValue(value: checked((long)value));
        }
    }

    private async ValueTask<PageId> AllocateAsyncCore(PageDatabaseContext pageDatabaseContext, PageType pageType, CancellationToken cancellationToken = default)
    {
        if (pageType is PageType.Unknown or PageType.DatabaseHeader or PageType.Free || !Enum.IsDefined(value: pageType))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(pageType));
        }

        {
            ThrowIfDisposed(pageDatabaseContext: pageDatabaseContext);
            PageId pageId;

            if (pageDatabaseContext.Runtime!.Header!.FreeListHeadPageId is { } freeId)
            {
                var freePage = await ReadValidatedPageAsync(pageDatabaseContext: pageDatabaseContext, pageId: freeId, type: PageType.Free, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                pageDatabaseContext.Runtime!.Header = pageDatabaseContext.Runtime!.Header!with
                {
                    FreeListHeadPageId = ReadFreeNext(page: freePage)
                };

                pageId = freeId;
            }
            else
            {
                pageId = pageDatabaseContext.Runtime!.Header!.NextPageId;

                if (pageDatabaseContext.Runtime!.MaximumAllocatedBytes is { } maximum && checked((pageId.Value + 1) * (ulong)GetPageSize(pageDatabaseContext: pageDatabaseContext)) > (ulong)maximum)
                {
                    throw new InvalidOperationException(message: "Transaction journal quota exceeded.");
                }

                pageDatabaseContext.Runtime!.Header = pageDatabaseContext.Runtime!.Header!with
                {
                    NextPageId = new sql_storage_engine.Models.Identifiers.PageId
                    {
                        Value = checked(pageId.Value + 1)
                    }
                };
            }

            var page = CreatePage(pageDatabaseContext: pageDatabaseContext, pageId: pageId, type: pageType);

            await WritePageAsync(pageDatabaseContext: pageDatabaseContext, pageId: pageId, source: page, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return pageId;
        }
    }

    private async ValueTask FreeAsyncCore(PageDatabaseContext pageDatabaseContext, PageId pageId, CancellationToken cancellationToken = default)
    {
        if (pageId.Value == 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(pageId), message: "Page zero cannot be freed.");
        }

        {
            ThrowIfDisposed(pageDatabaseContext: pageDatabaseContext);

            if (pageId.Value >= pageDatabaseContext.Runtime!.Header!.NextPageId.Value)
            {
                throw new StorageResourceException(message: $"Cannot free unallocated {pageId}.", innerException: new KeyNotFoundException());
            }

            var previous = new byte[GetPageSize(pageDatabaseContext: pageDatabaseContext)];

            await ReadPageAsync(pageDatabaseContext: pageDatabaseContext, pageId: pageId, destination: previous, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            headers.ValidateChecksum(page: previous, pageSize: GetPageSize(pageDatabaseContext: pageDatabaseContext));
            var previousHeader = headers.ReadPageHeader(source: previous);
            headers.CheckPageHeader(pageHeader: previousHeader, expectedPageId: (pageId).Value);

            if (previousHeader.PageType == PageType.Free)
            {
                throw new StorageResourceException(message: $"Cannot free {pageId} twice.", innerException: new InvalidOperationException());
            }

            var page = CreatePage(pageDatabaseContext: pageDatabaseContext, pageId: pageId, type: PageType.Free);
            WriteFreeNext(page: page, next: pageDatabaseContext.Runtime!.Header!.FreeListHeadPageId);
            headers.WriteChecksum(page: page, pageSize: GetPageSize(pageDatabaseContext: pageDatabaseContext));

            await WritePageAsync(pageDatabaseContext: pageDatabaseContext, pageId: pageId, source: page, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            pageDatabaseContext.Runtime!.Header = pageDatabaseContext.Runtime!.Header!with
            {
                FreeListHeadPageId = pageId
            };
        }
    }

    private byte[] CreatePage(PageDatabaseContext pageDatabaseContext, PageId pageId, PageType type)
    {
        var page = new byte[GetPageSize(pageDatabaseContext: pageDatabaseContext)];
        headers.WritePageHeader(destination: page, pageHeader: new PageHeader { PageId = pageId, PageType = type, FormatVersion = new sql_storage_engine.Models.Pages.PageFormatVersion { Value = sql_storage_engine.Models.Pages.PageHeaderFormat.CurrentVersion }, PageLogSequenceNumber = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 0 }, ChecksumAlgorithm = PageChecksumAlgorithm.Crc32, Checksum = 0 });
        headers.WriteChecksum(page: page, pageSize: GetPageSize(pageDatabaseContext: pageDatabaseContext));
        return page;
    }

    private async Task<byte[]> ReadValidatedPageAsync(PageDatabaseContext pageDatabaseContext, PageId pageId, PageType type, CancellationToken cancellationToken)
    {
        if (pageId.Value == 0 || pageId.Value >= pageDatabaseContext.Runtime!.Header!.NextPageId.Value)
        {
            throw new StorageCorruptionException(message: $"Free list references out-of-range {pageId}.");
        }

        var page = new byte[GetPageSize(pageDatabaseContext: pageDatabaseContext)];

        await ReadPageAsync(pageDatabaseContext: pageDatabaseContext, pageId: pageId, destination: page, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        headers.ValidateChecksum(page: page, pageSize: GetPageSize(pageDatabaseContext: pageDatabaseContext));
        headers.CheckPageHeader(pageHeader: headers.ReadPageHeader(source: page), expectedPageId: (pageId).Value, expectedType: type);
        return page;
    }

    private async Task<HashSet<PageId>> ReadFreeListIdsAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken)
    {
        HashSet<PageId> seen = [];
        var current = pageDatabaseContext.Runtime!.Header!.FreeListHeadPageId;

        while (current is { } pageId)
        {
            if (!seen.Add(item: pageId))
            {
                throw new StorageCorruptionException(message: $"Cycle detected in free list at {pageId}.");
            }

            current = ReadFreeNext(page: await ReadValidatedPageAsync(pageDatabaseContext: pageDatabaseContext, pageId: pageId, type: PageType.Free, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));
        }

        return seen;
    }

    private async Task ValidateFreeListAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken) =>
        _ = await ReadFreeListIdsAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

    private static void WriteFreeNext(Span<byte> page, PageId? next)
    {
        page[FreeNextOffset] = next.HasValue ? (byte)1 : (byte)0;

        BinaryPrimitives.WriteUInt64LittleEndian(destination: page[(FreeNextOffset + 1)..], value: next.GetValueOrDefault()
            .Value);
    }

    private static PageId? ReadFreeNext(ReadOnlySpan<byte> page) =>
        page[FreeNextOffset] switch
    {
        0 when BinaryPrimitives.ReadUInt64LittleEndian(source: page[(FreeNextOffset + 1)..]) == 0 => null,
        1 => new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = BinaryPrimitives.ReadUInt64LittleEndian(source: page[(FreeNextOffset + 1)..])
        },
        _ => throw new StorageCorruptionException(message: "Invalid free-list link encoding.")};

    private void ThrowIfDisposed(PageDatabaseContext pageDatabaseContext) =>
        ObjectDisposedException.ThrowIf(condition: pageDatabaseContext.Runtime!.Disposed, instance: this);

    private int GetPageSize(PageDatabaseContext pageDatabaseContext) =>
        pageDatabaseContext.Runtime!.Session!.PageSize;

    private ValueTask ReadPageAsync(PageDatabaseContext pageDatabaseContext, PageId pageId, Memory<byte> destination, CancellationToken cancellationToken) =>
        files.ReadPageFileSessionAsync(pageFileSession: pageDatabaseContext.Runtime!.Session!, pageId: pageId.Value, destination: destination, cancellationToken: cancellationToken);

    private ValueTask WritePageAsync(PageDatabaseContext pageDatabaseContext, PageId pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        files.WritePageFileSessionAsync(pageFileSession: pageDatabaseContext.Runtime!.Session!, pageId: pageId.Value, source: source, cancellationToken: cancellationToken);

    private static SqlValue CreateBinarySqlValue(ReadOnlySpan<byte> value) =>
        new BinarySqlValue
    {
        StorageFamily = SqlStorageFamily.Binary,
        Content = new string (value: value.ToArray()
            .Select(selector: item => (char)item)
            .ToArray())
    };

    private static SqlValue CreateDateTimeSqlValue(DateTime value) =>
        new DateTimeSqlValue
    {
        StorageFamily = SqlStorageFamily.DateTime,
        Value = DateTime.SpecifyKind(value: value, kind: DateTimeKind.Unspecified)
    };

    private static SqlValue CreateIntegerSqlValue(long value) =>
        new IntegerSqlValue
    {
        StorageFamily = SqlStorageFamily.Integer,
        Value = value
    };

    internal static PageDatabaseContextAllocationOrchestrationService CreatePageDatabaseContextAllocationOrchestrationService() =>
        new PageDatabaseContextAllocationOrchestrationService(files: PageFileSessionService.CreatePageFileSessionService(), headers: new PageHeaderService());

    public void CheckPageDatabaseContextPage(PageDatabaseContext pageDatabaseContext, ulong pageId, ReadOnlyMemory<byte> source) =>
        TryCatch(operation: () =>
    {
        ValidatePageInspection(pageDatabaseContext: pageDatabaseContext, pageId: pageId, source: source);
        headers.ValidateChecksum(page: source.Span, pageSize: pageDatabaseContext.Runtime!.Session!.PageSize);
        headers.CheckPageHeader(pageHeader: headers.ReadPageHeader(source: source.Span), expectedPageId: pageId);
    });
}