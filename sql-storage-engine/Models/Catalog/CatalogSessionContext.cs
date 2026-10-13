// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Models.Catalog;
internal sealed class CatalogSessionContext
{
    public int Operation { get; init; }
    public CatalogDefinition? Definition { get; set; }
    public PageId? RootPageId { get; set; }
    public string? Name { get; set; }
    public ulong SchemaVersion { get; set; }
    public IEnumerable<CatalogColumn>? Columns { get; set; }
    public CatalogTable? TableResult { get; set; }
    public IEnumerable<CatalogCheckConstraint>? CheckConstraints { get; set; }
    public CatalogTableName? QualifiedName { get; set; }
    public CatalogTableName? HistoryName { get; set; }
    public ColumnId PeriodStartColumnId { get; set; }
    public ColumnId PeriodEndColumnId { get; set; }
    public TableId TableId { get; set; }
    public SqlValue? ValueResult { get; set; }
    public CatalogTable? Table { get; set; }
    public bool Found { get; set; }
    public ColumnId IdentityColumnId { get; set; }
    public IndexId IdentityIndexId { get; set; }
    public TableId FromNodeTableId { get; set; }
    public ColumnId FromNodeColumnId { get; set; }
    public IndexId OutgoingIndexId { get; set; }
    public TableId ToNodeTableId { get; set; }
    public ColumnId ToNodeColumnId { get; set; }
    public IndexId IncomingIndexId { get; set; }
    public CatalogTable? CatalogTableTable { get; set; }
    public sql_storage_engine.Models.Heap.TableHeapBinding? HeapResult { get; set; }
    public bool IsUnique { get; set; }
    public IEnumerable<CatalogIndexedColumn>? IndexedColumns { get; set; }
    public CatalogIndex? IndexResult { get; set; }
    public CatalogBTreeIndexOptions? Options { get; set; }
    public CatalogIndexedColumn? Column { get; set; }
    public CatalogSpecializedIndexOptions? CatalogSpecializedIndexOptionsOptions { get; set; }
    public CatalogIndex? Index { get; set; }
    public SqlType? TypeDefinition { get; set; }
    public CatalogScalarType? ScalarTypeResult { get; set; }
    public string? SchemaName { get; set; }
    public IEnumerable<CatalogTableTypeIndex>? TableTypeIndexes { get; set; }
    public CatalogTableType? TableTypeResult { get; set; }
    public bool IsMemoryOptimized { get; set; }
    public CatalogScalarType? Type { get; set; }
    public CatalogTableType? CatalogTableTypeType { get; set; }
    public SqlXmlSchemaCollection? Collection { get; set; }
    public CatalogAssembly? Assembly { get; set; }
    public SqlXmlSchemaCollection? SqlXmlSchemaCollectionCollection { get; set; }
    public SqlXmlSchemaCollection? XmlSchemaCollectionResult { get; set; }
    public IEnumerable<string>? AdditionalDefinitions { get; set; }
    public CatalogAssembly? CatalogAssemblyAssembly { get; set; }
    public CatalogAssembly? AssemblyResult { get; set; }
    public CatalogIndex? CatalogIndexIndex { get; set; }
    public IndexTreeBinding? TreeResult { get; set; }
    public StorageSecurityState? State { get; set; }
    public CatalogTableName? []? QualifiedNames { get; set; }
    public ColumnId[]? PeriodColumnIds { get; set; }
    public CatalogGraphTable? Graph { get; set; }
}