// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using sql_storage_engine.Models.Checksums;
using sql_storage_engine.Models.Exceptions;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.DatabasePages;
internal sealed partial class PageHeaderDatabasePageProcessingService
{
    private static void TryCatch(Action<WritablePageChecksumContext> operation, WritablePageChecksumContext context)
    {
        try
        {
            operation(obj: context);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
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

    private static void TryCatch(Action<ReadablePageChecksumContext> operation, ReadablePageChecksumContext context)
    {
        try
        {
            operation(obj: context);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
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