// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.Services.Orchestrations.PageDatabaseFiles;
internal sealed partial class PageDatabaseContextFileOrchestrationService
{
    private static void ValidateSession(PageDatabaseContext pageDatabaseContext)
    {
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext);
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext.Runtime);
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext.Runtime.Session);
    }

    private static void ValidateSessionCancellation(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken)
    {
        ValidateSession(pageDatabaseContext: pageDatabaseContext);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateRead(PageDatabaseContext pageDatabaseContext, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken)
    {
        ValidateSessionCancellation(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        if (destination.Length != pageDatabaseContext.Runtime!.Session!.PageSize)
        {
            throw new ArgumentException(message: "Reads require a complete page.");
        }

        if (pageId > (ulong)(long.MaxValue / pageDatabaseContext.Runtime.Session.PageSize))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(pageId));
        }
    }

    private static void ValidateWrite(PageDatabaseContext pageDatabaseContext, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        ValidateSessionCancellation(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        if (source.Length != pageDatabaseContext.Runtime!.Session!.PageSize)
        {
            throw new ArgumentException(message: "Writes require a complete page.");
        }

        if (pageId > (ulong)(long.MaxValue / pageDatabaseContext.Runtime.Session.PageSize))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(pageId));
        }
    }

    private static void ValidateLength(PageDatabaseContext pageDatabaseContext, long length)
    {
        ValidateSession(pageDatabaseContext: pageDatabaseContext);

        if (length < 0 || length % pageDatabaseContext.Runtime!.Session!.PageSize != 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(length));
        }
    }

    private static void ValidateCreation(PageDatabaseContext pageDatabaseContext)
    {
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: pageDatabaseContext.Path);

        if (!Enum.IsDefined(value: (PageSize)pageDatabaseContext.PageSize))
        {
            throw new ArgumentOutOfRangeException(paramName: "pageSize");
        }
    }

    private static void ValidateOpen(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: pageDatabaseContext.Path);

        if (!Enum.IsDefined(value: pageDatabaseContext.OpenMode))
        {
            throw new ArgumentOutOfRangeException(paramName: "openMode");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidatePublicationPath(PageDatabaseContext pageDatabaseContext)
    {
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: pageDatabaseContext.Path);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: pageDatabaseContext.TemporaryPath);
    }

    private static void ValidatePublication(PageDatabaseContext pageDatabaseContext, byte[] page, CancellationToken cancellationToken)
    {
        ValidatePublicationPath(pageDatabaseContext: pageDatabaseContext);
        ArgumentNullException.ThrowIfNull(argument: page);

        if (page.Length != pageDatabaseContext.PageSize)
        {
            throw new ArgumentException(message: "Database publication requires a complete page.");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}