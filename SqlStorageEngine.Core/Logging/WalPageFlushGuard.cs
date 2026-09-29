using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Logging;

/// <summary>Enforces WAL durability through a page LSN before the buffer pool writes that page.</summary>
public sealed class WalPageFlushGuard(WriteAheadLog wal) : IPageFlushGuard
{
    public ValueTask EnsureCanFlushAsync(PageId pageId, LogSequenceNumber pageLogSequenceNumber,
        CancellationToken cancellationToken = default) => pageLogSequenceNumber.Value == 0
        ? ValueTask.CompletedTask
        : wal.FlushThroughAsync(pageLogSequenceNumber, cancellationToken);
}
