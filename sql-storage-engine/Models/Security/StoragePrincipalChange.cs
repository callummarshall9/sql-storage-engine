// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System;
using sql_storage_engine.Security;

namespace sql_storage_engine.Models.Security;
public sealed record StoragePrincipalChange
{
    public DatabaseId DatabaseId { get; init; }
    public StoragePrincipalId Actor { get; init; }
    public StoragePrincipalId Target { get; init; }
    public StoragePrincipalOperation Operation { get; init; }
    public long Revision { get; init; }
    public Guid OperationId { get; init; }
    public Guid CorrelationId { get; init; }
}