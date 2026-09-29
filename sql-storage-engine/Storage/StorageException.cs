namespace sql_storage_engine.Storage;

/// <summary>Indicates that a requested catalog name or identity is already published.</summary>
public sealed class CatalogConflictException : StorageException
{
    public CatalogConflictException(string message) : base(message) { }
}
