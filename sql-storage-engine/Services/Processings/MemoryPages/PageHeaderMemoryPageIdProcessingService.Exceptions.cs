// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using System.IO;
using sql_storage_engine.Models.Exceptions;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.MemoryPages;
internal sealed partial class PageHeaderMemoryPageIdProcessingService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception exception)when (IsKnownException(exception: exception))
        {
            throw MapKnownException(caughtException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new StorageModelDependencyException(exception);
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
        catch (Exception exception)when (IsKnownException(exception: exception))
        {
            throw MapKnownException(caughtException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new StorageModelDependencyException(exception);
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
        catch (Exception exception)when (IsKnownException(exception: exception))
        {
            throw MapKnownException(caughtException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new StorageModelDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }

    private static bool IsKnownException(Exception exception) =>
        exception is OperationCanceledException or ObjectDisposedException or StorageResourceException or OverflowException;

    private static Exception MapKnownException(Exception caughtException) =>
        caughtException switch
    {
        OperationCanceledException exception => new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken),
        ObjectDisposedException exception => new ObjectDisposedException(message: exception.Message, innerException: exception),
        StorageResourceException exception => new StorageResourceException(message: exception.Message, innerException: exception),
        OverflowException exception => new OverflowException(message: exception.Message, innerException: exception),
        _ => throw new ArgumentException(message: "Unsupported exception mapping.", paramName: nameof(caughtException))};
}