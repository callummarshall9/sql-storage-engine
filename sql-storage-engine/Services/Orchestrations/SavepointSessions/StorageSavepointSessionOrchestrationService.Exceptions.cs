// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Storage;
using System.IO;
using System.Threading.Tasks;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Orchestrations.SavepointSessions;
internal sealed partial class StorageSavepointSessionOrchestrationService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new TransactionDirectoryDependencyException(exception);
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (StorageResourceException exception)
        {
            throw new StorageResourceException(message: exception.Message, innerException: exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new sql_storage_engine.Models.Exceptions.StorageSavepointOperationException(innerException: exception);
        }
        catch (AggregateException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }

    private static async ValueTask TryCatch(Func<ValueTask> operation)
    {
        try
        {
            await operation()
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException exception)
        {
            throw new TransactionMetadataCancellationException(exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new TransactionDirectoryDependencyException(exception);
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (StorageResourceException exception)
        {
            throw new StorageResourceException(message: exception.Message, innerException: exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new sql_storage_engine.Models.Exceptions.StorageSavepointOperationException(innerException: exception);
        }
        catch (AggregateException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }

    private static async ValueTask<T> TryCatch<T>(Func<ValueTask<T>> operation)
    {
        try
        {
            return await operation()
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException exception)
        {
            throw new TransactionMetadataCancellationException(exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new TransactionDirectoryDependencyException(exception);
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (StorageResourceException exception)
        {
            throw new StorageResourceException(message: exception.Message, innerException: exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new sql_storage_engine.Models.Exceptions.StorageSavepointOperationException(innerException: exception);
        }
        catch (AggregateException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }
}