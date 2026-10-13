// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record NullSqlValue : SqlValue
{
    public override SqlStorageFamily? StorageFamily { get; init; }
}