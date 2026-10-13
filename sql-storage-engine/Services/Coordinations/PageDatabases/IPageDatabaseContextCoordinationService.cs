// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;
using sql_storage_engine.Catalog;

namespace sql_storage_engine.Services.Coordinations.PageDatabases;
internal interface IPageDatabaseContextCoordinationService
{
    ValueTask<PageDatabaseContext> CreatePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken);

    ValueTask<PageDatabaseContext> OpenPageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken);

    PageDatabaseContext ReadPageDatabaseContext(PageDatabaseContext pageDatabaseContext);

    void SetPageDatabaseContextMaximumLength(PageDatabaseContext pageDatabaseContext, long? maximumAllocatedBytes);

    void RestorePageDatabaseContextLength(PageDatabaseContext pageDatabaseContext, long length);

    ValueTask ReadPageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken);

    ValueTask WritePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask FlushPageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken);

    ValueTask ReloadPageDatabaseContextHeaderAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken);

    ValueTask<PageDatabaseContext> AllocatePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, PageType pageType, CancellationToken cancellationToken);

    ValueTask FreePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, CancellationToken cancellationToken);

    ValueTask PublishPageDatabaseContextCatalogAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, CancellationToken cancellationToken);

    ValueTask<PageDatabaseContext> AllocatePageDatabaseContextRowVersionAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken);

    ValueTask<PageDatabaseContext> AllocatePageDatabaseContextGeneratedValueAsync(PageDatabaseContext pageDatabaseContext, CatalogGeneratedAlwaysKind kind, CancellationToken cancellationToken);

    ValueTask ClosePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext);

    void CheckPageDatabaseContextPage(PageDatabaseContext pageDatabaseContext, ulong pageId, ReadOnlyMemory<byte> source);
}