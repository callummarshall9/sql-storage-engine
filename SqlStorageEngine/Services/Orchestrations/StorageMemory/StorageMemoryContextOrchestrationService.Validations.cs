// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.StorageMemory;
internal sealed partial class StorageMemoryContextOrchestrationService
{
    private static void ValidateStorageStrategyContext(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext);
        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext.State);
        cancellationToken.ThrowIfCancellationRequested();

        if (storageStrategyContext.Operation is not (100 or 101 or 102 or 103 or 110 or 111 or 112 or 120 or 121 or 122))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(storageStrategyContext.Operation));
        }

        if (storageStrategyContext.Operation is 120 or 121 or 122)
        {
            if (!Enum.IsDefined(value: (PageSize)storageStrategyContext.State.PageSize))
            {
                throw new InvalidPageSizeException(pageSize: storageStrategyContext.State.PageSize);
            }
        }

        if (storageStrategyContext.Operation is 121 or 122)
        {
            ArgumentNullException.ThrowIfNull(argument: storageStrategyContext.Bytes);

            if (storageStrategyContext.Bytes.Length != storageStrategyContext.State.PageSize)
            {
                throw new StorageFormatException(message: "A complete page is required for header and checksum operations.");
            }
        }
    }
}