// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Models.Rows;
internal sealed record SqlMaskContext
{
    public SqlType? Type { get; init; }
    public CatalogColumn? Column { get; init; }
    public SqlValue? Value { get; init; }
    public SqlValue? Result { get; init; }
    public string? Function { get; init; }
    public string? Kind { get; init; }
    public string? Text { get; init; }
    public SqlStorageFamily Family { get; init; }
    public int Prefix { get; init; }
    public int Suffix { get; init; }
    public string? Padding { get; init; }
    public long Start { get; init; }
    public long End { get; init; }
    public char Part { get; init; }
    public DateTime DateTime { get; init; }
    public long Entropy { get; init; }
}