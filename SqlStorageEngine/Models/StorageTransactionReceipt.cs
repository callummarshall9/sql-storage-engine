// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models;
public sealed record StorageTransactionReceipt
{
    public StorageTransactionIdentity Identity { get; init; }
    public StorageTransactionState State { get; init; }
}