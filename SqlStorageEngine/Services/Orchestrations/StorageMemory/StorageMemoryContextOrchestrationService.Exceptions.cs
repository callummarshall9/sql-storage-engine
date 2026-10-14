// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.StorageMemory;
internal sealed partial class StorageMemoryContextOrchestrationService
{
    private static async ValueTask<StorageStrategyContext> TryCatch(Func<ValueTask<StorageStrategyContext>> operation)
    {
        try
        {
            return await operation()
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception exception) when (exception is StorageException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            throw;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new StorageModelRangeValidationException(innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(innerException: exception);
        }
        catch (System.IO.IOException exception)
        {
            throw new StorageModelDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }
}