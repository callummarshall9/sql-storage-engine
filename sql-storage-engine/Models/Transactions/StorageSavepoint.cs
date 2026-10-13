// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Transactions;
public sealed record StorageSavepoint
{
    public const int MaximumNameLength = 32;
    public const int MaximumCount = 32;
    public StorageTransactionIdentity Transaction { get; init; }
    public Guid Value { get; init; }
    public string Name { get => name!; init => name = value; }

    private string? name;
}