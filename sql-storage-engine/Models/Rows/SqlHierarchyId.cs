// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Rows;
public sealed record SqlHierarchyId
{
    public string Path { get => path!; init => path = value; }

    private string? path;
}