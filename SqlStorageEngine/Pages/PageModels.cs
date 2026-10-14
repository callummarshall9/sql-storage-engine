// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Pages;
/// <summary>Identifies the logical contents of a persistent page.</summary>
public enum PageType : ushort
{
    Unknown = 0,
    DatabaseHeader = 1,
    Catalog = 2,
    Heap = 3,
    BPlusTreeInternal = 4,
    BPlusTreeLeaf = 5,
    Overflow = 6,
    Free = 7
}

/// <summary>Algorithm used to protect a complete page from corruption.</summary>
public enum PageChecksumAlgorithm : ushort
{
    None = 0,
    Crc32 = 1
}