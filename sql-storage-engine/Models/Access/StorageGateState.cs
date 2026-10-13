// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Access;
internal sealed class StorageGateState
{
    public object? Sync;
    public object? Owner;
    public int Leases;
    public int Pending;
    public bool Closed;
}