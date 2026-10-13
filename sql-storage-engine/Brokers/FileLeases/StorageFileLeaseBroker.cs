// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Dependencies.FileLeases;

namespace sql_storage_engine.Brokers.FileLeases;
internal sealed class StorageFileLeaseBroker : IStorageFileLeaseBroker
{
    private readonly StorageFileLeaseDependency dependency = new();
    public void Open(string path, bool creating) =>
        dependency.Open(path: path, creating: creating);

    public void Close() =>
        dependency.Dispose();
}