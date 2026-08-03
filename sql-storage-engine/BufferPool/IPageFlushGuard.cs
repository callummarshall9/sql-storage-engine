using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Buffers;

/// <summary>Guards page writes; a WAL implementation can require durability through the page LSN.</summary>
public interface IPageFlushGuard
{
    ValueTask EnsureCanFlushAsync(
        PageId pageId,
        LogSequenceNumber pageLogSequenceNumber,
        CancellationToken cancellationToken = default);
}

internal sealed class NoOpPageFlushGuard : IPageFlushGuard
{
    internal static NoOpPageFlushGuard Instance { get; } = new();
    public ValueTask EnsureCanFlushAsync(PageId pageId, LogSequenceNumber pageLogSequenceNumber,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
