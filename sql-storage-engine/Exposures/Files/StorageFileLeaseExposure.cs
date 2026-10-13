// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Services.Foundations.FileLeases;

namespace sql_storage_engine.Exposures.Files;
public sealed class StorageFileLeaseExposure : IDisposable
{
    private readonly StorageFileLeaseService service;
    private StorageFileLeaseExposure(StorageFileLeaseConfiguration configuration)
    {
        service = new StorageFileLeaseService(configuration: configuration);
    }

    public static StorageFileLeaseExposure Acquire(string path, bool creating) =>
        new StorageFileLeaseExposure(configuration: new StorageFileLeaseConfiguration { Path = path, Creating = creating });

    public void Dispose() =>
        service.CloseStorageFileLease();
}