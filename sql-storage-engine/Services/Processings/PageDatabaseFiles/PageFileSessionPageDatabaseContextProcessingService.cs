// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Foundations.PageFileSessions;

namespace sql_storage_engine.Services.Processings.PageDatabaseFiles;
internal sealed partial class PageFileSessionPageDatabaseContextProcessingService(PageFileSessionService files) : IPageFileSessionPageDatabaseContextProcessingService
{
    public PageDatabaseContext OpenPageDatabaseContextFile(PageDatabaseContext pageDatabaseContext, bool createNew) =>
        TryCatch(operation: () =>
    {
        ValidateOpen(pageDatabaseContext: pageDatabaseContext, createNew: createNew);
        var session = files.CreatePageFileSession(pageFileSession: new PageFileSession { Path = pageDatabaseContext.Path!, PageSize = pageDatabaseContext.PageSize, ReadOnly = pageDatabaseContext.OpenMode == DatabaseOpenMode.ReadOnly, CreateNew = createNew });

        return pageDatabaseContext with
        {
            Runtime = new PageDatabaseRuntime
            {
                Session = session,
                OpenMode = pageDatabaseContext.OpenMode
            }
        };
    });

    public ValueTask ReadPageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRead(pageDatabaseContext: pageDatabaseContext, pageId: pageId, destination: destination, cancellationToken: cancellationToken);

        await files.ReadPageFileSessionAsync(pageFileSession: pageDatabaseContext.Runtime!.Session!, pageId: pageId, destination: destination, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask WritePageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(pageDatabaseContext: pageDatabaseContext, pageId: pageId, source: source, cancellationToken: cancellationToken);

        await files.WritePageFileSessionAsync(pageFileSession: pageDatabaseContext.Runtime!.Session!, pageId: pageId, source: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FlushPageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateSessionCancellation(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        await files.FlushPageFileSessionAsync(pageFileSession: pageDatabaseContext.Runtime!.Session!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask ClosePageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext) =>
        TryCatch(operation: async () =>
    {
        ValidateSession(pageDatabaseContext: pageDatabaseContext);

        await files.ClosePageFileSessionAsync(pageFileSession: pageDatabaseContext.Runtime!.Session!)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void RestorePageDatabaseContextFileLength(PageDatabaseContext pageDatabaseContext, long length) =>
        TryCatch(operation: () =>
    {
        ValidateLength(pageDatabaseContext: pageDatabaseContext, length: length);
        files.RestorePageFileSessionLength(pageFileSession: pageDatabaseContext.Runtime!.Session!, length: length);
    });

    public ValueTask EnterPageDatabaseContextFileAsync(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateSessionCancellation(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        await files.EnterPageFileSessionAsync(pageFileSession: pageDatabaseContext.Runtime!.Session!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void ExitPageDatabaseContextFile(PageDatabaseContext pageDatabaseContext) =>
        TryCatch(operation: () =>
    {
        ValidateSession(pageDatabaseContext: pageDatabaseContext);
        files.ExitPageFileSession(pageFileSession: pageDatabaseContext.Runtime!.Session!);
    });

    internal static PageFileSessionPageDatabaseContextProcessingService CreatePageFileSessionPageDatabaseContextProcessingService() =>
        new PageFileSessionPageDatabaseContextProcessingService(files: sql_storage_engine.Services.Foundations.PageFileSessions.PageFileSessionService.CreatePageFileSessionService());
}