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
internal interface IStorageMemoryContextService
{
    ValueTask<StorageStrategyContext> ProcessStorageStrategyContextAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken);
}