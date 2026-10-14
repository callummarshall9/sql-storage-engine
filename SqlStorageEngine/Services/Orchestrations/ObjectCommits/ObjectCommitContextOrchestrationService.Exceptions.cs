// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Threading.Tasks;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.ObjectCommits;
internal sealed partial class ObjectCommitContextOrchestrationService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (StorageObjectVersionConflictException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (StorageResourceException exception)
        {
            throw new StorageModelDependencyException(innerException: exception);
        }
        catch (StorageException)
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
        catch (IOException exception)
        {
            throw new StorageModelDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }

    private static async ValueTask<T> TryCatch<T>(Func<ValueTask<T>> operation)
    {
        try
        {
            return await operation()
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (StorageObjectVersionConflictException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (StorageResourceException exception)
        {
            throw new StorageModelDependencyException(innerException: exception);
        }
        catch (StorageException)
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
        catch (IOException exception)
        {
            throw new StorageModelDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }
}