// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;
using sql_storage_engine.Catalog;

namespace sql_storage_engine.Services.Coordinations.PageDatabases;
internal sealed partial class PageDatabaseContextCoordinationService
{
    private static void ValidateState(PageDatabaseContext pageDatabaseContext)
    {
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext);
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext.Runtime);
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext.Runtime.Session);
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext.Runtime.Header);
    }

    private static void ValidateRuntime(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken)
    {
        ValidateState(pageDatabaseContext: pageDatabaseContext);
        ObjectDisposedException.ThrowIf(condition: pageDatabaseContext.Runtime!.Disposed, instance: pageDatabaseContext);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateCreation(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: pageDatabaseContext.Path);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: pageDatabaseContext.DefaultCollation);

        if (!Enum.IsDefined(value: (PageSize)pageDatabaseContext.PageSize))
        {
            throw new ArgumentOutOfRangeException(paramName: "pageSize");
        }

        cancellationToken.ThrowIfCancellationRequested();
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

    private static void ValidateMutation(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken)
    {
        ValidateRuntime(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        if (pageDatabaseContext.Runtime!.OpenMode == DatabaseOpenMode.ReadOnly)
        {
            throw new InvalidOperationException(message: "Read-only databases cannot be modified.");
        }
    }

    private static void ValidateRead(PageDatabaseContext pageDatabaseContext, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken)
    {
        ValidateRuntime(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);
        ValidateOffset(pageDatabaseContext: pageDatabaseContext, pageId: pageId);

        if (destination.Length != pageDatabaseContext.Runtime!.Session!.PageSize)
        {
            throw new ArgumentException(message: "Reads require a complete page.");
        }
    }

    private static void ValidateWrite(PageDatabaseContext pageDatabaseContext, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        ValidateMutation(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);
        ValidateOffset(pageDatabaseContext: pageDatabaseContext, pageId: pageId);

        if (source.Length != pageDatabaseContext.Runtime!.Session!.PageSize)
        {
            throw new ArgumentException(message: "Writes require a complete page.");
        }
    }

    private static void ValidateOffset(PageDatabaseContext pageDatabaseContext, ulong pageId)
    {
        if (pageId > (ulong)(long.MaxValue / pageDatabaseContext.Runtime!.Session!.PageSize))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(pageId));
        }
    }

    private static void ValidateAllocation(PageDatabaseContext pageDatabaseContext, PageType pageType, CancellationToken cancellationToken)
    {
        ValidateMutation(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        if (pageType is PageType.Unknown or PageType.DatabaseHeader or PageType.Free || !Enum.IsDefined(value: pageType))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(pageType));
        }
    }

    private static void ValidateRelease(PageDatabaseContext pageDatabaseContext, ulong pageId, CancellationToken cancellationToken)
    {
        ValidateMutation(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        if (pageId == 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(pageId));
        }
    }

    private static void ValidateGeneratedValue(PageDatabaseContext pageDatabaseContext, CatalogGeneratedAlwaysKind kind, CancellationToken cancellationToken)
    {
        ValidateMutation(pageDatabaseContext: pageDatabaseContext, cancellationToken: cancellationToken);

        if (kind == CatalogGeneratedAlwaysKind.None || !Enum.IsDefined(value: kind))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(kind));
        }
    }

    private static void ValidateMaximumLength(PageDatabaseContext pageDatabaseContext, long? maximumAllocatedBytes)
    {
        ValidateState(pageDatabaseContext: pageDatabaseContext);

        if (maximumAllocatedBytes < 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(maximumAllocatedBytes));
        }
    }

    private static void ValidateLength(PageDatabaseContext pageDatabaseContext, long length)
    {
        ValidateMutation(pageDatabaseContext: pageDatabaseContext, cancellationToken: CancellationToken.None);

        if (length < 0 || length % pageDatabaseContext.Runtime!.Session!.PageSize != 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(length));
        }
    }

    private static void ValidatePageInspection(PageDatabaseContext pageDatabaseContext, ulong pageId, ReadOnlyMemory<byte> source)
    {
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext);
    }
}