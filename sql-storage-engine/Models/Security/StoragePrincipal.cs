// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Security;
public sealed record StoragePrincipal
{
    public StoragePrincipalId Id { get; init; }
    public bool Active { get; init; }
}