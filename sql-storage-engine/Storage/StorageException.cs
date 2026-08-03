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

/// <summary>Indicates that an insertion would duplicate a logical key in a unique index.</summary>
public sealed class DuplicateIndexKeyException : StorageException
{
    public DuplicateIndexKeyException() : base("The unique index already contains the requested logical key.") { }
}

/// <summary>Indicates that a requested catalog name or identity is already published.</summary>
public sealed class CatalogConflictException : StorageException
{
    public CatalogConflictException(string message) : base(message) { }
}

/// <summary>Reports every index page allocated by a failed build and any page that cleanup could not reclaim.</summary>
public sealed class IndexBuildException : StorageException
{
    public IndexBuildException(string message, IReadOnlyList<sql_storage_engine.Identifiers.PageId> allocatedPageIds,
        IReadOnlyList<sql_storage_engine.Identifiers.PageId> unreclaimedPageIds, Exception innerException)
        : base(message, innerException)
    {
        AllocatedPageIds = allocatedPageIds.ToArray();
        UnreclaimedPageIds = unreclaimedPageIds.ToArray();
    }
    public IReadOnlyList<sql_storage_engine.Identifiers.PageId> AllocatedPageIds { get; }
    public IReadOnlyList<sql_storage_engine.Identifiers.PageId> UnreclaimedPageIds { get; }
}
