// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Foundations.CheckpointReferences;
internal sealed partial class CheckpointLogSequenceNumberService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (OperationCanceledException exception)
        {
            throw new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (System.IO.IOException exception)
        {
            throw new CheckpointPublicationDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }

    private static void TryCatch(Action operation)
    {
        try
        {
            operation();
        }
        catch (OperationCanceledException exception)
        {
            throw new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (System.IO.IOException exception)
        {
            throw new CheckpointPublicationDependencyException(exception);
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
            throw new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (System.IO.IOException exception)
        {
            throw new CheckpointPublicationDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }
}