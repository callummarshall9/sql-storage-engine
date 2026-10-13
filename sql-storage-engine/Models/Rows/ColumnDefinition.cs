// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Rows;
public sealed record ColumnDefinition
{
    public ColumnId Id { get; init; }
    public string Name { get => name!; init => name = value; }
    public SqlType Type { get => type!; init => type = value; }

    private string? name;
    private SqlType? type;
    public bool IsNullable { get; init; }
    public CatalogColumnEncryption? Encryption { get; init; }
    public bool IsSparse { get; init; }
    public bool IsColumnSet { get; init; }
}