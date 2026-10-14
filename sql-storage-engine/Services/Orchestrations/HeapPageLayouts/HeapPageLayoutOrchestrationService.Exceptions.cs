// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Exceptions;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.HeapPageLayouts;
internal sealed partial class HeapPageLayoutOrchestrationService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (System.IO.IOException exception)
        {
            throw new StorageModelDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }

    private static T TryCatch<T>(Func<ReadableHeapPageLayoutContext, T> operation, ReadableHeapPageLayoutContext context)
    {
        try
        {
            return operation(arg: context);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (System.IO.IOException exception)
        {
            throw new StorageModelDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }

    private static void TryCatch(Action<WritableHeapPageLayoutContext> operation, WritableHeapPageLayoutContext context)
    {
        try
        {
            operation(obj: context);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (System.IO.IOException exception)
        {
            throw new StorageModelDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }

    private static T TryCatch<T>(Func<HeapPageRowMutationContext, T> operation, HeapPageRowMutationContext context)
    {
        try
        {
            return operation(arg: context);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (System.IO.IOException exception)
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
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (System.IO.IOException exception)
        {
            throw new StorageModelDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }
}