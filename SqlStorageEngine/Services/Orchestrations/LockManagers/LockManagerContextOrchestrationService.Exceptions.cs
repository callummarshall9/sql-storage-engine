// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Orchestrations.LockManagers;
internal sealed partial class LockManagerContextOrchestrationService
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
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(message: exception.Message, innerException: exception);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new StorageModelRangeValidationException(exception);
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
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(message: exception.Message, innerException: exception);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new StorageModelRangeValidationException(exception);
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
}