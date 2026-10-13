// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Overflow;

namespace sql_storage_engine.Models.Storage;
internal sealed class StorageSessionConfiguration
{
    public EngineResourceBinding? EngineResourcesBinding { get; set; }
    public CatalogStorageConfiguration? CatalogStorage { get; set; }
    public RowCodecBinding? RowCodecBinding { get; set; }
    public OverflowPageResourceConfiguration? Overflow { get; set; }
    public RowEncodingConfiguration? Rows { get; set; }
    public StorageGenerationConfiguration? Generation { get; set; }
}