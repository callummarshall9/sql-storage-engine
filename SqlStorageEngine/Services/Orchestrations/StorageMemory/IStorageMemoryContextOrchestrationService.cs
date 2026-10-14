// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Strategies;

namespace sql_storage_engine.Services.Orchestrations.StorageMemory;
internal interface IStorageMemoryContextOrchestrationService
{
    ValueTask<StorageStrategyContext> ProcessStorageStrategyContextAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken);
}