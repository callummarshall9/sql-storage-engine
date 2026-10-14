// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Storage;
using System.Linq;
using sql_storage_engine.Brokers.StorageMemoryResources;

namespace sql_storage_engine.Services.Foundations.StorageMemory;
internal sealed partial class StorageMemoryContextService
{
    private static void ValidateStorageStrategyContext(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext);

        cancellationToken.ThrowIfCancellationRequested();
    }
}