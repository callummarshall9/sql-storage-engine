using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Overflow;

public sealed class OverflowWriteException : StorageException
{
    public OverflowWriteException(string message, IReadOnlyList<PageId> allocatedPages, Exception innerException)
        : base(message, innerException) => AllocatedPages = allocatedPages;
    public IReadOnlyList<PageId> AllocatedPages { get; }
}

/// <summary>Writes and reads exclusively owned, bounded overflow chains.</summary>
public sealed class OverflowManager
{
    private readonly BufferPool _bufferPool;
    private readonly IPageAllocator _allocator;

    public OverflowManager(BufferPool bufferPool, IPageAllocator allocator)
    {
        ArgumentNullException.ThrowIfNull(bufferPool);
        ArgumentNullException.ThrowIfNull(allocator);
        _bufferPool = bufferPool;
        _allocator = allocator;
    }

    public async ValueTask<OverflowReference> WriteAsync(ReadOnlyMemory<byte> value,
        CancellationToken cancellationToken = default)
    {
        if (value.IsEmpty) throw new ArgumentException("Overflow values cannot be empty.", nameof(value));
        if (value.Length > OverflowReferenceCodec.MaximumValueLength)
            throw new ArgumentException("Overflow value exceeds the maximum length.", nameof(value));
        var capacity = OverflowPageCodec.GetPayloadCapacity(_bufferPool.PageSize);
        var pageCount = checked((value.Length + capacity - 1) / capacity);
        if (pageCount > OverflowReferenceCodec.MaximumChainLength)
            throw new ArgumentException("Overflow value exceeds the maximum chain length.", nameof(value));
        List<PageId> allocated = [];
        try
        {
            for (var index = 0; index < pageCount; index++)
                allocated.Add(await _allocator.AllocateAsync(PageType.Overflow, cancellationToken).ConfigureAwait(false));
            for (var index = 0; index < allocated.Count; index++)
            {
                var offset = checked(index * capacity);
                var length = Math.Min(capacity, value.Length - offset);
                using var pin = await _bufferPool.GetPageAsync(allocated[index], cancellationToken).ConfigureAwait(false);
                OverflowPageCodec.Initialize(pin.Memory.Span, allocated[index],
                    index + 1 < allocated.Count ? allocated[index + 1] : null, value.Span.Slice(offset, length));
                pin.MarkDirty(new LogSequenceNumber(0));
            }
            return new OverflowReference(allocated[0], value.Length);
        }
        catch (Exception writeException)
        {
            try
            {
                await RollbackAsync(allocated).ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                throw new OverflowWriteException("Overflow write failed and allocated-page cleanup was incomplete.",
                    allocated.ToArray(), new AggregateException(writeException, cleanupException));
            }
            throw;
        }
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(OverflowReference reference,
        CancellationToken cancellationToken = default)
    {
        OverflowReferenceCodec.Validate(reference);
        var output = new byte[checked((int)reference.TotalLength)];
        var written = 0;
        var current = reference.FirstPageId;
        HashSet<PageId> visited = [];
        for (var pageNumber = 0; pageNumber < OverflowReferenceCodec.MaximumChainLength; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(current)) throw new StorageCorruptionException($"Cycle detected in overflow chain at {current}.");
            OverflowPageHeader header;
            ReadOnlyMemory<byte> payload;
            try
            {
                using var pin = await _bufferPool.GetPageAsync(current, cancellationToken).ConfigureAwait(false);
                header = OverflowPageCodec.ReadHeader(pin.Memory.Span, current);
                payload = OverflowPageCodec.ReadPayload(pin.Memory.Span, current);
            }
            catch (StorageResourceException exception)
            {
                throw new StorageCorruptionException($"Overflow chain references inaccessible {current}.", exception);
            }

            var remaining = output.Length - written;
            if (payload.Length > remaining) throw new StorageCorruptionException("Overflow chain is longer than its reference length.");
            payload.Span.CopyTo(output.AsSpan(written));
            written = checked(written + payload.Length);
            if (written == output.Length)
            {
                if (header.NextPageId is not null) throw new StorageCorruptionException("Overflow chain continues beyond its reference length.");
                return output;
            }
            if (header.NextPageId is not { } next) throw new StorageCorruptionException("Overflow chain is truncated.");
            current = next;
        }
        throw new StorageCorruptionException($"Overflow chain exceeds {OverflowReferenceCodec.MaximumChainLength} pages.");
    }

    /// <summary>Validates and frees every page exclusively owned by a chain.</summary>
    public async ValueTask FreeAsync(OverflowReference reference, CancellationToken cancellationToken = default)
    {
        _ = await ReadAsync(reference, cancellationToken).ConfigureAwait(false);
        List<PageId> pages = [];
        var current = reference.FirstPageId;
        while (true)
        {
            pages.Add(current);
            using var pin = await _bufferPool.GetPageAsync(current, cancellationToken).ConfigureAwait(false);
            var header = OverflowPageCodec.ReadHeader(pin.Memory.Span, current);
            if (header.NextPageId is not { } next) break;
            current = next;
        }
        for (var index = pages.Count - 1; index >= 0; index--)
        {
            await _bufferPool.DiscardPageAsync(pages[index], cancellationToken).ConfigureAwait(false);
            await _allocator.FreeAsync(pages[index], cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask RollbackAsync(IReadOnlyList<PageId> allocated)
    {
        for (var index = allocated.Count - 1; index >= 0; index--)
        {
            await _bufferPool.DiscardPageAsync(allocated[index]).ConfigureAwait(false);
            await _allocator.FreeAsync(allocated[index]).ConfigureAwait(false);
        }
    }
}
