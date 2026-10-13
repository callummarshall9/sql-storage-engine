// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;
using sql_storage_engine.Catalog;

namespace sql_storage_engine.Services.Orchestrations.PageDatabaseAllocations;
internal interface IPageDatabaseContextAllocationOrchestrationService
{
    ValueTask<PageDatabaseContext> AllocatePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, PageType pageType, CancellationToken cancellationToken);

    ValueTask FreePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, CancellationToken cancellationToken);

    ValueTask CheckPageDatabaseContextFreeListAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken);

    PageDatabaseContext RebindPageDatabaseContextCatalog(PageDatabaseContext pageDatabaseContext, ulong pageId, CancellationToken cancellationToken);

    PageDatabaseContext AllocatePageDatabaseContextRowVersion(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken);

    PageDatabaseContext AllocatePageDatabaseContextGeneratedValue(PageDatabaseContext pageDatabaseContext, CatalogGeneratedAlwaysKind kind, CancellationToken cancellationToken);

    void CheckPageDatabaseContextPage(PageDatabaseContext pageDatabaseContext, ulong pageId, ReadOnlyMemory<byte> source);
}