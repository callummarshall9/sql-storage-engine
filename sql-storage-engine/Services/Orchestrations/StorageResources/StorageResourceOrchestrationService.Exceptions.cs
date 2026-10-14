// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using sql_storage_engine.Models.Exceptions;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.StorageResources;
internal sealed partial class StorageResourceOrchestrationService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (StorageResourceExhaustedException exception)
        {
            throw new StorageResourceExhaustedException(message: exception.Message, innerException: exception);
        }
        catch (StorageModelDependencyException exception)
        {
            throw new StorageModelDependencyException(exception);
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
        catch (StorageResourceExhaustedException exception)
        {
            throw new StorageResourceExhaustedException(message: exception.Message, innerException: exception);
        }
        catch (StorageModelDependencyException exception)
        {
            throw new StorageModelDependencyException(exception);
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