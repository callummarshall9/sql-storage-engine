namespace sql_storage_engine.Transactions;

internal static class StorageSavepointFiles
{
    internal static string NewPath(string databasePath) => databasePath + ".savepoint-" + Guid.NewGuid().ToString("N");
    internal static void DeleteAll(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        var prefix = Path.GetFileName(fullPath) + ".savepoint-";
        foreach (var path in Directory.EnumerateFiles(Path.GetDirectoryName(fullPath)!))
            if (Path.GetFileName(path).StartsWith(prefix, StringComparison.Ordinal)) TransactionDirectory.Delete(path);
    }
}
