// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using System.IO;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.PageFiles;
internal sealed partial class PageFileService
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
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new PageFileDependencyException(innerException: exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new PageFileDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
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
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new PageFileDependencyException(innerException: exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new PageFileDependencyException(innerException: exception);
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
            throw new PageFileDependencyException(innerException: exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new PageFileDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }

    private static bool IsKnownException(Exception exception) =>
        exception is OperationCanceledException or ObjectDisposedException or EndOfStreamException or OverflowException;

    private static Exception MapKnownException(Exception caughtException) =>
        caughtException switch
    {
        OperationCanceledException exception => new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken),
        ObjectDisposedException exception => new ObjectDisposedException(message: exception.Message, innerException: exception),
        EndOfStreamException exception => new StorageFormatException(message: exception.Message, innerException: exception),
        OverflowException exception => new OverflowException(message: exception.Message, innerException: exception),
        _ => throw new ArgumentException(message: "Unsupported exception mapping.", paramName: nameof(caughtException))};
}