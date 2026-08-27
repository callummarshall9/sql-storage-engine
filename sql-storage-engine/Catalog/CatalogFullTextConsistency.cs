namespace sql_storage_engine.Catalog;

/// <summary>Defines when full-text index entries become visible relative to their source rows.</summary>
public enum CatalogFullTextConsistency : byte
{
    /// <summary>Index entries are maintained in the same atomic storage statement as the source-row mutation.</summary>
    Transactional = 1
}
