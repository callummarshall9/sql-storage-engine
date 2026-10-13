// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models.Storage;
internal sealed record StorageGenerationContext
{
    public PageDatabaseContext? Database { get; init; }
    public object? TimeProviderSource { get; init; }
    public CatalogGeneratedAlwaysKind Kind { get; init; }
    public sql_storage_engine.Models.Identifiers.PageId RootPageId { get; init; }
    public System.DateTimeOffset Timestamp { get; init; }
    public SqlValue? Value { get; init; }
}