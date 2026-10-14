// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;

namespace sql_storage_engine.Services.Orchestrations.PageDatabaseAllocations;
internal sealed partial class PageDatabaseContextAllocationOrchestrationService
{
    private static void ValidateRuntime(PageDatabaseContext pageDatabaseContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext);
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext.Runtime);
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext.Runtime.Session);
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext.Runtime.Header);
        ObjectDisposedException.ThrowIf(condition: pageDatabaseContext.Runtime.Disposed, instance: pageDatabaseContext);
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

    private static void ValidateCatalog(PageDatabaseContext pageDatabaseContext, ulong pageId, CancellationToken cancellationToken) =>
        ValidateRelease(pageDatabaseContext: pageDatabaseContext, pageId: pageId, cancellationToken: cancellationToken);

    private static void ValidatePageInspection(PageDatabaseContext pageDatabaseContext, ulong pageId, ReadOnlyMemory<byte> source)
    {
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext);
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext.Runtime);
        ArgumentNullException.ThrowIfNull(argument: pageDatabaseContext.Runtime.Session);

        if (source.Length != pageDatabaseContext.Runtime.Session.PageSize)
        {
            throw new ArgumentException(message: "Page image length must match its database page size.", paramName: nameof(source));
        }
    }
}