// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Configurations;
internal sealed class StorageFileLeaseConfiguration
{
    public string? Path { get; set; }
    public bool Creating { get; set; }
}