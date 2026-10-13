// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record SqlCollation
{
    public string Name { get => name!; init => name = value; }

    private string? name;
    private string? cultureName;
    public ushort Lcid { get; init; }
    public byte Version { get; init; }
    public SqlCollationComparisonFlags ComparisonFlags { get; init; }
    public byte SortId { get; init; }
    public ushort CodePage { get; init; }
    public string CultureName { get => cultureName!; init => cultureName = value; }
}