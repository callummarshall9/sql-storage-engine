using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Security;

public sealed record StoragePrincipalChange(DatabaseId DatabaseId, StoragePrincipalId Actor, StoragePrincipalId Target, StoragePrincipalOperation Operation, long Revision, Guid OperationId, Guid CorrelationId);
