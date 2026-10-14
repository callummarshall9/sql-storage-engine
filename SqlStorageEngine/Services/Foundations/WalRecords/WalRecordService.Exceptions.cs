// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Models.Exceptions;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.WalRecords;
internal sealed partial class WalRecordService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
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

    private static T TryCatch<T>(Func<ReadableWalFormatContext, T> operation, ReadableWalFormatContext context)
    {
        try
        {
            return operation(arg: context);
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
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