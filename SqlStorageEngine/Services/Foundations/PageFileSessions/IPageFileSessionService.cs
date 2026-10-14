// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.Services.Foundations.PageFileSessions;
internal interface IPageFileSessionService
{
    PageFileSession CreatePageFileSession(PageFileSession pageFileSession);

    ValueTask ReadPageFileSessionAsync(PageFileSession pageFileSession, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken);

    ValueTask WritePageFileSessionAsync(PageFileSession pageFileSession, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask FlushPageFileSessionAsync(PageFileSession pageFileSession, CancellationToken cancellationToken);

    void RestorePageFileSessionLength(PageFileSession pageFileSession, long length);

    ValueTask ClosePageFileSessionAsync(PageFileSession pageFileSession);

    ValueTask EnterPageFileSessionAsync(PageFileSession pageFileSession, CancellationToken cancellationToken);

    void ExitPageFileSession(PageFileSession pageFileSession);
}