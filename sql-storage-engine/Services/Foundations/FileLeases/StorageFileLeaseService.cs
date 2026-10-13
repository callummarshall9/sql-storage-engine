// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Brokers.FileLeases;

namespace sql_storage_engine.Services.Foundations.FileLeases;
internal sealed partial class StorageFileLeaseService : IStorageFileLeaseService
{
    private readonly StorageFileLeaseBroker broker = new();
    private readonly object sync = new();
    public StorageFileLeaseService(StorageFileLeaseConfiguration configuration)
    {
        OpenStorageFileLease(configuration: configuration);
    }

    private void OpenStorageFileLease(StorageFileLeaseConfiguration configuration) =>
        TryCatch(operation: () =>
    {
        ValidateStorageFileLeaseConfiguration(configuration: configuration);
        broker.Open(path: configuration.Path!, creating: configuration.Creating);
    });

    public void CloseStorageFileLease() =>
        TryCatch(operation: () =>
    {
        lock (sync)
        {
            broker.Close();
        }
    });
}