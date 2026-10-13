// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private static void ValidateStorageSessionContext(StorageSessionContext storageSessionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext);
    }
}