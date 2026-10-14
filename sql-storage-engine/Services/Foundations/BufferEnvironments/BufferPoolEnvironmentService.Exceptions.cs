// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using sql_storage_engine.Models.Exceptions;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.BufferEnvironments;
internal sealed partial class BufferPoolEnvironmentService
{
    private static void TryCatch(Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception exception)when (IsKnownException(exception: exception))
        {
            throw MapKnownException(caughtException: exception);
        }
        catch (StorageResourceException exception)
        {
            throw new BufferPoolDependencyException(innerException: exception);
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
            throw new BufferPoolIOExceptionDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }

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
        catch (StorageResourceException exception)
        {
            throw new BufferPoolDependencyException(innerException: exception);
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
            throw new BufferPoolIOExceptionDependencyException(innerException: exception);
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
        catch (StorageResourceException exception)
        {
            throw new BufferPoolDependencyException(innerException: exception);
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
            throw new BufferPoolIOExceptionDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }

    private static bool IsKnownException(Exception exception) =>
        exception is InvalidDatabaseMagicException or InvalidPageSizeException or UnsupportedDatabaseVersionException or StorageCorruptionException or StorageFormatException or OperationCanceledException or ObjectDisposedException or InvalidOperationException or OverflowException;

    private static Exception MapKnownException(Exception caughtException) =>
        caughtException switch
    {
        InvalidDatabaseMagicException exception => new InvalidDatabaseMagicException(innerException: exception),
        InvalidPageSizeException exception => new InvalidPageSizeException(pageSize: exception.PageSize, innerException: exception),
        UnsupportedDatabaseVersionException exception => new UnsupportedDatabaseVersionException(version: exception.Version, innerException: exception),
        StorageCorruptionException exception => new StorageCorruptionException(message: exception.Message, innerException: exception),
        StorageFormatException exception => new StorageFormatException(message: exception.Message, innerException: exception),
        OperationCanceledException exception => new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken),
        ObjectDisposedException exception => new ObjectDisposedException(message: exception.Message, innerException: exception),
        InvalidOperationException exception => new InvalidOperationException(message: exception.Message, innerException: exception),
        OverflowException exception => new OverflowException(message: exception.Message, innerException: exception),
        _ => throw new ArgumentException(message: "Unsupported exception mapping.", paramName: nameof(caughtException))};
}