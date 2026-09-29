namespace sql_storage_engine.Storage;

/// <summary>Indicates that an insertion would duplicate a logical key in a unique index.</summary>
public sealed class DuplicateIndexKeyException : StorageException
{
    public DuplicateIndexKeyException() : base("The unique index already contains the requested logical key.") { }
}
