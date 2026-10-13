// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Brokers.PageFileSessions;

namespace sql_storage_engine.Services.Foundations.PageFileSessions;
internal sealed partial class PageFileSessionService(PageFileSessionBroker broker) : IPageFileSessionService
{
    public PageFileSession CreatePageFileSession(PageFileSession pageFileSession) =>
        TryCatch(operation: () =>
    {
        ValidateCreation(pageFileSession: pageFileSession);

        return pageFileSession with
        {
            SessionId = broker.OpenFile(path: pageFileSession.Path, readOnly: pageFileSession.ReadOnly, createNew: pageFileSession.CreateNew)
        };
    });

    public ValueTask ReadPageFileSessionAsync(PageFileSession pageFileSession, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRead(pageFileSession: pageFileSession, pageId: pageId, destination: destination, cancellationToken: cancellationToken);

        await broker.ReadFileAsync(sessionId: pageFileSession.SessionId, offset: checked((long)pageId * pageFileSession.PageSize), destination: destination, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask WritePageFileSessionAsync(PageFileSession pageFileSession, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(pageFileSession: pageFileSession, pageId: pageId, source: source, cancellationToken: cancellationToken);

        await broker.WriteFileAsync(sessionId: pageFileSession.SessionId, offset: checked((long)pageId * pageFileSession.PageSize), source: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FlushPageFileSessionAsync(PageFileSession pageFileSession, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateFlush(pageFileSession: pageFileSession, cancellationToken: cancellationToken);

        await broker.FlushFileAsync(sessionId: pageFileSession.SessionId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void RestorePageFileSessionLength(PageFileSession pageFileSession, long length) =>
        TryCatch(operation: () =>
    {
        ValidateLength(pageFileSession: pageFileSession, length: length);
        broker.RestoreFileLength(sessionId: pageFileSession.SessionId, length: length);
    });

    public ValueTask ClosePageFileSessionAsync(PageFileSession pageFileSession) =>
        TryCatch(operation: async () =>
    {
        ValidateSession(pageFileSession: pageFileSession);

        await broker.CloseFileAsync(sessionId: pageFileSession.SessionId)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask EnterPageFileSessionAsync(PageFileSession pageFileSession, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateFlush(pageFileSession: pageFileSession, cancellationToken: cancellationToken);

        await broker.EnterFileGateAsync(sessionId: pageFileSession.SessionId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void ExitPageFileSession(PageFileSession pageFileSession) =>
        TryCatch(operation: () =>
    {
        ValidateSession(pageFileSession: pageFileSession);
        broker.ExitFileGate(sessionId: pageFileSession.SessionId);
    });

    internal static PageFileSessionService CreatePageFileSessionService() =>
        new PageFileSessionService(broker: new sql_storage_engine.Brokers.PageFileSessions.PageFileSessionBroker());
}