namespace sql_storage_engine.Storage;

public sealed class UnsupportedDatabaseVersionException : StorageFormatException
{
    public UnsupportedDatabaseVersionException(ushort version) : base($"Unsupported database format version {version}.") { }
}
