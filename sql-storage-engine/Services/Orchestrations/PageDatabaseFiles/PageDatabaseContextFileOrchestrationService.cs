// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Processings.PageDatabaseFiles;
using sql_storage_engine.Services.Processings.DatabaseFileProbes;
using sql_storage_engine.Services.Processings.StatementJournals;

namespace sql_storage_engine.Services.Orchestrations.PageDatabaseFiles;
internal sealed partial class PageDatabaseContextFileOrchestrationService(PageFileSessionPageDatabaseContextProcessingService files, TransactionDirectoryDatabaseFileProcessingService pathsProcessing, TransactionDirectoryStatementJournalProcessingService journals) : IPageDatabaseContextFileOrchestrationService
{
    public PageDatabaseContext PreparePageDatabaseContextPublication(PageDatabaseContext pageDatabaseContext) =>
        TryCatch(operation: () =>
    {
        ValidateCreation(pageDatabaseContext: pageDatabaseContext);
        var paths = pathsProcessing.PrepareTransactionDirectoryDatabasePublication(path: pageDatabaseContext.Path!);

        return pageDatabaseContext with
        {
            Path = paths.FullPath,
            TemporaryPath = paths.TemporaryPath,
            DatabaseIdentity = paths.DatabaseIdentity
        };
    });

    public ValueTask<PageDatabaseContext> OpenPageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateOpen(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);
        var fullPath = pathsProcessing.NormalizeTransactionDirectoryDatabasePath(path: pageDatabaseContext.Path!);

        await journals.RecoverStatementJournalStateAsync(databasePath: fullPath, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var size = await pathsProcessing.ReadTransactionDirectoryDatabasePageSizeAsync(path: fullPath, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return files.OpenPageDatabaseContextFile(pageDatabaseContext: pageDatabaseContext with { Path = fullPath, PageSize = size }, createNew: false);
    });

    public ValueTask PublishPageDatabaseContextAsync(PageDatabaseContext pageDatabaseContext, byte[] page, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePublication(pageDatabaseContext: pageDatabaseContext, page: page, cancellationToken: cancellationToken);
        var temporary = files.OpenPageDatabaseContextFile(pageDatabaseContext: pageDatabaseContext with { Path = pageDatabaseContext.TemporaryPath, OpenMode = DatabaseOpenMode.Writer }, createNew: true);

        try
        {
            await files.WritePageDatabaseContextFileAsync(pageDatabaseContext: temporary, pageId: 0, source: page, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await files.FlushPageDatabaseContextFileAsync(pageDatabaseContext: temporary, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            await files.ClosePageDatabaseContextFileAsync(pageDatabaseContext: temporary)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        pathsProcessing.PublishTransactionDirectoryDatabaseFile(temporaryPath: pageDatabaseContext.TemporaryPath!, fullPath: pageDatabaseContext.Path!);
    });

    public void RemovePageDatabaseContextPublication(PageDatabaseContext pageDatabaseContext) =>
        TryCatch(operation: () =>
    {
        ValidatePublicationPath(pageDatabaseContext: pageDatabaseContext);
        pathsProcessing.RemoveTransactionDirectoryDatabaseTemporaryFile(path: pageDatabaseContext.TemporaryPath!);
    });

    public ValueTask ReadPageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRead(pageDatabaseContext: pageDatabaseContext, pageId: pageId, destination: destination, cancellationToken: cancellationToken);

        await files.ReadPageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, pageId: pageId, destination: destination, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask WritePageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(pageDatabaseContext: pageDatabaseContext, pageId: pageId, source: source, cancellationToken: cancellationToken);

        await files.WritePageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, pageId: pageId, source: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FlushPageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateSessionCancellation(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        await files.FlushPageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask ClosePageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext) =>
        TryCatch(operation: async () =>
    {
        ValidateSession(pageDatabaseContext: pageDatabaseContext);

        await files.ClosePageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void RestorePageDatabaseContextFileLength(PageDatabaseContext pageDatabaseContext, long length) =>
        TryCatch(operation: () =>
    {
        ValidateLength(pageDatabaseContext: pageDatabaseContext, length: length);
        files.RestorePageDatabaseContextFileLength(pageDatabaseContext: pageDatabaseContext, length: length);
    });

    public ValueTask EnterPageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateSessionCancellation(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        await files.EnterPageDatabaseContextFileAsync(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void ExitPageDatabaseContextFile(PageDatabaseContext pageDatabaseContext) =>
        TryCatch(operation: () =>
    {
        ValidateSession(pageDatabaseContext: pageDatabaseContext);
        files.ExitPageDatabaseContextFile(pageDatabaseContext: pageDatabaseContext);
    });

    internal static PageDatabaseContextFileOrchestrationService CreatePageDatabaseContextFileOrchestrationService() =>
        new PageDatabaseContextFileOrchestrationService(files: PageFileSessionPageDatabaseContextProcessingService.CreatePageFileSessionPageDatabaseContextProcessingService(), pathsProcessing: TransactionDirectoryDatabaseFileProcessingService.CreateTransactionDirectoryDatabaseFileProcessingService(), journals: TransactionDirectoryStatementJournalProcessingService.CreateTransactionDirectoryStatementJournalProcessingService());
}