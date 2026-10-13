// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record UniqueIdentifierSqlValue : SqlValue
{
    public override SqlStorageFamily? StorageFamily { get; init; }
    public Guid Value { get; init; }
}