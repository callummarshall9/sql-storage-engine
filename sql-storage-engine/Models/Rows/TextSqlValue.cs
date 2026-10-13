// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record TextSqlValue : SqlValue
{
    public override SqlStorageFamily? StorageFamily { get; init; }

    private string? value;
    public string Value { get => value!; init => this.value = value; }
}