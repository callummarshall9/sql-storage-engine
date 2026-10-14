// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;

namespace sql_storage_engine.Services.Orchestrations.TransactionRecovery;
internal sealed partial class StorageTransactionReceiptRecoveryOrchestrationService
{
    private static void ValidateEngineResourceContext(sql_storage_engine.Models.Storage.EngineResourceContext engineResourceContext)
    {
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext);
    }

    private static void ValidateEngineResourceContext(sql_storage_engine.Models.Storage.EngineResourceContext engineResourceContext, CancellationToken cancellationToken)
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateEngineResourceMetadataContext(sql_storage_engine.Models.Storage.EngineResourceContext engineResourceContext, CancellationToken cancellationToken)
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.Metadata);

        if (engineResourceContext.Metadata.State is not null)
        {
            throw new ArgumentException(message: "Active transaction metadata cannot contain a decision.");
        }
    }
}