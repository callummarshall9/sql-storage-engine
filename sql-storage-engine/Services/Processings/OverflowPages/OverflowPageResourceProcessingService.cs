// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Services.Foundations.OverflowPages;

namespace sql_storage_engine.Services.Processings.OverflowPages;
internal sealed partial class OverflowPageResourceProcessingService(OverflowPageResourceService pages) : IOverflowPageResourceProcessingService
{
    internal static OverflowPageResourceProcessingService CreateOverflowPageResourceProcessingService(OverflowPageResourceConfiguration overflowPageResourceConfiguration) =>
        new OverflowPageResourceProcessingService(pages: new OverflowPageResourceService(overflowPageResourceConfiguration: overflowPageResourceConfiguration));

    public int ReadOverflowPageResourceSize() =>
        TryCatch(operation: () =>
    {
        ValidateResource();
        return pages.ReadOverflowPageResourceSize();
    });

    public ValueTask<byte[]> ReadOverflowPageResourceAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        return await pages.ReadOverflowPageResourceAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask WriteOverflowPageResourceAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(pageId: pageId, source: source, cancellationToken: cancellationToken);

        await pages.WriteOverflowPageResourceAsync(pageId: pageId, source: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<ulong> AllocateOverflowPageResourceAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(cancellationToken: cancellationToken);

        return await pages.AllocateOverflowPageResourceAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask DiscardOverflowPageResourceAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        await pages.DiscardOverflowPageResourceAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FreeOverflowPageResourceAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        await pages.FreeOverflowPageResourceAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}