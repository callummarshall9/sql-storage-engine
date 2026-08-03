using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Buffers;

/// <summary>
/// Holds a page resident in the buffer pool. The handle and its memory must not be used after disposal.
/// </summary>
public interface IPinnedPage : IDisposable
{
    PageId PageId { get; }
    Memory<byte> Memory { get; }
    LogSequenceNumber PageLogSequenceNumber { get; }
    bool IsDirty { get; }

    /// <summary>Marks the page dirty at the supplied log position.</summary>
    void MarkDirty(LogSequenceNumber pageLogSequenceNumber);
}
