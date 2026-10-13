// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Brokers.OverflowPages;

namespace sql_storage_engine.Services.Foundations.OverflowPages;
internal sealed partial class OverflowPageResourceService : IOverflowPageResourceService
{
    private readonly OverflowPageResourceBroker broker;
    public OverflowPageResourceService(OverflowPageResourceConfiguration overflowPageResourceConfiguration)
    {
        broker = OpenOverflowPageResourceBroker(overflowPageResourceConfiguration: overflowPageResourceConfiguration);
    }

    private static OverflowPageResourceBroker OpenOverflowPageResourceBroker(OverflowPageResourceConfiguration overflowPageResourceConfiguration) =>
        TryCatch(operation: () =>
    {
        ValidateConfiguration(overflowPageResourceConfiguration: overflowPageResourceConfiguration);
        return new OverflowPageResourceBroker(overflowPageResourceConfiguration: overflowPageResourceConfiguration);
    });

    public int ReadOverflowPageResourceSize() =>
        TryCatch(operation: () =>
    {
        ValidateResource();
        return broker.ReadPageSize();
    });

    public ValueTask<byte[]> ReadOverflowPageResourceAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        return await broker.ReadAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask WriteOverflowPageResourceAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(pageId: pageId, source: source, cancellationToken: cancellationToken);

        await broker.WriteAsync(pageId: pageId, source: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<ulong> AllocateOverflowPageResourceAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(cancellationToken: cancellationToken);

        return await broker.AllocateAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask DiscardOverflowPageResourceAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        await broker.DiscardAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FreeOverflowPageResourceAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        await broker.FreeAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}