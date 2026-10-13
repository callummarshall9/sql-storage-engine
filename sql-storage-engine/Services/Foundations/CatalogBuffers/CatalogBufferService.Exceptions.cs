// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading.Tasks;
using System;
using System.IO;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Foundations.CatalogBuffers;
internal sealed partial class CatalogBufferService
{
    private static async ValueTask<T> TryCatch<T>(Func<ValueTask<T>> operation)
    {
        try
        {
            return await operation()
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (sql_storage_engine.Storage.StorageCorruptionException exception)
        {
            throw new sql_storage_engine.Storage.StorageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (sql_storage_engine.Storage.StorageFormatException exception)
        {
            throw new sql_storage_engine.Storage.StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (sql_storage_engine.Storage.StorageResourceExhaustedException exception)
        {
            throw new sql_storage_engine.Storage.StorageResourceExhaustedException(message: exception.Message, innerException: exception);
        }
        catch (OperationCanceledException exception)
        {
            throw new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(innerException: exception);
        }
        catch (IOException exception)
        {
            throw new StorageModelDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }
}