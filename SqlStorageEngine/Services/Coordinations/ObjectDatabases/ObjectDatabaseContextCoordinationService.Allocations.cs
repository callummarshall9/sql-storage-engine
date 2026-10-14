// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Coordinations.ObjectDatabases;
internal sealed partial class ObjectDatabaseContextCoordinationService
{
    private async ValueTask AllocateAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken)
    {
        var state = storageStrategyContext.State!;
        var header = state.Header!;

        if (storageStrategyContext.PageType is PageType.Unknown or PageType.DatabaseHeader or PageType.Free || !Enum.IsDefined(value: storageStrategyContext.PageType))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(storageStrategyContext.PageType));
        }

        var pageId = header.FreeListHeadPageId ?? header.NextPageId;
        PageId? next = null;

        if (header.FreeListHeadPageId.HasValue)
        {
            var previous = await ReadAndCheckPageAsync(state: state, pageId: pageId.Value, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            if (previous.PageType != PageType.Free)
            {
                throw new StorageCorruptionException(message: "Free list references a live page.");
            }

            next = ReadNextFreePage(bytes: previous.Bytes!);

            if (next.HasValue && (next.Value.Value == 0 || next.Value.Value == pageId.Value || next.Value.Value >= header.NextPageId.Value))
            {
                throw new StorageCorruptionException(message: "Free list link is out of range or cyclic.");
            }
        }

        await ReserveMutationAsync(state: state, pageIds: new ulong[] { 0, pageId.Value }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        cancellationToken.ThrowIfCancellationRequested();

        var initialized = await cache.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { Operation = 120, State = state, PageId = pageId.Value, PageType = storageStrategyContext.PageType }, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        state.Header = header with
        {
            FreeListHeadPageId = header.FreeListHeadPageId.HasValue ? next : null,
            NextPageId = header.FreeListHeadPageId.HasValue ? header.NextPageId : new PageId { Value = checked(pageId.Value + 1) }
        };

        await WritePageAsync(storageStrategyContext: new StorageStrategyContext { State = state, PageId = pageId.Value }, bytes: initialized.Bytes!, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        await WriteHeaderAsync(state: state, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        storageStrategyContext.PageId = pageId.Value;
    }

    private async ValueTask FreeAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken)
    {
        var state = storageStrategyContext.State!;
        var pageId = storageStrategyContext.PageId;
        RequireAllocatedPage(state: state, pageId: pageId);

        var previous = await ReadAndCheckPageAsync(state: state, pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (previous.PageType == PageType.Free)
        {
            throw new InvalidOperationException(message: "A page cannot be freed twice.");
        }

        await ReserveMutationAsync(state: state, pageIds: new ulong[] { 0, pageId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        cancellationToken.ThrowIfCancellationRequested();

        var initialized = await cache.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { Operation = 120, State = state, PageId = pageId, PageType = PageType.Free }, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        var next = state.Header!.FreeListHeadPageId;
        initialized.Bytes![PageHeaderFormat.EncodedLength] = next.HasValue ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt64LittleEndian(destination: initialized.Bytes.AsSpan(start: PageHeaderFormat.EncodedLength + 1), value: next.GetValueOrDefault().Value);

        await cache.ProcessStorageStrategyContextAsync(storageStrategyContext: initialized with { Operation = 122 }, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        await WritePageAsync(storageStrategyContext: new StorageStrategyContext { State = state, PageId = pageId }, bytes: initialized.Bytes, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        state.Header = state.Header with { FreeListHeadPageId = new PageId { Value = pageId } };

        await WriteHeaderAsync(state: state, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async ValueTask PublishRootAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken)
    {
        var state = storageStrategyContext.State!;
        RequireAllocatedPage(state: state, pageId: storageStrategyContext.PageId);

        var page = await ReadAndCheckPageAsync(state: state, pageId: storageStrategyContext.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (page.PageType == PageType.Free)
        {
            throw new ArgumentException(message: "A free page cannot be a metadata root.", paramName: nameof(storageStrategyContext.PageId));
        }

        await ReserveMutationAsync(state: state, pageIds: new ulong[] { 0 }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        cancellationToken.ThrowIfCancellationRequested();
        state.Header = state.Header! with { CatalogRootPageId = new PageId { Value = storageStrategyContext.PageId } };

        await WriteHeaderAsync(state: state, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async ValueTask<StorageStrategyContext> ReadAndCheckPageAsync(StorageSessionState state, ulong pageId, CancellationToken cancellationToken)
    {
        RequireAllocatedPage(state: state, pageId: pageId);
        var context = new StorageStrategyContext { State = state, PageId = pageId };

        context.Bytes = await ReadPageAsync(storageStrategyContext: context, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return await cache.ProcessStorageStrategyContextAsync(storageStrategyContext: context with { Operation = 121 }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private static void RequireAllocatedPage(StorageSessionState state, ulong pageId)
    {
        if (pageId == 0 || pageId >= state.Header!.NextPageId.Value)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(pageId), message: "An allocated non-header page is required.");
        }
    }

    private static PageId? ReadNextFreePage(byte[] bytes)
    {
        var value = BinaryPrimitives.ReadUInt64LittleEndian(source: bytes.AsSpan(start: PageHeaderFormat.EncodedLength + 1));

        return bytes[PageHeaderFormat.EncodedLength] switch
        {
            0 when value == 0 => null,
            1 => new PageId { Value = value },
            _ => throw new StorageCorruptionException(message: "Free-list link encoding is invalid.")
        };
    }
}