// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Coordinations.ObjectDatabases;
using sql_storage_engine.Services.Coordinations.PageDatabases;

namespace sql_storage_engine.Services.Managements.StorageStrategies;
internal sealed partial class StorageStrategyContextManagementService : IStorageStrategyContextManagementService
{
    private readonly PageDatabaseContextCoordinationService pages;
    private readonly ObjectDatabaseContextCoordinationService objects;
    private readonly StorageStrategyOptions options;
    private readonly bool remote;
    private StorageStrategyContextManagementService(PageDatabaseContextCoordinationService pages, ObjectDatabaseContextCoordinationService objects, StorageStrategyConfiguration storageStrategyConfiguration)
    {
        this.pages = pages;
        this.objects = objects;
        options = storageStrategyConfiguration.Options!;
        remote = storageStrategyConfiguration.Remote;
    }

    internal static StorageStrategyContextManagementService CreateStorageStrategyContextManagementService(StorageStrategyContext storageStrategyContext)
    {
        ValidateConfiguration(storageStrategyContext: storageStrategyContext);

        return new StorageStrategyContextManagementService(pages: PageDatabaseContextCoordinationService.CreatePageDatabaseContextCoordinationService(), objects: ObjectDatabaseContextCoordinationService.CreateObjectDatabaseContextCoordinationService(objectResourceConfiguration: storageStrategyContext.Objects), storageStrategyConfiguration: new StorageStrategyConfiguration { Options = NormalizeOptions(storageStrategyOptions: storageStrategyContext.Options!), Remote = storageStrategyContext.Objects is not null });
    }

    public ValueTask<StorageStrategyContext> ProcessStorageStrategyContextAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRequest(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken);

        if (storageStrategyContext.Operation is 0 or 1)
        {
            var created = remote ? await objects.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext with { Options = options }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false) : await OpenFileAsync(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            created.Service = this;
            return created;
        }

        var state = storageStrategyContext.State!;

        if (state.Disposed && storageStrategyContext.Operation == 9)
        {
            return storageStrategyContext;
        }

        await objects.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext with { Operation = 101 }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            if (state.Disposed)
            {
                if (storageStrategyContext.Operation == 9)
                {
                    return storageStrategyContext;
                }

                throw new ObjectDisposedException(objectName: "StorageDatabaseExposure");
            }

            var result = state.Remote ? await objects.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false) : await ProcessFileAsync(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            if (storageStrategyContext.Operation == 9)
            {
                await objects.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext with { Operation = 103 }, cancellationToken: CancellationToken.None)
                    .ConfigureAwait(continueOnCapturedContext: false);
            }

            return result;
        }
        finally
        {
            await objects.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext with { Operation = 102 }, cancellationToken: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
    });

    public StorageStrategyContext ReadStorageStrategyContext(StorageStrategyContext storageStrategyContext) =>
        TryCatch(operation: () =>
    {
        ValidateRead(storageStrategyContext: storageStrategyContext);
        var state = storageStrategyContext.State!;

        switch (storageStrategyContext.Operation)
        {
            case 10:
                storageStrategyContext.PageSize = state.PageSize;
                break;
            case 11:
                storageStrategyContext.Header = state.Remote ? state.Header : pages.ReadPageDatabaseContext(pageDatabaseContext: state.Database!).Runtime!.Header;
                break;
            case 12:
                storageStrategyContext.Status = new StorageCommitStatus { CommittedGeneration = state.CommittedGeneration, PendingPages = state.DirtyPages?.Count ?? 0, LastError = state.LastError };
                break;
        }

        return storageStrategyContext;
    });

    private async ValueTask<StorageStrategyContext> OpenFileAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken)
    {
        var path = options.Directory!.TrimEnd(trimChars: ['/', '\\']) + "/" + storageStrategyContext.Name + ".db";
        var request = new PageDatabaseContext { Path = path, PageSize = storageStrategyContext.PageSize, OpenMode = DatabaseOpenMode.Writer };

        var database = storageStrategyContext.Operation == 0 ? await pages.CreatePageDatabaseContextAsync(pageDatabaseContext: request, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) : await pages.OpenPageDatabaseContextAsync(pageDatabaseContext: request, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var state = new StorageSessionState { Name = storageStrategyContext.Name, Options = options, Database = database, Header = database.Runtime!.Header, PageSize = database.Runtime.Header!.PageSize, DirtyPages = new HashSet<ulong>() };
        var result = storageStrategyContext with { State = state, Options = options };

        try
        {
            await objects.ProcessStorageStrategyContextAsync(storageStrategyContext: result with { Operation = 100 }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return result;
        }
        catch
        {
            await pages.ClosePageDatabaseContextAsync(pageDatabaseContext: database)
                .ConfigureAwait(continueOnCapturedContext: false);

            throw;
        }
    }

    private async ValueTask<StorageStrategyContext> ProcessFileAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken)
    {
        var state = storageStrategyContext.State!;
        var database = state.Database!;

        try
        {
            switch (storageStrategyContext.Operation)
            {
                case 2:
                    await pages.ReadPageDatabaseContextAsync(pageDatabaseContext: database, pageId: storageStrategyContext.PageId, destination: storageStrategyContext.Destination, cancellationToken: cancellationToken)
                        .ConfigureAwait(continueOnCapturedContext: false);
                    break;
                case 3:
                    await pages.WritePageDatabaseContextAsync(pageDatabaseContext: database, pageId: storageStrategyContext.PageId, source: storageStrategyContext.Source, cancellationToken: cancellationToken)
                        .ConfigureAwait(continueOnCapturedContext: false);
                    state.DirtyPages!.Add(item: storageStrategyContext.PageId);
                    break;
                case 4:
                case 8:
                    await pages.FlushPageDatabaseContextAsync(pageDatabaseContext: database, cancellationToken: cancellationToken)
                        .ConfigureAwait(continueOnCapturedContext: false);
                    state.DirtyPages!.Clear();
                    if (storageStrategyContext.Operation == 8)
                    {
                        state.CommittedGeneration = checked(state.CommittedGeneration + 1);
                    }
                    state.LastError = null;
                    break;
                case 5:
                    storageStrategyContext.PageId = (await pages.AllocatePageDatabaseContextAsync(pageDatabaseContext: database, pageType: storageStrategyContext.PageType, cancellationToken: cancellationToken)
                        .ConfigureAwait(continueOnCapturedContext: false)).ResultPageId;
                    state.DirtyPages!.Add(item: storageStrategyContext.PageId);
                    state.DirtyPages.Add(item: 0);
                    break;
                case 6:
                    await pages.FreePageDatabaseContextAsync(pageDatabaseContext: database, pageId: storageStrategyContext.PageId, cancellationToken: cancellationToken)
                        .ConfigureAwait(continueOnCapturedContext: false);
                    state.DirtyPages!.Add(item: storageStrategyContext.PageId);
                    state.DirtyPages.Add(item: 0);
                    break;
                case 7:
                    await pages.PublishPageDatabaseContextCatalogAsync(pageDatabaseContext: database, pageId: storageStrategyContext.PageId, cancellationToken: cancellationToken)
                        .ConfigureAwait(continueOnCapturedContext: false);
                    state.DirtyPages!.Add(item: 0);
                    break;
                case 9:
                    await pages.ClosePageDatabaseContextAsync(pageDatabaseContext: database)
                        .ConfigureAwait(continueOnCapturedContext: false);
                    state.Disposed = true;
                    break;
            }

            state.Header = database.Runtime!.Header;
            return storageStrategyContext;
        }
        catch (Exception exception)
        {
            state.LastError = exception.Message;
            throw;
        }
    }
}