// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Security;
/// <summary>Data identifying an authorization operation and its required accesses.</summary>
public sealed class StorageAuthorizationRequest
{
    private IReadOnlyList<StorageRequiredAccess>? _access;
    internal IEnumerable<StorageRequiredAccess>? AccessSource { get; init; }
    public StoragePrincipalId Principal { get; init; }
    public long Revision { get; init; }
    public Guid OperationId { get; init; }
    public Guid CorrelationId { get; init; }
    public IReadOnlyList<StorageRequiredAccess> Access { get => _access!; init => _access = value; }
}