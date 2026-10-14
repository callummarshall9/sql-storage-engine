// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.Services.Processings.MemoryPages;
internal interface IPageHeaderMemoryPageIdProcessingService
{
    int ReadMemoryPageSize();

    ValueTask<PageId> AllocateMemoryPageIdAsync(PageType pageType, CancellationToken cancellationToken);

    ValueTask FreeMemoryPageAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask ReadMemoryPageAsync(ulong pageId, Memory<byte> destination, CancellationToken cancellationToken);

    ValueTask WriteMemoryPageAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask FlushMemoryPagesAsync(CancellationToken cancellationToken);

    ValueTask CloseMemoryPagesAsync();
}