// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Foundations.ObjectStorageResources;
using sql_storage_engine.Services.Foundations.ObjectManifests;
using sql_storage_engine.Brokers.ObjectStorageResources;

namespace sql_storage_engine.Services.Orchestrations.ObjectCommits;
internal sealed partial class ObjectCommitContextOrchestrationService
{
    private static void ValidateStorageStrategyContext(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext);

        cancellationToken.ThrowIfCancellationRequested();
    }
}