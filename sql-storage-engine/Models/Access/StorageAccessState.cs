// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Access;
internal sealed class StorageAccessState
{
    public object? Owner;
    public bool Activation;
    public bool Disposed;
    public bool OwnsSemaphore;
    public StorageAccessScopeState? Parent;
    public StorageAccessScopeState? Scope;
    public StorageAccessScopeState? Previous;
}