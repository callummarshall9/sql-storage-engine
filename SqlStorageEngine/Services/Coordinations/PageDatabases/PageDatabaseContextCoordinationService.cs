// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Orchestrations.PageDatabaseFiles;
using sql_storage_engine.Services.Orchestrations.PageDatabaseAllocations;
using sql_storage_engine.Services.Orchestrations.DatabaseHeaders;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Coordinations.PageDatabases;
internal sealed partial class PageDatabaseContextCoordinationService(PageDatabaseContextFileOrchestrationService lifecycle, PageDatabaseContextAllocationOrchestrationService allocations, DatabaseHeaderOrchestrationService headers) : IPageDatabaseContextCoordinationService
{
    public ValueTask<PageDatabaseContext> CreatePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCreation(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);
        var prepared = lifecycle.PreparePageDatabaseContextPublication(pageDatabaseContext: pageDatabaseContext);

        try
        {
            var header = new DatabaseHeader
            {
                DatabaseId = new DatabaseId
                {
                    Value = prepared.DatabaseIdentity
                },
                PageSize = prepared.PageSize,
                FormatVersion = DatabaseHeaderFormat.CurrentVersion,
                NextPageId = new PageId
                {
                    Value = 1
                },
                IsCleanShutdown = true
            };

            var page = new byte[prepared.PageSize];
            headers.WriteDatabaseHeader(page: page, databaseHeader: header);

            await lifecycle.PublishPageDatabaseContextAsync(pageDatabaseContext: prepared, page: page, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return await OpenCoreAsync(pageDatabaseContext: prepared, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch
        {
            lifecycle.RemovePageDatabaseContextPublication(pageDatabaseContext: prepared);
            throw;
        }
    });

    public ValueTask<PageDatabaseContext> OpenPageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateOpen(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        return await OpenCoreAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public PageDatabaseContext ReadPageDatabaseContext(PageDatabaseContext pageDatabaseContext) =>
        TryCatch(operation: () =>
    {
        ValidateState(pageDatabaseContext: pageDatabaseContext);
        return pageDatabaseContext;
    });

    public void SetPageDatabaseContextMaximumLength(PageDatabaseContext pageDatabaseContext, long? maximumAllocatedBytes) =>
        TryCatch(operation: () =>
    {
        ValidateMaximumLength(pageDatabaseContext: pageDatabaseContext, maximumAllocatedBytes: maximumAllocatedBytes);
        pageDatabaseContext.Runtime!.MaximumAllocatedBytes = maximumAllocatedBytes;
    });

    public void RestorePageDatabaseContextLength(PageDatabaseContext pageDatabaseContext, long length) =>
        TryCatch(operation: () =>
    {
        ValidateLength(pageDatabaseContext: pageDatabaseContext, length: length);
        lifecycle.RestorePageDatabaseContextFileLength(pageDatabaseContext: pageDatabaseContext, length: length);
    });

    public ValueTask ReadPageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRead(pageDatabaseContext: pageDatabaseContext, pageId: pageId, destination: destination, cancellationToken: cancellationToken);

        await lifecycle.ReadPageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, pageId: pageId, destination: destination, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask WritePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(pageDatabaseContext: pageDatabaseContext, pageId: pageId, source: source, cancellationToken: cancellationToken);

        await lifecycle.WritePageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, pageId: pageId, source: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FlushPageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRuntime(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        await lifecycle.FlushPageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask ReloadPageDatabaseContextHeaderAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRuntime(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);
        var page = new byte[pageDatabaseContext.Runtime!.Session!.PageSize];

        await lifecycle.ReadPageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, pageId: 0, destination: page, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        pageDatabaseContext.Runtime.Header = headers.ReadDatabaseHeader(page: page);
    });

    public ValueTask<PageDatabaseContext> AllocatePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, PageType pageType, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateAllocation(pageDatabaseContext: pageDatabaseContext, pageType: pageType, cancellationToken: cancellationToken);

        await lifecycle.EnterPageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            var result = await allocations.AllocatePageDatabaseContextAsync(pageDatabaseContext: pageDatabaseContext, pageType: pageType, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await PersistHeaderAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return result;
        }
        finally
        {
            lifecycle.ExitPageDatabaseContextFile(pageDatabaseContext: pageDatabaseContext);
        }
    });

    public ValueTask FreePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRelease(pageDatabaseContext: pageDatabaseContext, pageId: pageId, cancellationToken: cancellationToken);

        await lifecycle.EnterPageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            await allocations.FreePageDatabaseContextAsync(pageDatabaseContext: pageDatabaseContext, pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await PersistHeaderAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            lifecycle.ExitPageDatabaseContextFile(pageDatabaseContext: pageDatabaseContext);
        }
    });

    public ValueTask PublishPageDatabaseContextCatalogAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRelease(pageDatabaseContext: pageDatabaseContext, pageId: pageId, cancellationToken: cancellationToken);

        await lifecycle.EnterPageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            allocations.RebindPageDatabaseContextCatalog(pageDatabaseContext: pageDatabaseContext, pageId: pageId, cancellationToken: cancellationToken);

            await PersistHeaderAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            lifecycle.ExitPageDatabaseContextFile(pageDatabaseContext: pageDatabaseContext);
        }
    });

    public ValueTask ClosePageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext) =>
        TryCatch(operation: async () =>
    {
        ValidateState(pageDatabaseContext: pageDatabaseContext);

        if (pageDatabaseContext.Runtime!.Disposed)
        {
            return;
        }

        await lifecycle.EnterPageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            if (pageDatabaseContext.Runtime.Disposed)
            {
                return;
            }

            await lifecycle.FlushPageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (pageDatabaseContext.Runtime.OpenMode == DatabaseOpenMode.Writer)
            {
                pageDatabaseContext.Runtime.Header = pageDatabaseContext.Runtime.Header!with
                {
                    IsCleanShutdown = true
                };

                await PersistHeaderAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            pageDatabaseContext.Runtime.Disposed = true;

            await lifecycle.ClosePageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            lifecycle.ExitPageDatabaseContextFile(pageDatabaseContext: pageDatabaseContext);
        }
    });

    private async ValueTask<PageDatabaseContext> OpenCoreAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken)
    {
        var opened = await lifecycle.OpenPageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            var page = new byte[opened.Runtime!.Session!.PageSize];

            await lifecycle.ReadPageDatabaseContextFileAsync(pageDatabaseContext: opened, pageId: 0, destination: page, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            opened.Runtime.Header = headers.ReadDatabaseHeader(page: page);

            if (opened.Runtime.OpenMode == DatabaseOpenMode.ReadOnly && !opened.Runtime.Header.IsCleanShutdown)
            {
                throw new RecoveryRequiredException();
            }

            await allocations.CheckPageDatabaseContextFreeListAsync(pageDatabaseContext: opened, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (opened.Runtime.OpenMode == DatabaseOpenMode.Writer)
            {
                opened.Runtime.Header = opened.Runtime.Header with
                {
                    IsCleanShutdown = false
                };

                await PersistHeaderAsync(pageDatabaseContext: opened, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            return opened;
        }
        catch
        {
            await lifecycle.ClosePageDatabaseContextFileAsync(pageDatabaseContext: opened)
            .ConfigureAwait(continueOnCapturedContext: false);

            throw;
        }
    }

    private async ValueTask PersistHeaderAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken)
    {
        var page = new byte[pageDatabaseContext.Runtime!.Session!.PageSize];
        headers.WriteDatabaseHeader(page: page, databaseHeader: pageDatabaseContext.Runtime.Header!);

        await lifecycle.WritePageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, pageId: 0, source: page, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await lifecycle.FlushPageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    internal static PageDatabaseContextCoordinationService CreatePageDatabaseContextCoordinationService() =>
        new PageDatabaseContextCoordinationService(lifecycle: PageDatabaseContextFileOrchestrationService.CreatePageDatabaseContextFileOrchestrationService(), allocations: PageDatabaseContextAllocationOrchestrationService.CreatePageDatabaseContextAllocationOrchestrationService(), headers: new DatabaseHeaderOrchestrationService());

    public void CheckPageDatabaseContextPage(PageDatabaseContext pageDatabaseContext, ulong pageId, ReadOnlyMemory<byte> source) =>
        TryCatch(operation: () =>
    {
        ValidatePageInspection(pageDatabaseContext: pageDatabaseContext, pageId: pageId, source: source);
        allocations.CheckPageDatabaseContextPage(pageDatabaseContext: pageDatabaseContext, pageId: pageId, source: source);
    });
}