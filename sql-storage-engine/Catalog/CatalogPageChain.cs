using System.Buffers;
using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Catalog;

/// <summary>Persists and traverses a bounded chain of bootstrap catalog pages.</summary>
public sealed class CatalogPageChain(IPageStore pageStore, IPageAllocator allocator)
{
    public const int ChainHeaderLength = PageHeaderCodec.EncodedLength + 16;
    public const int MaximumPages = 65_536;

    public async ValueTask<(PageId RootPageId, IReadOnlyList<PageId> PageIds)> WriteAsync(
        CatalogDefinition catalog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var encoded = CatalogCodec.Encode(catalog);
        var capacity = pageStore.PageSize - ChainHeaderLength;
        if (capacity <= 0) throw new StorageResourceExhaustedException("Page size cannot contain a catalog payload.");
        var pageCount = Math.Max(1, checked((encoded.Length + capacity - 1) / capacity));
        if (pageCount > MaximumPages) throw new StorageResourceExhaustedException("Catalog exceeds the page traversal bound.");
        List<PageId> ids = new(pageCount);
        try
        {
            for (var index = 0; index < pageCount; index++)
                ids.Add(await allocator.AllocateAsync(PageType.Catalog, cancellationToken).ConfigureAwait(false));
            for (var index = 0; index < ids.Count; index++)
            {
                var page = new byte[pageStore.PageSize];
                PageHeaderCodec.Write(page, new PageHeader(ids[index], PageType.Catalog, PageFormatVersion.Current,
                    new LogSequenceNumber(0), PageChecksumAlgorithm.Crc32, 0));
                var hasNext = index + 1 < ids.Count;
                page[32] = hasNext ? (byte)1 : (byte)0;
                if (hasNext) BinaryPrimitives.WriteUInt64LittleEndian(page.AsSpan(36), ids[index + 1].Value);
                var sourceOffset = checked(index * capacity);
                var length = Math.Min(capacity, encoded.Length - sourceOffset);
                BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(44), checked((uint)length));
                encoded.AsSpan(sourceOffset, length).CopyTo(page.AsSpan(ChainHeaderLength));
                PageChecksum.WriteChecksum(page, pageStore.PageSize);
                await pageStore.WriteAsync(ids[index], page, cancellationToken).ConfigureAwait(false);
            }
            return (ids[0], ids.AsReadOnly());
        }
        catch
        {
            for (var index = ids.Count - 1; index >= 0; index--)
                await allocator.FreeAsync(ids[index], CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask<CatalogDefinition> ReadAsync(PageId rootPageId,
        CancellationToken cancellationToken = default)
    {
        var bytes = new ArrayBufferWriter<byte>();
        HashSet<PageId> seen = [];
        PageId? current = rootPageId;
        while (current is { } pageId)
        {
            if (seen.Count >= MaximumPages) throw new StorageCorruptionException("Catalog page chain exceeds its traversal bound.");
            if (!seen.Add(pageId)) throw new StorageCorruptionException($"Cycle detected in catalog page chain at {pageId}.");
            var page = new byte[pageStore.PageSize];
            await pageStore.ReadAsync(pageId, page, cancellationToken).ConfigureAwait(false);
            PageChecksum.ValidateChecksum(page, pageStore.PageSize);
            PageHeaderCodec.Read(page).Validate(pageId, PageType.Catalog);
            if (page.AsSpan(33, 3).IndexOfAnyExcept((byte)0) >= 0)
                throw new StorageFormatException("Reserved catalog-page bytes must be zero.");
            var hasNext = page[32] switch { 0 => false, 1 => true, _ => throw new StorageFormatException("Invalid catalog-page link flag.") };
            var nextValue = BinaryPrimitives.ReadUInt64LittleEndian(page.AsSpan(36));
            if (!hasNext && nextValue != 0) throw new StorageFormatException("Terminal catalog page has nonzero link bytes.");
            var length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(page.AsSpan(44)));
            if (length > pageStore.PageSize - ChainHeaderLength) throw new StorageFormatException("Catalog page payload length is invalid.");
            var target = bytes.GetSpan(length);
            page.AsSpan(ChainHeaderLength, length).CopyTo(target);
            bytes.Advance(length);
            current = hasNext ? new PageId(nextValue) : null;
        }
        return CatalogCodec.Decode(bytes.WrittenSpan);
    }
}
