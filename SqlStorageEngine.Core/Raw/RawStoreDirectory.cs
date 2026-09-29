using sql_storage_engine.Transactions;

namespace SqlStorageEngine.Core;

internal static class RawStoreDirectory
{
    internal static void CreateDurable(string directory, Action<string>? directoryFlushed)
    {
        Directory.CreateDirectory(directory);
        // Persist every entry in the path, deepest first. An existing ancestor may
        // itself have been left unsynchronized by an interrupted/concurrent creator.
        // Publishing the store later also synchronizes the leaf directory itself.
        for (var current = new DirectoryInfo(directory); current.Parent is { } parent; current = parent)
        {
            TransactionDirectory.Flush(current.FullName);
            directoryFlushed?.Invoke(parent.FullName);
        }
    }
}
