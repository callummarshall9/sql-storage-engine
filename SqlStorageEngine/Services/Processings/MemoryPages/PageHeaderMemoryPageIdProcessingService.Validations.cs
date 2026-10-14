// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Pages;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.MemoryPages;
internal sealed partial class PageHeaderMemoryPageIdProcessingService
{
    private static void ValidateMemoryPageConfiguration(MemoryPageConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(argument: configuration);
        ValidateMemoryPageSize(pageSize: configuration.PageSize);
    }

    private static void ValidateMemoryPageSize(int pageSize) =>
        Validate(inputs: [(!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(pageSize)), "Unsupported memory page size.")]);

    private void ValidateMemoryPageOnAllocate(PageType pageType, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        Validate(inputs: [(!Enum.IsDefined(value: pageType) || pageType == PageType.Unknown, "Unsupported page type.")]);
    }

    private void ValidateMemoryPageOnFree(ulong pageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ValidateLiveMemoryPage(pageId: pageId);
    }

    private void ValidateMemoryPageOnRead(ulong pageId, Memory<byte> destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        Validate(inputs: [(destination.Length != state.PageSize, "Destination must match the page size.")]);
        ValidateLiveMemoryPage(pageId: pageId);
    }

    private void ValidateMemoryPageOnWrite(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        Validate(inputs: [(source.Length != state.PageSize, "Source must match the page size.")]);
        ValidateLiveMemoryPage(pageId: pageId);
    }

    private void ValidateMemoryPageOnFlush(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
    }

    private void ValidateLiveMemoryPage(ulong pageId)
    {
        if (!state.Pages.ContainsKey(key: new PageId { Value = pageId }))
        {
            throw new StorageResourceException(message: $"Unknown or freed page {pageId}.", innerException: new KeyNotFoundException());
        }
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: input.Message);
            }
        }
    }
}