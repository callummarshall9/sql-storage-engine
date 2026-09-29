namespace sql_storage_engine.Storage;

public sealed class InvalidPageSizeException : StorageFormatException
{
    public InvalidPageSizeException(int pageSize) : base($"Unsupported database page size {pageSize}.") { }
}
