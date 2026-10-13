// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogTableType
{
    public string SchemaName { get => schemaName!; internal set => schemaName = value; }
    public string Name { get => name!; internal set => name = value; }
    public string QualifiedName { get => qualifiedName!; internal set => qualifiedName = value; }
    public IReadOnlyList<CatalogColumn> Columns { get => columns!; internal set => columns = value; }
    public IReadOnlyList<CatalogTableTypeIndex> Indexes { get => indexes!; internal set => indexes = value; }
    public IReadOnlyList<CatalogCheckConstraint> CheckConstraints { get => checkConstraints!; internal set => checkConstraints = value; }

    private string? schemaName;
    private string? name;
    private string? qualifiedName;
    private IReadOnlyList<CatalogColumn>? columns;
    private IReadOnlyList<CatalogTableTypeIndex>? indexes;
    private IReadOnlyList<CatalogCheckConstraint>? checkConstraints;
    public bool IsMemoryOptimized { get; internal set; }
    internal string SchemaNameSource { get => schemaNameSource!; init => schemaNameSource = value; }
    internal string NameSource { get => nameSource!; init => nameSource = value; }
    internal IEnumerable<CatalogColumn> ColumnsSource { get => columnsSource!; init => columnsSource = value; }

    private string? schemaNameSource;
    private string? nameSource;
    private IEnumerable<CatalogColumn>? columnsSource;
    internal IEnumerable<CatalogTableTypeIndex>? IndexesSource { get; init; }
    internal IEnumerable<CatalogCheckConstraint>? CheckConstraintsSource { get; init; }
    internal bool IsMemoryOptimizedSource { get; init; }
}