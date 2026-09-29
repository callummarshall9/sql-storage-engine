using System.Runtime.CompilerServices;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Indexes;

public interface IIndexRootReference
{
    PageId RootPageId { get; }
    ValueTask UpdateRootAsync(PageId rootPageId, CancellationToken cancellationToken = default);
}
