// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogTableName
{
    public string DatabaseName { get => _databaseName!; init => _databaseName = value; }
    public string SchemaName { get => _schemaName!; init => _schemaName = value; }
    public string Name { get => _name!; init => _name = value; }

    private string? _databaseName;
    private string? _schemaName;
    private string? _name;
}