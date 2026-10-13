// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Configurations;

namespace sql_storage_engine.Services.Foundations.FileLeases;
internal sealed partial class StorageFileLeaseService
{
    private static void ValidateStorageFileLeaseConfiguration(StorageFileLeaseConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(argument: configuration);
        var path = configuration.Path;
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);
    }
}