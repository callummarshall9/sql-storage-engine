// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using sql_storage_engine.Models.Exceptions;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Managements.EngineResources;
internal sealed partial class EngineResourceContextManagementService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (InvalidDatabaseMagicException exception)
        {
            throw new InvalidDatabaseMagicException(innerException: exception);
        }
        catch (InvalidPageSizeException exception)
        {
            throw new InvalidPageSizeException(pageSize: exception.PageSize, innerException: exception);
        }
        catch (UnsupportedDatabaseVersionException exception)
        {
            throw new UnsupportedDatabaseVersionException(version: exception.Version, innerException: exception);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (RecoveryRequiredException exception)
        {
            throw new RecoveryRequiredException(innerException: exception);
        }
        catch (TransactionDirectoryDependencyException exception)
        {
            throw new TransactionDirectoryDependencyException(innerException: exception);
        }
        catch (StorageResourceException exception)
        {
            throw new PageDatabaseDependencyException(innerException: exception);
        }
        catch (OperationCanceledException exception)
        {
            throw new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken);
        }
        catch (ObjectDisposedException exception)
        {
            throw new ObjectDisposedException(message: exception.Message, innerException: exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(message: exception.Message, innerException: exception);
        }
        catch (OverflowException exception)
        {
            throw new OverflowException(message: exception.Message, innerException: exception);
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
            throw new StorageIoDependencyException(innerException: exception);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(message: exception.Message, innerException: exception);
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
        catch (InvalidDatabaseMagicException exception)
        {
            throw new InvalidDatabaseMagicException(innerException: exception);
        }
        catch (InvalidPageSizeException exception)
        {
            throw new InvalidPageSizeException(pageSize: exception.PageSize, innerException: exception);
        }
        catch (UnsupportedDatabaseVersionException exception)
        {
            throw new UnsupportedDatabaseVersionException(version: exception.Version, innerException: exception);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (RecoveryRequiredException exception)
        {
            throw new RecoveryRequiredException(innerException: exception);
        }
        catch (TransactionDirectoryDependencyException exception)
        {
            throw new TransactionDirectoryDependencyException(innerException: exception);
        }
        catch (StorageResourceException exception)
        {
            throw new PageDatabaseDependencyException(innerException: exception);
        }
        catch (OperationCanceledException exception)
        {
            throw new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken);
        }
        catch (ObjectDisposedException exception)
        {
            throw new ObjectDisposedException(message: exception.Message, innerException: exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(message: exception.Message, innerException: exception);
        }
        catch (OverflowException exception)
        {
            throw new OverflowException(message: exception.Message, innerException: exception);
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
            throw new StorageIoDependencyException(innerException: exception);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(message: exception.Message, innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }

    private static async ValueTask<T> TryCatch<T>(Func<ValueTask<T>> operation, Task? task)
    {
        try
        {
            return await operation()
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception)when (task?.IsFaulted == true)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(message: exception.Message, innerException: exception);
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
            throw new StorageIoDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }
}