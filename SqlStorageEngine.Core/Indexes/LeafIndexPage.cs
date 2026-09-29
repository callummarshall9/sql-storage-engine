using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Indexes;

public sealed record LeafIndexPage(
    PageId PageId,
    PageId? ParentPageId,
    PageId? PreviousPageId,
    PageId? NextPageId,
    IReadOnlyList<LeafIndexEntry> Entries);
