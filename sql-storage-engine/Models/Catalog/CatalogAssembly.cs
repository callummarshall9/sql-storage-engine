// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogAssembly
{
    public string Name { get => _name!; init => _name = value; }

    private string? _name;
    public string? Version { get; init; }
    public string? Culture { get; init; }
    public string? PublicKeyToken { get; init; }
    public CatalogAssemblyPermissionSet PermissionSet { get; init; }
    public string ImageContent { get => _imageContent!; init => _imageContent = value; }

    private string? _imageContent;
}