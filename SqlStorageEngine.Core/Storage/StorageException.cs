namespace sql_storage_engine.Storage;

/// <summary>Base exception for storage-engine failures.</summary>
public class StorageException : Exception
{
    public StorageException(string message) : base(message) { }
    public StorageException(string message, Exception innerException) : base(message, innerException) { }
}
