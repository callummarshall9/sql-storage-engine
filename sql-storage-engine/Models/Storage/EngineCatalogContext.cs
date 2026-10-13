// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Storage;
internal sealed class EngineCatalogContext
{
    public int Operation { get; set; }
    public CatalogSessionContext? Request { get; set; }
    public CatalogDefinition? Definition { get; set; }
    public string? DefaultCollation { get; set; }
    public DatabaseId DatabaseId { get; set; }
    public TableId TableId { get; set; }
    public IndexId IndexId { get; set; }
    public CatalogTable? Table { get; set; }
    public IReadOnlyList<CatalogIndex>? Indexes { get; set; }
    public bool Found { get; set; }
    public StorageTableStatistics? TableStatistics { get; set; }
    public StorageIndexStatistics? IndexStatistics { get; set; }
}