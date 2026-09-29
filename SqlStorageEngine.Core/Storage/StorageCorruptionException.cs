namespace sql_storage_engine.Storage;

/// <summary>Indicates that persisted data failed an integrity check.</summary>
public sealed class StorageCorruptionException : StorageException
{
    public StorageCorruptionException(string message) : base(message) { }
    public StorageCorruptionException(string message, Exception innerException) : base(message, innerException) { }
}
