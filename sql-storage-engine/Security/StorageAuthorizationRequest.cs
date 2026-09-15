namespace sql_storage_engine.Security;

public sealed class StorageAuthorizationRequest
{
    public StorageAuthorizationRequest(StoragePrincipalId principal, long revision, Guid operationId,
        Guid correlationId, IEnumerable<StorageRequiredAccess> access)
    {
        ArgumentNullException.ThrowIfNull(access);
        Principal = principal; Revision = revision; OperationId = operationId; CorrelationId = correlationId;
        Access = Array.AsReadOnly(access.Take(257).ToArray());
    }
    public StoragePrincipalId Principal { get; }
    public long Revision { get; }
    public Guid OperationId { get; }
    public Guid CorrelationId { get; }
    public IReadOnlyList<StorageRequiredAccess> Access { get; }
}
