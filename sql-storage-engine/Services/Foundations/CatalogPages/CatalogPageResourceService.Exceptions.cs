// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading.Tasks;
using System;
using System.IO;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Foundations.CatalogPages;
internal sealed partial class CatalogPageResourceService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new StorageModelRangeValidationException(innerException: exception);
        }
        catch (Exception exception)when (IsKnownException(exception: exception))
        {
            throw MapKnownException(caughtException: exception);
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
        catch (Exception exception)when (IsKnownException(exception: exception))
        {
            throw MapKnownException(caughtException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(innerException: exception);
        }
        catch (IOException exception)
        {
            throw new StorageModelDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }

    private static async ValueTask TryCatch(Func<ValueTask> operation)
    {
        try
        {
            await operation()
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception exception)when (IsKnownException(exception: exception))
        {
            throw MapKnownException(caughtException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(innerException: exception);
        }
        catch (IOException exception)
        {
            throw new StorageModelDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }

    private static bool IsKnownException(Exception exception) =>
        exception is sql_storage_engine.Storage.StorageCorruptionException or sql_storage_engine.Storage.StorageFormatException or sql_storage_engine.Storage.StorageResourceExhaustedException or OperationCanceledException;

    private static Exception MapKnownException(Exception caughtException) =>
        caughtException switch
    {
        sql_storage_engine.Storage.StorageCorruptionException exception => new sql_storage_engine.Storage.StorageCorruptionException(message: exception.Message, innerException: exception),
        sql_storage_engine.Storage.StorageFormatException exception => new sql_storage_engine.Storage.StorageFormatException(message: exception.Message, innerException: exception),
        sql_storage_engine.Storage.StorageResourceExhaustedException exception => new sql_storage_engine.Storage.StorageResourceExhaustedException(message: exception.Message, innerException: exception),
        OperationCanceledException exception => new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken),
        _ => throw new ArgumentException(message: "Unsupported exception mapping.", paramName: nameof(caughtException))};
}