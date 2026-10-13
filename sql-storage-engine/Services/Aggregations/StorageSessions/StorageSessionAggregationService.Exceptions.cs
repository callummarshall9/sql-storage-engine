// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading.Tasks;
using System;
using System.IO;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
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
        catch (OverflowException exception)
        {
            throw new OverflowException(message: exception.Message, innerException: exception);
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (AggregateException)
        {
            throw;
        }
        catch (sql_storage_engine.Security.StorageAuthorizationException)
        {
            throw;
        }
        catch (sql_storage_engine.Security.StorageAuditException)
        {
            throw;
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
        catch (StorageModelValidationException)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(message: exception.Message, paramName: exception.ParamName, innerException: exception);
        }
        catch (sql_storage_engine.Storage.GraphReferentialIntegrityException)
        {
            throw;
        }
        catch (sql_storage_engine.Tables.TableMutationException)
        {
            throw;
        }
        catch (sql_storage_engine.Tables.DuplicateKeyIgnoredException exception)
        {
            throw new sql_storage_engine.Tables.DuplicateKeyIgnoredException(message: exception.Message, innerException: exception);
        }
        catch (sql_storage_engine.Storage.IndexBuildException)
        {
            throw;
        }
        catch (sql_storage_engine.Storage.CatalogConflictException exception)
        {
            throw new sql_storage_engine.Storage.CatalogConflictException(message: exception.Message, innerException: exception);
        }
        catch (sql_storage_engine.Transactions.DeadlockException exception)
        {
            throw new sql_storage_engine.Transactions.DeadlockException(victimTransactionId: exception.VictimTransactionId, innerException: exception);
        }
        catch (sql_storage_engine.Storage.StorageResourceException exception)
        {
            throw new StorageModelDependencyException(innerException: exception);
        }
        catch (sql_storage_engine.Storage.StorageException exception)
        {
            throw new sql_storage_engine.Storage.StorageException(message: exception.Message, innerException: exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(message: exception.Message, innerException: exception);
        }
        catch (IOException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }

    private static async ValueTask<T> TryCatch<T>(Func<ValueTask<T>> operation)
    {
        try
        {
            return await operation()
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OverflowException exception)
        {
            throw new OverflowException(message: exception.Message, innerException: exception);
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (AggregateException)
        {
            throw;
        }
        catch (sql_storage_engine.Security.StorageAuthorizationException)
        {
            throw;
        }
        catch (sql_storage_engine.Security.StorageAuditException)
        {
            throw;
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
        catch (ArgumentOutOfRangeException exception)
        {
            throw new StorageModelRangeValidationException(innerException: exception);
        }
        catch (StorageModelValidationException)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(message: exception.Message, paramName: exception.ParamName, innerException: exception);
        }
        catch (sql_storage_engine.Storage.GraphReferentialIntegrityException)
        {
            throw;
        }
        catch (sql_storage_engine.Tables.TableMutationException)
        {
            throw;
        }
        catch (sql_storage_engine.Tables.DuplicateKeyIgnoredException exception)
        {
            throw new sql_storage_engine.Tables.DuplicateKeyIgnoredException(message: exception.Message, innerException: exception);
        }
        catch (sql_storage_engine.Storage.IndexBuildException)
        {
            throw;
        }
        catch (sql_storage_engine.Storage.CatalogConflictException exception)
        {
            throw new sql_storage_engine.Storage.CatalogConflictException(message: exception.Message, innerException: exception);
        }
        catch (sql_storage_engine.Transactions.DeadlockException exception)
        {
            throw new sql_storage_engine.Transactions.DeadlockException(victimTransactionId: exception.VictimTransactionId, innerException: exception);
        }
        catch (sql_storage_engine.Storage.StorageResourceException exception)
        {
            throw new StorageModelDependencyException(innerException: exception);
        }
        catch (sql_storage_engine.Storage.StorageException exception)
        {
            throw new sql_storage_engine.Storage.StorageException(message: exception.Message, innerException: exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(message: exception.Message, innerException: exception);
        }
        catch (IOException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }
}