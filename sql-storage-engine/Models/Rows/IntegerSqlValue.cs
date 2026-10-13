// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record IntegerSqlValue : SqlValue
{
    public override SqlStorageFamily? StorageFamily { get; init; }
    public long Value { get; init; }
}