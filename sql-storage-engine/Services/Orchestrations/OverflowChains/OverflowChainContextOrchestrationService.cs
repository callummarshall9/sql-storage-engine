// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Processings.OverflowPages;
using sql_storage_engine.Services.Processings.OverflowPageHeaders;
using sql_storage_engine.Services.Processings.OverflowChainReferences;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.OverflowChains;
internal sealed partial class OverflowChainContextOrchestrationService : IOverflowChainContextOrchestrationService
{
    private readonly OverflowPageResourceProcessingService pages;
    private readonly PageHeaderOverflowPageHeaderProcessingService headers;
    private readonly OverflowReferenceProcessingService references;
    private OverflowChainContextOrchestrationService(OverflowPageResourceProcessingService pages, PageHeaderOverflowPageHeaderProcessingService headers, OverflowReferenceProcessingService references)
    {
        this.pages = pages;
        this.headers = headers;
        this.references = references;
    }

    internal static OverflowChainContextOrchestrationService CreateOverflowChainContextOrchestrationService(OverflowPageResourceConfiguration overflowPageResourceConfiguration) =>
        new OverflowChainContextOrchestrationService(pages: OverflowPageResourceProcessingService.CreateOverflowPageResourceProcessingService(overflowPageResourceConfiguration: overflowPageResourceConfiguration), headers: new PageHeaderOverflowPageHeaderProcessingService(), references: OverflowReferenceProcessingService.CreateOverflowReferenceProcessingService());

    public ValueTask<OverflowChainContext> WriteOverflowChainContextAsync(OverflowChainContext overflowChainContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(overflowChainContext: overflowChainContext, cancellationToken: cancellationToken);

        var reference = await WriteCoreAsync(value: overflowChainContext.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return overflowChainContext with
        {
            Reference = reference
        };
    });

    public ValueTask<OverflowChainContext> ReadOverflowChainContextAsync(OverflowChainContext overflowChainContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRead(overflowChainContext: overflowChainContext, cancellationToken: cancellationToken);

        var value = await ReadCoreAsync(overflowReference: overflowChainContext.Reference!.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return overflowChainContext with
        {
            Value = value
        };
    });

    public ValueTask FreeOverflowChainContextAsync(OverflowChainContext overflowChainContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRead(overflowChainContext: overflowChainContext, cancellationToken: cancellationToken);

        await FreeCoreAsync(overflowReference: overflowChainContext.Reference!.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    private async ValueTask<OverflowReference> WriteCoreAsync(ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException("Overflow values cannot be empty.", nameof(value));
        }

        if (value.Length > OverflowReferenceFormat.MaximumValueLength)
        {
            throw new ArgumentException("Overflow value exceeds the maximum length.", nameof(value));
        }

        var capacity = headers.CalculateOverflowPageHeaderCapacity(pageSize: pages.ReadOverflowPageResourceSize());
        var pageCount = checked((value.Length + capacity - 1) / capacity);

        if (pageCount > OverflowReferenceFormat.MaximumChainLength)
        {
            throw new ArgumentException("Overflow value exceeds the maximum chain length.", nameof(value));
        }

        List<PageId> allocated = [];

        try
        {
            for (var index = 0; index < pageCount; index++)
            {
                allocated.Add(item: new PageId { Value = await pages.AllocateOverflowPageResourceAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) });
            }

            for (var index = 0; index < allocated.Count; index++)
            {
                var offset = checked(index * capacity);
                var length = Math.Min(val1: capacity, val2: value.Length - offset);
                var image = new byte[pages.ReadOverflowPageResourceSize()];
                headers.WriteOverflowPageHeader(page: image, pageId: allocated[index].Value, nextPageId: index + 1 < allocated.Count ? allocated[index + 1].Value : null, payload: value.Span.Slice(start: offset, length: length));

                await pages.WriteOverflowPageResourceAsync(pageId: allocated[index].Value, source: image, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            return new OverflowReference
            {
                FirstPageId = allocated[0],
                TotalLength = value.Length
            };
        }
        catch (Exception writeException)
        {
            try
            {
                await RollbackAsync(allocated: allocated)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (Exception cleanupException)
            {
                throw new OverflowWriteException("Overflow write failed and allocated-page cleanup was incomplete.", allocated.ToArray(), new AggregateException(writeException, cleanupException));
            }

            throw;
        }
    }

    private async ValueTask<ReadOnlyMemory<byte>> ReadCoreAsync(OverflowReference overflowReference, CancellationToken cancellationToken = default)
    {
        references.CheckOverflowReference(overflowReference: overflowReference);
        var output = new byte[checked((int)overflowReference.TotalLength)];

        await ValidateChainAsync(overflowReference: overflowReference, output: output, pages: null, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return output;
    }

    private async ValueTask ValidateChainAsync(OverflowReference overflowReference, byte[]? output, List<PageId>? pages, CancellationToken cancellationToken)
    {
        references.CheckOverflowReference(overflowReference: overflowReference);
        var written = 0;
        var total = checked((int)overflowReference.TotalLength);
        var current = overflowReference.FirstPageId;
        HashSet<PageId> visited = [];

        for (var pageNumber = 0; pageNumber < OverflowReferenceFormat.MaximumChainLength; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!visited.Add(item: current))
            {
                throw new StorageCorruptionException($"Cycle detected in overflow chain at {current}.");
            }

            OverflowPageHeader header;

            try
            {
                var image = await this.pages.ReadOverflowPageResourceAsync(pageId: current.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                header = headers.ReadOverflowPageHeader(page: image, expectedPageId: current.Value);
                var length = checked((int)header.UsedLength);

                if (length > total - written)
                {
                    throw new StorageCorruptionException("Overflow chain is longer than its reference length.");
                }

                if (output is not null)
                {
                    image.AsSpan()
            .Slice(start: sql_storage_engine.Models.Overflow.OverflowPageFormat.HeaderLength, length: length)
            .CopyTo(destination: output.AsSpan(start: written));
                }

                written = checked(written + length);
            }
            catch (StorageResourceException exception)
            {
                throw new StorageCorruptionException($"Overflow chain references inaccessible {current}.", exception);
            }

            pages?.Add(item: current);

            if (written == total)
            {
                if (header.NextPageId is not null)
                {
                    throw new StorageCorruptionException("Overflow chain continues beyond its reference length.");
                }

                return;
            }

            if (header.NextPageId is not { } next)
            {
                throw new StorageCorruptionException("Overflow chain is truncated.");
            }

            current = next;
        }

        throw new StorageCorruptionException($"Overflow chain exceeds {OverflowReferenceFormat.MaximumChainLength} pages.");
    }

    private async ValueTask FreeCoreAsync(OverflowReference overflowReference, CancellationToken cancellationToken = default)
    {
        List<PageId> pages = [];

        await ValidateChainAsync(overflowReference: overflowReference, output: null, pages: pages, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        for (var index = pages.Count - 1; index >= 0; index--)
        {
            await this.pages.DiscardOverflowPageResourceAsync(pageId: pages[index].Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await this.pages.FreeOverflowPageResourceAsync(pageId: pages[index].Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private async ValueTask RollbackAsync(IReadOnlyList<PageId> allocated)
    {
        for (var index = allocated.Count - 1; index >= 0; index--)
        {
            await pages.DiscardOverflowPageResourceAsync(pageId: allocated[index].Value, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

            await pages.FreeOverflowPageResourceAsync(pageId: allocated[index].Value, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
    }
}