// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record VariantSqlValue : SqlValue
{
    public override SqlStorageFamily? StorageFamily { get; init; }
    public SqlType DeclaredType { get => declaredType!; init => declaredType = value; }
    public SqlValue Value { get => value!; init => this.value = value; }

    private SqlType? declaredType;
    private SqlValue? value;
}