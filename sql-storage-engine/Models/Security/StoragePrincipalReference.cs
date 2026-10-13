// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Security;

namespace sql_storage_engine.Models.Security;
/// <summary>Host-trusted reference index. Future role/ownership/delegation writers must update this atomically with their records.</summary>
public sealed record StoragePrincipalReference
{
    public StoragePrincipalId Principal { get; init; }
    public StoragePrincipalDependencyKind Kind { get; init; }
    public Guid ReferenceId { get; init; }
}