// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Exceptions;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.HeapPageHeaders;
internal sealed partial class HeapPageHeaderService
{
    private static T TryCatch<T>(Func<ReadableHeapPageHeaderContext, T> operation, ReadableHeapPageHeaderContext context)
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

    private static void TryCatch(Action<WritableHeapPageHeaderContext> operation, WritableHeapPageHeaderContext context)
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
}