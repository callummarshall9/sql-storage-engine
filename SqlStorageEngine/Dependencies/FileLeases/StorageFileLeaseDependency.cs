// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;

namespace sql_storage_engine.Dependencies.FileLeases;
internal sealed class StorageFileLeaseDependency : IDisposable
{
    private FileStream? stream;
    public void Open(string path, bool creating)
    {
        var full = Path.GetFullPath(path: path);

        if (creating)
        {
            Directory.CreateDirectory(path: Path.GetDirectoryName(path: full)!);
        }

        stream = new FileStream(full + ".storage-lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public void Dispose()
    {
        stream?.Dispose();
        stream = null;
    }
}