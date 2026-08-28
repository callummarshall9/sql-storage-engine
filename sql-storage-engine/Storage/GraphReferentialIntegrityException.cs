namespace sql_storage_engine.Storage;

/// <summary>Raised when a graph mutation would create a dangling endpoint or delete a referenced node.</summary>
public sealed class GraphReferentialIntegrityException : StorageException
{
    public GraphReferentialIntegrityException(string message) : base(message) { }
}
