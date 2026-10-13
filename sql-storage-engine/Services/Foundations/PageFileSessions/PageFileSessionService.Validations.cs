// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.Services.Foundations.PageFileSessions;
internal sealed partial class PageFileSessionService
{
    private static void ValidateCreation(PageFileSession pageFileSession)
    {
        ArgumentNullException.ThrowIfNull(argument: pageFileSession);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: pageFileSession.Path);

        if (!Enum.IsDefined(value: (PageSize)pageFileSession.PageSize) || pageFileSession.SessionId != Guid.Empty || pageFileSession.ReadOnly && pageFileSession.CreateNew)
        {
            throw new ArgumentException(message: "Invalid page-file session configuration.");
        }
    }

    private static void ValidateSession(PageFileSession pageFileSession)
    {
        ArgumentNullException.ThrowIfNull(argument: pageFileSession);

        if (pageFileSession.SessionId == Guid.Empty || !Enum.IsDefined(value: (PageSize)pageFileSession.PageSize))
        {
            throw new ArgumentException(message: "Invalid page-file session.");
        }
    }

    private static void ValidateOffset(PageFileSession pageFileSession, ulong pageId, CancellationToken cancellationToken)
    {
        ValidateSession(pageFileSession: pageFileSession);

        if (pageId > (ulong)(long.MaxValue / pageFileSession.PageSize))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(pageId));
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateRead(PageFileSession pageFileSession, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken)
    {
        ValidateOffset(pageFileSession: pageFileSession, pageId: pageId, cancellationToken: cancellationToken);

        if (destination.Length != pageFileSession.PageSize)
        {
            throw new ArgumentException(message: "Reads require a complete page.", paramName: nameof(destination));
        }
    }

    private static void ValidateWrite(PageFileSession pageFileSession, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        ValidateOffset(pageFileSession: pageFileSession, pageId: pageId, cancellationToken: cancellationToken);

        if (pageFileSession.ReadOnly)
        {
            throw new InvalidOperationException(message: "Read-only databases cannot be modified.");
        }

        if (source.Length != pageFileSession.PageSize)
        {
            throw new ArgumentException(message: "Writes require a complete page.", paramName: nameof(source));
        }
    }

    private static void ValidateFlush(PageFileSession pageFileSession, CancellationToken cancellationToken)
    {
        ValidateSession(pageFileSession: pageFileSession);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateLength(PageFileSession pageFileSession, long length)
    {
        ValidateSession(pageFileSession: pageFileSession);

        if (pageFileSession.ReadOnly)
        {
            throw new InvalidOperationException(message: "Read-only databases cannot be modified.");
        }

        if (length < 0 || length % pageFileSession.PageSize != 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(length));
        }
    }
}