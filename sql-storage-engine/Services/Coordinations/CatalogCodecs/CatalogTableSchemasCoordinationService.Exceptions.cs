// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Coordinations.CatalogCodecs;
internal sealed partial class CatalogTableSchemasCoordinationService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new StorageModelRangeValidationException(innerException: exception);
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
            throw new StorageModelDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }
}