// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.Services.Orchestrations.PageDatabaseFiles;
internal interface IPageDatabaseContextFileOrchestrationService
{
    PageDatabaseContext PreparePageDatabaseContextPublication(PageDatabaseContext pageDatabaseContext);

    ValueTask<PageDatabaseContext> OpenPageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken);

    ValueTask PublishPageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, byte[] page, CancellationToken cancellationToken);

    void RemovePageDatabaseContextPublication(PageDatabaseContext pageDatabaseContext);

    ValueTask ReadPageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken);

    ValueTask WritePageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask FlushPageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken);

    ValueTask ClosePageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext);

    void RestorePageDatabaseContextFileLength(PageDatabaseContext pageDatabaseContext, long length);

    ValueTask EnterPageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken);

    void ExitPageDatabaseContextFile(PageDatabaseContext pageDatabaseContext);
}