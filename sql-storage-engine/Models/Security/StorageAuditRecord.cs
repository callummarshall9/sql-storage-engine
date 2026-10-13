// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Security;

namespace sql_storage_engine.Models.Security;
public sealed record StorageAuditRecord
{
    public Guid OperationId { get; init; }
    public Guid CorrelationId { get; init; }
    public StoragePrincipalId? Principal { get; init; }
    public Guid? ObjectId { get; init; }
    public StoragePermissionAction Action { get; init; }
    public StorageAuditKind Kind { get; init; }
    public long Revision { get; init; }
    public StorageAuditReason Reason { get; init; }
    public StoragePrincipalId? TargetPrincipal { get; init; }
    public StoragePrincipalOperation? PrincipalOperation { get; init; }
}