// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record SpatialSqlValue : SqlValue
{
    public override SqlStorageFamily? StorageFamily { get; init; }

    private SqlSpatial? value;
    public SqlSpatial Value { get => value!; init => this.value = value; }
}