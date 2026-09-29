namespace sql_storage_engine.Storage;

/// <summary>Indicates that read-only open cannot proceed until crash recovery runs.</summary>
public sealed class RecoveryRequiredException : StorageException
{
    public RecoveryRequiredException() : base("Database requires recovery before it can be opened read-only.") { }
}
