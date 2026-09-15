namespace sql_storage_engine.Security;

public sealed record StorageAuditRecord(Guid OperationId, Guid CorrelationId, StoragePrincipalId? Principal, Guid? ObjectId, StoragePermissionAction Action, StorageAuditKind Kind, long Revision, StorageAuditReason Reason = StorageAuditReason.None);
