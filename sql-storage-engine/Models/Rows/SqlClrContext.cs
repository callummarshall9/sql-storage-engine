// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Models.Rows;
internal sealed record SqlClrContext
{
    public object? RuntimeSource { get; init; }
    public string? AssemblyName { get; init; }
    public string? ClassName { get; init; }
    public Func<string, ReadOnlyMemory<byte>>? Parse { get; init; }
    public Func<ReadOnlyMemory<byte>, string>? Format { get; init; }
    public Func<ReadOnlyMemory<byte>, bool>? Validate { get; init; }
    public Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>, int>? Compare { get; init; }
    public CatalogAssembly? Assembly { get; init; }
    public SqlType[]? DeclaredTypes { get; init; }
    public SqlType? SqlType { get; init; }
    public string? Text { get; init; }
    public ReadOnlyMemory<byte> Value { get; init; }
    public ReadOnlyMemory<byte> Other { get; init; }
    public BinarySqlValue? BinaryResult { get; init; }
    public BinarySqlValue? BinaryInput { get; init; }
    public bool Found { get; init; }
    public bool Valid { get; init; }
}