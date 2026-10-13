// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Catalog;
using sql_storage_engine.Brokers.PageFileSessions;
using sql_storage_engine.Brokers.Directories;
using sql_storage_engine.Services.Foundations.PageFileSessions;
using sql_storage_engine.Services.Foundations.PageHeaders;
using sql_storage_engine.Services.Foundations.TransactionDirectories;
using sql_storage_engine.Services.Processings.PageDatabaseFiles;
using sql_storage_engine.Services.Processings.DatabaseFileProbes;
using sql_storage_engine.Services.Processings.StatementJournals;
using sql_storage_engine.Services.Orchestrations.PageDatabaseFiles;
using sql_storage_engine.Services.Orchestrations.PageDatabaseAllocations;
using sql_storage_engine.Services.Orchestrations.DatabaseHeaders;
using sql_storage_engine.Services.Coordinations.PageDatabases;

namespace sql_storage_engine.Exposures.Pages;
public sealed class PageDatabaseExposure : IPageDatabaseExposure, sql_storage_engine.Exposures.Pages.IPageStoreExposure, sql_storage_engine.Exposures.Pages.IPageAllocatorExposure, ICompositionExposure
{
    private static readonly IPageDatabaseContextCoordinationService service = new PageDatabaseContextCoordinationService(lifecycle: new PageDatabaseContextFileOrchestrationService(files: new PageFileSessionPageDatabaseContextProcessingService(files: new PageFileSessionService(broker: new PageFileSessionBroker())), pathsProcessing: new TransactionDirectoryDatabaseFileProcessingService(directories: new TransactionDirectoryService(transactionDirectoryBroker: new TransactionDirectoryBroker())), journals: new TransactionDirectoryStatementJournalProcessingService(transactionDirectoryService: new TransactionDirectoryService(transactionDirectoryBroker: new TransactionDirectoryBroker()))), allocations: new PageDatabaseContextAllocationOrchestrationService(files: new PageFileSessionService(broker: new PageFileSessionBroker()), headers: new PageHeaderService()), headers: new DatabaseHeaderOrchestrationService());
    private readonly PageDatabaseContext state;
    private readonly IPageDatabaseContextCoordinationService boundService;
    private PageDatabaseExposure(PageDatabaseContext pageDatabaseContext)
    {
        state = pageDatabaseContext;
        boundService = service;
    }

    public static async Task<PageDatabaseExposure> CreatePageDatabaseExposureAsync(string path, int pageSize = PageConstants.DefaultSize, string defaultCollation = "SQL_Latin1_General_CP1_CI_AS", CancellationToken cancellationToken = default) =>
        new PageDatabaseExposure(pageDatabaseContext: await service.CreatePageDatabaseContextAsync(pageDatabaseContext: new PageDatabaseContext { Path = path, PageSize = pageSize, DefaultCollation = defaultCollation, OpenMode = DatabaseOpenMode.Writer }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));

    public static async Task<PageDatabaseExposure> OpenAsync(string path, CancellationToken cancellationToken = default) =>
        new PageDatabaseExposure(pageDatabaseContext: await service.OpenPageDatabaseContextAsync(pageDatabaseContext: new PageDatabaseContext { Path = path, OpenMode = DatabaseOpenMode.Writer }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));

    public static async Task<PageDatabaseExposure> OpenAsync(string path, DatabaseOpenMode openMode, CancellationToken cancellationToken = default) =>
        new PageDatabaseExposure(pageDatabaseContext: await service.OpenPageDatabaseContextAsync(pageDatabaseContext: new PageDatabaseContext { Path = path, OpenMode = openMode }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));

    public int GetPageSize() =>
        boundService.ReadPageDatabaseContext(pageDatabaseContext: state)
            .Runtime!.Session!.PageSize;

    public DatabaseHeader ReadHeader() =>
        boundService.ReadPageDatabaseContext(pageDatabaseContext: state)
            .Runtime!.Header!;

    internal string ReadDatabasePath() =>
        boundService.ReadPageDatabaseContext(pageDatabaseContext: state)
            .Runtime!.Session!.Path;

    internal void WriteMaximumAllocatedBytes(long? maximumAllocatedBytes) =>
        boundService.SetPageDatabaseContextMaximumLength(pageDatabaseContext: state, maximumAllocatedBytes: maximumAllocatedBytes);

    internal void RestoreLength(long length) =>
        boundService.RestorePageDatabaseContextLength(pageDatabaseContext: state, length: length);

    public ValueTask ReadAsync(PageId pageId, Memory<byte> destination, CancellationToken cancellationToken = default) =>
        boundService.ReadPageDatabaseContextAsync(pageDatabaseContext: state, pageId: pageId.Value, destination: destination, cancellationToken: cancellationToken);

    public ValueTask WriteAsync(PageId pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) =>
        boundService.WritePageDatabaseContextAsync(pageDatabaseContext: state, pageId: pageId.Value, source: source, cancellationToken: cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
        boundService.FlushPageDatabaseContextAsync(pageDatabaseContext: state, cancellationToken: cancellationToken);

    internal ValueTask ReloadHeaderAsync(CancellationToken cancellationToken = default) =>
        boundService.ReloadPageDatabaseContextHeaderAsync(pageDatabaseContext: state, cancellationToken: cancellationToken);

    public async ValueTask<PageId> AllocateAsync(PageType pageType, CancellationToken cancellationToken = default) =>
        new PageId
    {
        Value = (await boundService.AllocatePageDatabaseContextAsync(pageDatabaseContext: state, pageType: pageType, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).ResultPageId
    };

    public ValueTask FreeAsync(PageId pageId, CancellationToken cancellationToken = default) =>
        boundService.FreePageDatabaseContextAsync(pageDatabaseContext: state, pageId: pageId.Value, cancellationToken: cancellationToken);

    internal ValueTask PublishCatalogRootAsync(PageId pageId, CancellationToken cancellationToken = default) =>
        boundService.PublishPageDatabaseContextCatalogAsync(pageDatabaseContext: state, pageId: pageId.Value, cancellationToken: cancellationToken);

    internal async ValueTask<SqlValue> AllocateRowVersionAsync(CancellationToken cancellationToken = default) =>
        (await boundService.AllocatePageDatabaseContextRowVersionAsync(pageDatabaseContext: state, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).ResultValue!;

    internal async ValueTask<SqlValue> AllocateGeneratedValueAsync(CatalogGeneratedAlwaysKind kind, CancellationToken cancellationToken = default) =>
        (await boundService.AllocatePageDatabaseContextGeneratedValueAsync(pageDatabaseContext: state, kind: kind, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).ResultValue!;

    public ValueTask DisposeAsync() =>
        boundService.ClosePageDatabaseContextAsync(pageDatabaseContext: state);

    public PageDatabaseBinding ReadPageDatabaseBinding() =>
        new PageDatabaseBinding
    {
        Service = boundService,
        Context = state
    };
    private PageDatabaseExposure(PageDatabaseBinding pageDatabaseBinding)
    {
        state = pageDatabaseBinding.Context!;
        boundService = (IPageDatabaseContextCoordinationService)pageDatabaseBinding.Service!;
    }

    internal static PageDatabaseExposure BindPageDatabaseExposure(PageDatabaseBinding pageDatabaseBinding) =>
        new(pageDatabaseBinding: pageDatabaseBinding);
}