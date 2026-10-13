// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogColumn
{
    public ColumnId Id { get; internal set; }
    public string Name { get => name!; internal set => name = value; }
    public SqlType Type { get => type!; internal set => type = value; }

    private string? name;
    private SqlType? type;
    public bool IsNullable { get; internal set; }
    public string? DefaultExpression { get; internal set; }
    public CatalogIdentity? Identity { get; internal set; }
    public bool IsRowGuidCol { get; internal set; }
    public string? ComputedExpression { get; internal set; }
    public bool IsComputedPersisted { get; internal set; }
    public bool IsComputed { get; internal set; }
    public bool IsSparse { get; internal set; }
    public bool IsColumnSet { get; internal set; }
    public bool IsFileStream { get; internal set; }
    public string? MaskingFunction { get; internal set; }
    public CatalogColumnEncryption? Encryption { get; internal set; }
    public CatalogGeneratedAlwaysKind GeneratedAlways { get; internal set; }
    public bool IsHidden { get; internal set; }
    internal ColumnId IdSource { get; init; }
    internal string NameSource { get => nameSource!; init => nameSource = value; }
    internal SqlType TypeSource { get => typeSource!; init => typeSource = value; }

    private string? nameSource;
    private SqlType? typeSource;
    internal bool IsNullableSource { get; init; }
    internal string? DefaultExpressionSource { get; init; }
    internal CatalogIdentity? IdentitySource { get; init; }
    internal bool IsRowGuidColSource { get; init; }
    internal string? ComputedExpressionSource { get; init; }
    internal bool IsComputedPersistedSource { get; init; }
    internal bool IsSparseSource { get; init; }
    internal bool IsColumnSetSource { get; init; }
    internal bool IsFileStreamSource { get; init; }
    internal string? MaskingFunctionSource { get; init; }
    internal CatalogColumnEncryption? EncryptionSource { get; init; }
    internal CatalogGeneratedAlwaysKind GeneratedAlwaysSource { get; init; }
    internal bool IsHiddenSource { get; init; }
}