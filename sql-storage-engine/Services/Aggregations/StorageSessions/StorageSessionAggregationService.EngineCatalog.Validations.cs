// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private static void ValidateEngineCatalogRequest(StorageSessionContext storageSessionContext, CancellationToken cancellationToken, bool reading)
    {
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext);
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext.Engine);
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext.Engine.ResourceSource);
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext.EngineCatalog);

        if (storageSessionContext.Statement is { } statement && !statement.Active)
        {
            throw new ObjectDisposedException(objectName: "StorageStatementExposure");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var context = storageSessionContext.EngineCatalog;

        if (reading ? context.Operation is < 0 or > 7 or 5 or 6 : context.Operation is not (1 or 5 or 6))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(storageSessionContext));
        }

        if (context.Operation == 1)
        {
            ArgumentNullException.ThrowIfNull(argument: context.Request);

            if (reading && context.Request.Operation is not (6 or 7 or 8 or 15 or 19 or 20 or 21 or 22))
            {
                throw new ArgumentException(message: "A metadata read requires a catalogue lookup.", paramName: nameof(storageSessionContext));
            }
        }
    }

    private static void EnsureEngineCatalogCurrent(StorageEngineRuntime storageEngineRuntime)
    {
        if (storageEngineRuntime.Disposed)
        {
            throw new ObjectDisposedException(objectName: "StorageEngineExposure");
        }

        if (storageEngineRuntime.Quarantined)
        {
            throw new InvalidOperationException(message: "The storage engine is quarantined after incomplete transaction resolution.");
        }
    }
}