using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Indexes;

public sealed record InternalIndexPage(
    PageId PageId,
    PageId? ParentPageId,
    IReadOnlyList<IndexKey> Separators,
    IReadOnlyList<PageId> Children);
