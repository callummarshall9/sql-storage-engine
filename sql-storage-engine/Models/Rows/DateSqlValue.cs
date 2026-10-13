// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record DateSqlValue : SqlValue
{
    public override SqlStorageFamily? StorageFamily { get; init; }
    public DateOnly Value { get; init; }
}