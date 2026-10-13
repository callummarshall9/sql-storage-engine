// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.Models.Storage;
internal sealed class StorageGenerationConfiguration
{
    public PageDatabaseBinding? DatabaseBinding { get; set; }
    public object? TimeProviderSource { get; set; }
}