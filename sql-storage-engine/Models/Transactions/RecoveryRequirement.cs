// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Transactions;
/// <summary>Stores the monotonic in-process recovery flag; access is synchronized by its service.</summary>
internal sealed class RecoveryRequirement
{
    public int Required;
}