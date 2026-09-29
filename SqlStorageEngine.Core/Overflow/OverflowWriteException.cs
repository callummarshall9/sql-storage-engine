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
