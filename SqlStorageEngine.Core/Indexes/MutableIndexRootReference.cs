using System.Runtime.CompilerServices;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Indexes;

public sealed class MutableIndexRootReference(PageId rootPageId) : IIndexRootReference
{
    public PageId RootPageId { get; private set; } = rootPageId;
    public ValueTask UpdateRootAsync(PageId rootPageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (rootPageId.Value == 0) throw new ArgumentOutOfRangeException(nameof(rootPageId));
        RootPageId = rootPageId;
        return ValueTask.CompletedTask;
    }
}
