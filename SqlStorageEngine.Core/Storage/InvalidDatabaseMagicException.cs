namespace sql_storage_engine.Storage;

public sealed class InvalidDatabaseMagicException : StorageFormatException
{
    public InvalidDatabaseMagicException() : base("The file does not contain the SQL storage-engine magic number.") { }
}
