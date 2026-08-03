namespace sql_storage_engine.Storage;

/// <summary>Base exception for storage-engine failures.</summary>
public class StorageException : Exception
{
    public StorageException(string message) : base(message) { }
    public StorageException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Indicates an unsupported or malformed persistent format.</summary>
public class StorageFormatException : StorageException
{
    public StorageFormatException(string message) : base(message) { }
    public StorageFormatException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class InvalidDatabaseMagicException : StorageFormatException
{
    public InvalidDatabaseMagicException() : base("The file does not contain the SQL storage-engine magic number.") { }
}

public sealed class UnsupportedDatabaseVersionException : StorageFormatException
{
    public UnsupportedDatabaseVersionException(ushort version) : base($"Unsupported database format version {version}.") { }
}

public sealed class InvalidPageSizeException : StorageFormatException
{
    public InvalidPageSizeException(int pageSize) : base($"Unsupported database page size {pageSize}.") { }
}

/// <summary>Indicates that persisted data failed an integrity check.</summary>
public sealed class StorageCorruptionException : StorageException
{
    public StorageCorruptionException(string message) : base(message) { }
    public StorageCorruptionException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Indicates an operating-system storage resource failure.</summary>
public sealed class StorageResourceException : StorageException
{
    public StorageResourceException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Indicates that a bounded storage resource has no available capacity.</summary>
public sealed class StorageResourceExhaustedException : StorageException
{
    public StorageResourceExhaustedException(string message) : base(message) { }
}
