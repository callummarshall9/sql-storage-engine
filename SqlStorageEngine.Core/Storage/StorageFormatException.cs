namespace sql_storage_engine.Storage;

/// <summary>Indicates an unsupported or malformed persistent format.</summary>
public class StorageFormatException : StorageException
{
    public StorageFormatException(string message) : base(message) { }
    public StorageFormatException(string message, Exception innerException) : base(message, innerException) { }
}
