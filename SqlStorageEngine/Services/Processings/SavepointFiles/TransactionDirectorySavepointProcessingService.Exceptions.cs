// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Processings.SavepointFiles;
internal sealed partial class TransactionDirectorySavepointProcessingService
{
    private static void TryCatch(Action operation)
    {
        try
        {
            operation();
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new TransactionDirectoryDependencyException(exception);
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
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new TransactionDirectoryDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }
}