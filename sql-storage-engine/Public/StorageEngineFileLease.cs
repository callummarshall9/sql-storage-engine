namespace sql_storage_engine;

internal static class StorageEngineFileLease
{
    internal static FileStream Acquire(string path, bool creating)
    {
        var full = Path.GetFullPath(path);
        if (creating) Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        // Persistent lock inode: deleting it on close could split concurrent contenders across different inodes.
        return new FileStream(full + ".storage-lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
}
