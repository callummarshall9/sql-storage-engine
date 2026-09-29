using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Heap;

/// <summary>Heap-specific metadata following the common page header.</summary>
public readonly record struct HeapPageHeader(
    PageId? PreviousPageId,
    PageId? NextPageId,
    ushort SlotCount,
    uint SlotDirectoryEnd,
    uint RowDataStart);
