namespace sql_storage_engine.Security;

/// <summary>Publication failed. Resolve OperationId before retrying; absence is not a durable acknowledgement.</summary>
public sealed class StorageAuditException(Guid operationId) : IOException("Audit publication did not produce a confirmed durable acknowledgement.")
{
    public Guid OperationId { get; } = operationId;
}
