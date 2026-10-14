// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Configurations;
internal sealed class TransactionSequenceConfiguration
{
    public ulong LastAllocatedId { get; set; }
}