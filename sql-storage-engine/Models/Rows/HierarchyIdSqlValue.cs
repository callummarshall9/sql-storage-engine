// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record HierarchyIdSqlValue : SqlValue
{
    public override SqlStorageFamily? StorageFamily { get; init; }

    private SqlHierarchyId? value;
    public SqlHierarchyId Value { get => value!; init => this.value = value; }
}