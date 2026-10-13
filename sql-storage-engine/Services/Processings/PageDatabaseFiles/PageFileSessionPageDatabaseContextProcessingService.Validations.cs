// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;

namespace sql_storage_engine.Services.Processings.PageDatabaseFiles;
internal sealed partial class PageFileSessionPageDatabaseContextProcessingService
{
    private static void ValidateOpen(PageDatabaseContext pageDatabaseContext, bool createNew)
    {
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: pageDatabaseContext.Path);

        if (!Enum.IsDefined(value: pageDatabaseContext.OpenMode) || !Enum.IsDefined(value: (PageSize)pageDatabaseContext.PageSize) || createNew && pageDatabaseContext.OpenMode == DatabaseOpenMode.ReadOnly)
        {
            throw new ArgumentException(message: "Invalid database file configuration.");
        }
    }

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
}