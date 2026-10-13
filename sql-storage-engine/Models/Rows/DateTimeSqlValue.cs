// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record DateTimeSqlValue : SqlValue
{
    public override SqlStorageFamily? StorageFamily { get; init; }
    public System.DateTime Value { get; init; }
}