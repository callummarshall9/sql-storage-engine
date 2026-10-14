// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Transactions;
internal sealed record TransactionMetadata
{
    public StorageTransactionIdentity Identity { get; init; }
    public StorageTransactionState? State { get; init; }
}