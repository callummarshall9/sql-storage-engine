namespace sql_storage_engine.Storage;

/// <summary>Indicates an operating-system storage resource failure.</summary>
public sealed class StorageResourceException : StorageException
{
    public StorageResourceException(string message, Exception innerException) : base(message, innerException) { }
}
