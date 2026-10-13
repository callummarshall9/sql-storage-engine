// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Numerics;
using System;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogTable
{
    public TableId Id { get; internal set; }
    public string Name { get => name!; internal set => name = value; }
    public string DatabaseName { get => databaseName!; internal set => databaseName = value; }
    public string SchemaName { get => schemaName!; internal set => schemaName = value; }
    public CatalogTableName QualifiedName { get => qualifiedName!; internal set => qualifiedName = value; }

    private string? name;
    private string? databaseName;
    private string? schemaName;
    private CatalogTableName? qualifiedName;
    public ulong SchemaVersion { get; internal set; }
    public PageId FirstHeapPageId { get; internal set; }
    public IReadOnlyList<CatalogColumn> Columns { get => columns!; internal set => columns = value; }
    public IReadOnlyList<CatalogCheckConstraint> CheckConstraints { get => checkConstraints!; internal set => checkConstraints = value; }

    private IReadOnlyList<CatalogColumn>? columns;
    private IReadOnlyList<CatalogCheckConstraint>? checkConstraints;
    public BigInteger? NextIdentityValue { get; internal set; }
    public CatalogSystemVersioning? SystemVersioning { get; internal set; }
    public CatalogGraphTable? Graph { get; internal set; }
    internal TableId IdSource { get; init; }
    internal string NameSource { get => nameSource!; init => nameSource = value; }

    private string? nameSource;
    internal ulong SchemaVersionSource { get; init; }
    internal PageId FirstHeapPageIdSource { get; init; }
    internal IEnumerable<CatalogColumn> ColumnsSource { get => columnsSource!; init => columnsSource = value; }

    private IEnumerable<CatalogColumn>? columnsSource;
    internal IEnumerable<CatalogCheckConstraint>? CheckConstraintsSource { get; init; }
    internal BigInteger? NextIdentityValueSource { get; init; }
    internal string DatabaseNameSource { get => databaseNameSource!; init => databaseNameSource = value; }
    internal string SchemaNameSource { get => schemaNameSource!; init => schemaNameSource = value; }

    private string? databaseNameSource;
    private string? schemaNameSource;
    internal CatalogSystemVersioning? SystemVersioningSource { get; init; }
    internal CatalogGraphTable? GraphSource { get; init; }
    public Guid ObjectId { get; internal set; }
}