// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record VectorSqlValue : SqlValue
{
    public override SqlStorageFamily? StorageFamily { get; init; }
    public SqlVectorBaseType BaseType { get; init; }
    public string Content { get => content!; init => content = value; }

    private string? content;
}