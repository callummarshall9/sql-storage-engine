// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System;

namespace sql_storage_engine.Models;
public readonly record struct StorageTransactionIdentity
{
    public DatabaseId DatabaseId { get; init; }
    public Guid Value { get; init; }
}