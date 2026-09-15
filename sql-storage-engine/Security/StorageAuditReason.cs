namespace sql_storage_engine.Security;

public enum StorageAuditReason { None, PermissionDenied, MissingPrincipal, StaleRevision, StaleObject, AuditUnavailable }
