// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using System.Buffers;
using System.Buffers.Binary;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Pages;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.CatalogPages;
/// <summary>Persists and traverses a bounded chain of bootstrap catalog pages.</summary>
internal sealed partial class CatalogPageChainOrchestrationService(sql_storage_engine.Services.Foundations.CatalogPages.CatalogPageResourceService resources, sql_storage_engine.Services.Foundations.PageHeaders.PageHeaderService headers) : ICatalogPageChainOrchestrationService
{
    internal static CatalogPageChainOrchestrationService CreateCatalogPageChainOrchestrationService(CatalogPageResourceConfiguration configuration) =>
        new CatalogPageChainOrchestrationService(resources: sql_storage_engine.Services.Foundations.CatalogPages.CatalogPageResourceService.CreateCatalogPageResourceService(configuration: configuration), headers: new sql_storage_engine.Services.Foundations.PageHeaders.PageHeaderService());

    public ValueTask<CatalogPageChainRequest> ExecuteCatalogPageChainRequestAsync(CatalogPageChainRequest catalogPageChainRequest, CancellationToken token) =>
        TryCatch(operation: async () =>
    {
        ValidateRequest(catalogPageChainRequest: catalogPageChainRequest, token: token);

        if (catalogPageChainRequest.IsWrite)
        {
            var result = await WriteCoreAsync(catalogPayload: catalogPageChainRequest.Payload!, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);

            catalogPageChainRequest.RootPageId = result.RootPageId;
            catalogPageChainRequest.PageIds = result.PageIds;
        }
        else
        {
            catalogPageChainRequest.Payload = await ReadCoreAsync(rootPageId: catalogPageChainRequest.RootPageId, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        return catalogPageChainRequest;
    });

    private async ValueTask<PageId> AllocateAsync(CancellationToken cancellationToken) =>
        new PageId
    {
        Value = await resources.AllocateCatalogPageAsync(pageType: (ushort)PageType.Catalog, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
    };
    private const int ChainHeaderLength = sql_storage_engine.Models.Pages.PageHeaderFormat.EncodedLength + 16;
    private const int MaximumPages = 65_536;
    private async ValueTask<(PageId RootPageId, IReadOnlyList<PageId> PageIds)> WriteCoreAsync(byte[] catalogPayload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogPayload);
        var encoded = catalogPayload;
        var capacity = resources.ReadCatalogPageSize() - ChainHeaderLength;

        if (capacity <= 0)
        {
            throw new StorageResourceExhaustedException(message: "Page size cannot contain a catalog payload.");
        }

        var pageCount = Math.Max(val1: 1, val2: checked((encoded.Length + capacity - 1) / capacity));

        if (pageCount > MaximumPages)
        {
            throw new StorageResourceExhaustedException(message: "Catalog exceeds the page traversal bound.");
        }

        List<PageId> ids = new(capacity: pageCount);

        try
        {
            for (var index = 0; index < pageCount; index++)
            {
                ids.Add(item: await AllocateAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));
            }

            for (var index = 0; index < ids.Count; index++)
            {
                var page = new byte[resources.ReadCatalogPageSize()];
                headers.WritePageHeader(destination: page, pageHeader: new PageHeader { PageId = ids[index], PageType = PageType.Catalog, FormatVersion = new sql_storage_engine.Models.Pages.PageFormatVersion { Value = sql_storage_engine.Models.Pages.PageHeaderFormat.CurrentVersion }, PageLogSequenceNumber = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 0 }, ChecksumAlgorithm = PageChecksumAlgorithm.Crc32, Checksum = 0 });
                var hasNext = index + 1 < ids.Count;
                page[32] = hasNext ? (byte)1 : (byte)0;

                if (hasNext)
                {
                    BinaryPrimitives.WriteUInt64LittleEndian(destination: page.AsSpan(start: 36), value: ids[index + 1].Value);
                }

                var sourceOffset = checked(index * capacity);
                var length = Math.Min(val1: capacity, val2: encoded.Length - sourceOffset);
                BinaryPrimitives.WriteUInt32LittleEndian(destination: page.AsSpan(start: 44), value: checked((uint)length));

                encoded.AsSpan(start: sourceOffset, length: length)
            .CopyTo(destination: page.AsSpan(start: ChainHeaderLength));

                headers.WriteChecksum(page: page, pageSize: resources.ReadCatalogPageSize());

                await resources.WriteCatalogPageBufferAsync(catalogPageBuffer: new CatalogPageBuffer { PageId = ids[index].Value, Bytes = page }, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            return (ids[0], ids.AsReadOnly());
        }
        catch
        {
            for (var index = ids.Count - 1; index >= 0; index--)
            {
                await resources.FreeCatalogPageAsync(pageId: ids[index].Value, token: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            throw;
        }
    }

    private async ValueTask<byte[]> ReadCoreAsync(PageId rootPageId, CancellationToken cancellationToken = default)
    {
        var bytes = new ArrayBufferWriter<byte>();
        HashSet<PageId> seen = [];
        PageId? current = rootPageId;

        while (current is { } pageId)
        {
            if (seen.Count >= MaximumPages)
            {
                throw new StorageCorruptionException(message: "Catalog page chain exceeds its traversal bound.");
            }

            if (!seen.Add(item: pageId))
            {
                throw new StorageCorruptionException(message: $"Cycle detected in catalog page chain at {pageId}.");
            }

            var page = new byte[resources.ReadCatalogPageSize()];

            await resources.ReadCatalogPageBufferAsync(catalogPageBuffer: new CatalogPageBuffer { PageId = pageId.Value, Bytes = page }, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            headers.ValidateChecksum(page: page, pageSize: resources.ReadCatalogPageSize());
            headers.CheckPageHeader(pageHeader: headers.ReadPageHeader(source: page), expectedPageId: (pageId).Value, expectedType: PageType.Catalog);

            if (page.AsSpan(start: 33, length: 3)
            .IndexOfAnyExcept(value: (byte)0) >= 0)
            {
                throw new StorageFormatException(message: "Reserved catalog-page bytes must be zero.");
            }

            var hasNext = page[32] switch
            {
                0 => false,
                1 => true,
                _ => throw new StorageFormatException(message: "Invalid catalog-page link flag.")};

            var nextValue = BinaryPrimitives.ReadUInt64LittleEndian(source: page.AsSpan(start: 36));

            if (!hasNext && nextValue != 0)
            {
                throw new StorageFormatException(message: "Terminal catalog page has nonzero link bytes.");
            }

            var length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source: page.AsSpan(start: 44)));

            if (length > resources.ReadCatalogPageSize() - ChainHeaderLength)
            {
                throw new StorageFormatException(message: "Catalog page payload length is invalid.");
            }

            var target = bytes.GetSpan(sizeHint: length);

            page.AsSpan(start: ChainHeaderLength, length: length)
            .CopyTo(destination: target);

            bytes.Advance(count: length);

            current = hasNext ? new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = nextValue
            }

            : null;
        }

        return bytes.WrittenSpan.ToArray();
    }
}