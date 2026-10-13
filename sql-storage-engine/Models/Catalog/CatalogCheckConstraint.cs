// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogCheckConstraint
{
    public string Name { get => _name!; init => _name = value; }
    public string Expression { get => _expression!; init => _expression = value; }

    private string? _name;
    private string? _expression;
}