// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Tables;

namespace sql_storage_engine.Models.Catalog;
internal sealed class CatalogSessionState
{
    private CatalogDefinition? definition;
    public CatalogDefinition Definition { get => definition!; set => definition = value; }
    public PageId? RootPageId { get; set; }
    public Dictionary<TableId, TableHeapBinding>? HeapBindings { get; init; }
    public Dictionary<IndexId, IndexTreeBinding>? IndexBindings { get; init; }
    public Dictionary<IndexId, IndexRootContext>? IndexRoots { get; init; }
    public Dictionary<TableId, TableStorageContext>? TableStorages { get; init; }
}