// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Threading.Tasks;
using sql_storage_engine.Models.Exceptions;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.DatabaseFileProbes;
internal sealed partial class TransactionDirectoryDatabaseFileProcessingService
{
    private static async ValueTask<int> TryCatch(Func<ValueTask<int>> operation, string path)
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
            throw new DatabaseProbeDependencyException(path: path, innerException: exception);
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
        catch (TransactionDirectoryDependencyException exception)
        {
            throw new TransactionDirectoryDependencyException(innerException: exception);
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
            throw new DatabaseProbeDependencyException(path: "database", innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }

    private static void TryCatch(Action operation)
    {
        try
        {
            operation();
        }
        catch (TransactionDirectoryDependencyException exception)
        {
            throw new TransactionDirectoryDependencyException(innerException: exception);
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
            throw new DatabaseProbeDependencyException(path: "database", innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }

    private static bool IsKnownException(Exception exception) =>
        exception is InvalidDatabaseMagicException or InvalidPageSizeException or StorageFormatException or OperationCanceledException;

    private static Exception MapKnownException(Exception caughtException) =>
        caughtException switch
    {
        InvalidDatabaseMagicException exception => new InvalidDatabaseMagicException(innerException: exception),
        InvalidPageSizeException exception => new InvalidPageSizeException(pageSize: exception.PageSize, innerException: exception),
        StorageFormatException exception => new StorageFormatException(message: exception.Message, innerException: exception),
        OperationCanceledException exception => new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken),
        _ => throw new ArgumentException(message: "Unsupported exception mapping.", paramName: nameof(caughtException))};
}