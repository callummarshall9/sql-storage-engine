// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public abstract record SqlValue
{
    public abstract SqlStorageFamily? StorageFamily { get; init; }
    public bool IsNull { get; init; }
}