// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Models.Catalog;
internal sealed class CatalogStorageConfiguration
{
    public CatalogPageResourceConfiguration? Pages { get; set; }
    public CatalogBufferConfiguration? Buffer { get; set; }
    public HeapPageResourceConfiguration? Heap { get; set; }
    public IndexTreeResourceConfiguration? Tree { get; set; }
}